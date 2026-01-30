using Alt_Support.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Alt_Support.Services
{
    public interface IReleaseBranchService
    {
        Task<ReleaseBranchResponse> GetReleaseBranchesAsync(DateTime? startDate = null, DateTime? endDate = null, string? branchFilter = null, bool forceRefresh = false);
        Task<ReleaseBranchInfo?> GetReleaseBranchDetailsAsync(string branchName, DateTime? startDate = null, DateTime? endDate = null);
    }

    public class ReleaseBranchService : IReleaseBranchService
    {
        private readonly IJiraService _jiraService;
        private readonly GitHubService _gitHubService;
        private readonly ILogger<ReleaseBranchService> _logger;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY = "ReleaseBranches";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

        public ReleaseBranchService(
            IJiraService jiraService, 
            GitHubService gitHubService, 
            ILogger<ReleaseBranchService> logger, 
            IMemoryCache cache)
        {
            _jiraService = jiraService;
            _gitHubService = gitHubService;
            _logger = logger;
            _cache = cache;
        }

        public async Task<ReleaseBranchResponse> GetReleaseBranchesAsync(
            DateTime? startDate = null, 
            DateTime? endDate = null, 
            string? branchFilter = null, 
            bool forceRefresh = false)
        {
            var startTime = DateTime.UtcNow;

            // Create cache key with parameters
            string cacheKey = $"{CACHE_KEY}_{startDate?.ToString("yyyyMMdd")}_{endDate?.ToString("yyyyMMdd")}_{branchFilter ?? "all"}";

            // Clear cache if force refresh requested
            if (forceRefresh)
            {
                _logger.LogInformation("Force refresh requested - clearing release branches cache");
                _cache.Remove(cacheKey);
            }

            // Check cache first
            if (_cache.TryGetValue(cacheKey, out ReleaseBranchResponse? cachedResponse) && cachedResponse != null)
            {
                _logger.LogInformation("Returning cached release branches data");
                return cachedResponse;
            }

            _logger.LogInformation("Fetching release branches data. Date range: {StartDate} to {EndDate}, Filter: {Filter}", 
                startDate, endDate, branchFilter ?? "none");

            // Fetch tickets from Jira
            var tickets = await FetchTicketsWithPRsAsync(startDate, endDate);
            _logger.LogInformation("Fetched {Count} tickets with PR links", tickets.Count);

            // Process tickets and group by release branch
            var releaseBranches = await GroupTicketsByReleaseBranchAsync(tickets, branchFilter);

            var processingTime = DateTime.UtcNow - startTime;

            var response = new ReleaseBranchResponse
            {
                TotalReleaseBranches = releaseBranches.Count,
                TotalTickets = releaseBranches.Sum(rb => rb.TotalTickets),
                TotalMergedPRs = releaseBranches.Sum(rb => rb.TotalPRs),
                LastUpdated = DateTime.UtcNow,
                ProcessingTimeMs = (int)processingTime.TotalMilliseconds,
                ReleaseBranches = releaseBranches.OrderByDescending(rb => rb.LatestMergeDate ?? DateTime.MinValue).ToList()
            };

            // Cache the result
            _cache.Set(cacheKey, response, CacheDuration);

            _logger.LogInformation("Release branches data processed in {Time}ms. Found {Branches} release branches with {Tickets} tickets",
                processingTime.TotalMilliseconds, response.TotalReleaseBranches, response.TotalTickets);

            return response;
        }

        public async Task<ReleaseBranchInfo?> GetReleaseBranchDetailsAsync(
            string branchName, 
            DateTime? startDate = null, 
            DateTime? endDate = null)
        {
            var allBranches = await GetReleaseBranchesAsync(startDate, endDate);
            return allBranches.ReleaseBranches.FirstOrDefault(rb => 
                rb.BranchName.Equals(branchName, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<List<TicketInfo>> FetchTicketsWithPRsAsync(DateTime? startDate, DateTime? endDate)
        {
            // Default to last 60 days if no date range specified
            if (!startDate.HasValue && !endDate.HasValue)
            {
                endDate = DateTime.Now;
                startDate = DateTime.Now.AddDays(-60);
            }
            
            // Build JQL query - fetch tickets from ALL projects within date range
            var startDateStr = startDate!.Value.ToString("yyyy-MM-dd");
            var endDateStr = endDate!.Value.AddDays(1).ToString("yyyy-MM-dd");
            var jql = $"updated >= '{startDateStr}' AND updated < '{endDateStr}' ORDER BY updated DESC";
            
            _logger.LogInformation("Executing JQL for release branch tracking: {JQL}", jql);

            List<TicketInfo> tickets;
            try
            {
                tickets = await _jiraService.SearchTicketsAsync(jql, 1000);
                _logger.LogInformation("Jira returned {Count} tickets", tickets.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch tickets from Jira");
                return new List<TicketInfo>();
            }

            // Log some sample tickets to debug
            foreach (var ticket in tickets.Take(5))
            {
                _logger.LogInformation("Sample ticket: {Key} - PRLinks count: {PrCount}, PRs: {PRs}", 
                    ticket.TicketKey, 
                    ticket.PrLinks?.Count ?? 0,
                    string.Join(", ", ticket.PrLinks ?? new List<string>()));
            }

            // First check tickets that already have PR links from custom fields/description
            var ticketsWithPRs = tickets.Where(t => t.PrLinks != null && t.PrLinks.Any()).ToList();
            _logger.LogInformation("Found {Count} tickets with PR links in custom fields/description", ticketsWithPRs.Count);

            // For tickets without PR links, try to fetch from Jira's Development panel
            var ticketsWithoutPRs = tickets.Where(t => t.PrLinks == null || !t.PrLinks.Any()).ToList();
            _logger.LogInformation("Checking {Count} tickets for PR links in Development panel...", ticketsWithoutPRs.Count);

            var semaphore = new SemaphoreSlim(10); // Limit concurrent API calls
            var additionalTicketsWithPRs = new List<TicketInfo>();

            var fetchTasks = ticketsWithoutPRs.Select(async ticket =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var devPrLinks = await _jiraService.GetDevelopmentPRLinksAsync(ticket.TicketKey);
                    if (devPrLinks.Any())
                    {
                        ticket.PrLinks = devPrLinks;
                        lock (additionalTicketsWithPRs)
                        {
                            additionalTicketsWithPRs.Add(ticket);
                        }
                        _logger.LogInformation("Found {Count} PR links from Development panel for {TicketKey}", devPrLinks.Count, ticket.TicketKey);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error fetching dev PR links for {TicketKey}", ticket.TicketKey);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(fetchTasks);

            // Combine both sources
            ticketsWithPRs.AddRange(additionalTicketsWithPRs);
            
            _logger.LogInformation("Total tickets with PR links: {Total} (Custom fields: {Custom}, Development panel: {Dev})", 
                ticketsWithPRs.Count, 
                ticketsWithPRs.Count - additionalTicketsWithPRs.Count, 
                additionalTicketsWithPRs.Count);

            if (ticketsWithPRs.Count == 0)
            {
                _logger.LogWarning("No tickets with PR links found from any source");
            }

            return ticketsWithPRs;
        }

        private async Task<List<ReleaseBranchInfo>> GroupTicketsByReleaseBranchAsync(
            List<TicketInfo> tickets, 
            string? branchFilter)
        {
            // Dictionary to group tickets by release branch
            var branchTickets = new ConcurrentDictionary<string, ConcurrentBag<(ReleaseBranchTicket Ticket, ReleaseBranchPR PR)>>();

            // Process tickets in parallel to fetch PR details
            var semaphore = new SemaphoreSlim(5); // Limit concurrent GitHub API calls

            var tasks = tickets.Select(async ticket =>
            {
                await semaphore.WaitAsync();
                try
                {
                    _logger.LogDebug("Processing ticket {TicketKey} with {PrCount} PR links", ticket.TicketKey, ticket.PrLinks.Count);
                    
                    foreach (var prUrl in ticket.PrLinks)
                    {
                        try
                        {
                            _logger.LogDebug("Fetching PR details for {PrUrl}", prUrl);
                            var prDetails = await _gitHubService.GetPRDetailsAsync(prUrl);
                            if (prDetails == null)
                            {
                                _logger.LogWarning("Failed to get PR details for {PrUrl} - returned null", prUrl);
                                continue;
                            }
                            
                            _logger.LogInformation("PR #{Number} for {TicketKey}: BaseBranch={BaseBranch}, HeadBranch={HeadBranch}, Merged={IsMerged}",
                                prDetails.Number, ticket.TicketKey, prDetails.BaseBranch, prDetails.HeadBranch, prDetails.IsMerged);

                            var baseBranch = prDetails.BaseBranch;
                            
                            // Skip if no base branch or doesn't match filter
                            if (string.IsNullOrEmpty(baseBranch)) continue;
                            
                            // Apply branch filter if specified (e.g., "release/" to only show release branches)
                            if (!string.IsNullOrEmpty(branchFilter) && 
                                !baseBranch.Contains(branchFilter, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            // Create ticket info
                            var ticketInfo = new ReleaseBranchTicket
                            {
                                TicketKey = ticket.TicketKey,
                                Title = ticket.Title,
                                Status = ticket.Status,
                                Priority = ticket.Priority,
                                Assignee = ticket.Assignee,
                                Reporter = ticket.Reporter,
                                CreatedDate = ticket.CreatedDate,
                                ResolvedDate = ticket.ResolvedDate,
                                JiraUrl = $"https://navex.atlassian.net/browse/{ticket.TicketKey}",
                                FixVersions = ticket.FixVersions ?? new List<string>()
                            };

                            // Create PR info
                            var prInfo = new ReleaseBranchPR
                            {
                                PrNumber = prDetails.Number,
                                PrUrl = prDetails.HtmlUrl,
                                Title = prDetails.Title,
                                State = prDetails.State,
                                BaseBranch = prDetails.BaseBranch,
                                HeadBranch = prDetails.HeadBranch,
                                IsMerged = prDetails.IsMerged,
                                MergedAt = prDetails.MergedAt,
                                MergedBy = prDetails.MergedBy,
                                Author = prDetails.User
                            };

                            // Add to the appropriate branch group
                            if (!branchTickets.ContainsKey(baseBranch))
                            {
                                branchTickets[baseBranch] = new ConcurrentBag<(ReleaseBranchTicket, ReleaseBranchPR)>();
                            }
                            branchTickets[baseBranch].Add((ticketInfo, prInfo));

                            _logger.LogDebug("Ticket {TicketKey} PR #{PRNumber} -> {BaseBranch}", 
                                ticket.TicketKey, prDetails.Number, baseBranch);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch PR details for {PrUrl}", prUrl);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Build ReleaseBranchInfo objects
            var result = new List<ReleaseBranchInfo>();

            foreach (var kvp in branchTickets)
            {
                var branchName = kvp.Key;
                var items = kvp.Value.ToList();

                // Group by ticket to consolidate multiple PRs per ticket
                var ticketGroups = items.GroupBy(x => x.Ticket.TicketKey);
                var consolidatedTickets = new List<ReleaseBranchTicket>();

                foreach (var ticketGroup in ticketGroups)
                {
                    var firstTicket = ticketGroup.First().Ticket;
                    firstTicket.MergedPRs = ticketGroup.Select(x => x.PR).ToList();
                    consolidatedTickets.Add(firstTicket);
                }

                var allPRs = items.Select(x => x.PR).ToList();
                var mergedPRs = allPRs.Where(p => p.MergedAt.HasValue).ToList();

                var branchInfo = new ReleaseBranchInfo
                {
                    BranchName = branchName,
                    ReleaseVersion = ExtractVersionFromBranch(branchName),
                    TotalTickets = consolidatedTickets.Count,
                    TotalPRs = allPRs.Count,
                    EarliestMergeDate = mergedPRs.Any() ? mergedPRs.Min(p => p.MergedAt) : null,
                    LatestMergeDate = mergedPRs.Any() ? mergedPRs.Max(p => p.MergedAt) : null,
                    Tickets = consolidatedTickets.OrderByDescending(t => t.CreatedDate).ToList()
                };

                result.Add(branchInfo);
            }

            return result;
        }

        private string ExtractVersionFromBranch(string branchName)
        {
            // Extract version from branch name like "release/9.91.0" -> "9.91.0"
            if (branchName.StartsWith("release/", StringComparison.OrdinalIgnoreCase))
            {
                return branchName.Substring(8);
            }
            
            // For other branch patterns, return the branch name as-is
            return branchName;
        }
    }
}
