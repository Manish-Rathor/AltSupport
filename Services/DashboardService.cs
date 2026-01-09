using Alt_Support.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Alt_Support.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IJiraService _jiraService;
        private readonly ILogger<DashboardService> _logger;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY = "ApplicationDashboard";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

        // Keyword categories - will be ranked by occurrence count
        private static readonly List<CategoryKeyword> CategoryKeywords = new()
        {
            new CategoryKeyword { Category = "EPIM", Keywords = new[] { "EPIM", "epim" } },
            new CategoryKeyword { Category = "EPAdmin", Keywords = new[] { "EPAdmin", "epadmin", "EP Admin" } },
            new CategoryKeyword { Category = "Standard Intake", Keywords = new[] { "Standard Intake", "standard intake", "StandardIntake", "Hotline", "hotline" } },
            new CategoryKeyword { Category = "Call Center", Keywords = new[] { "Call Center", "call center", "CallCenter", "Contact center", "contact center", "ContactCenter" } },
            new CategoryKeyword { Category = "Digital Intake", Keywords = new[] { "Digital Intake", "digital intake", "DI", " di " } },
            new CategoryKeyword { Category = "Digital Intake Call Center", Keywords = new[] { "Digital Intake Call Center", "DICC", "dicc" } },
            new CategoryKeyword { Category = "WIF", Keywords = new[] { "WIF", "wif" } },
            new CategoryKeyword { Category = "Database", Keywords = new[] { "Database", "database", "DB", " db " } },
            new CategoryKeyword { Category = "Platforminator", Keywords = new[] { "Platforminator", "platforminator", "Platform" } }
        };

        public DashboardService(IJiraService jiraService, ILogger<DashboardService> logger, IMemoryCache cache)
        {
            _jiraService = jiraService;
            _logger = logger;
            _cache = cache;
        }

        public async Task<DashboardResponse> GetApplicationLevelDashboardAsync(DateTime? startDate = null, DateTime? endDate = null, bool forceRefresh = false)
        {
            // Create cache key with date parameters
            string cacheKey = CACHE_KEY;
            if (startDate.HasValue || endDate.HasValue)
            {
                cacheKey = $"{CACHE_KEY}_{startDate?.ToString("yyyyMMdd")}_{endDate?.ToString("yyyyMMdd")}";
            }

            // Clear cache if force refresh requested
            if (forceRefresh)
            {
                _logger.LogInformation("Force refresh requested - clearing cache for key: {CacheKey}", cacheKey);
                _cache.Remove(cacheKey);
            }

            // Check cache first
            if (_cache.TryGetValue(cacheKey, out DashboardResponse? cachedDashboard) && cachedDashboard != null)
            {
                _logger.LogInformation("Returning cached dashboard data for key: {CacheKey}", cacheKey);
                return cachedDashboard;
            }

            _logger.LogInformation("Cache miss - fetching fresh dashboard data with date range: {StartDate} to {EndDate}", 
                startDate, endDate);
            var dashboard = await FetchAndCategorizeDashboardAsync(startDate, endDate);

            // Cache the result
            _cache.Set(cacheKey, dashboard, CacheDuration);

            return dashboard;
        }

        public async Task<CategoryTicketsResponse> GetTicketsByCategoryAsync(string categoryName, int page, int pageSize, DateTime? startDate = null, DateTime? endDate = null)
        {
            var dashboard = await GetApplicationLevelDashboardAsync(startDate, endDate);
            var category = dashboard.Categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

            if (category == null)
            {
                return new CategoryTicketsResponse
                {
                    CategoryName = categoryName,
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = 0,
                    Tickets = new List<DashboardTicket>()
                };
            }

            // Implement pagination
            var skip = (page - 1) * pageSize;
            var paginatedTickets = category.Tickets.Skip(skip).Take(pageSize).ToList();
            var totalPages = (int)Math.Ceiling(category.Count / (double)pageSize);

            return new CategoryTicketsResponse
            {
                CategoryName = categoryName,
                TotalCount = category.Count,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Tickets = paginatedTickets
            };
        }

        public async Task<DashboardResponse> RefreshDashboardAsync()
        {
            _logger.LogInformation("Clearing dashboard cache and refreshing data");
            _cache.Remove(CACHE_KEY);
            return await GetApplicationLevelDashboardAsync();
        }

        private async Task<DashboardResponse> FetchAndCategorizeDashboardAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var startTime = DateTime.UtcNow;

            // Build JQL query with date filter
            var jql = "project = PRODSUP AND issuetype = \"SusEng Bug\"";
            
            // Default to last 30 days if no date range specified
            if (!startDate.HasValue && !endDate.HasValue)
            {
                endDate = DateTime.Now;
                startDate = DateTime.Now.AddDays(-30);
            }
            
            if (startDate.HasValue && endDate.HasValue)
            {
                // Convert to Jira date format (yyyy-MM-dd)
                var startDateStr = startDate.Value.ToString("yyyy-MM-dd");
                var endDateStr = endDate.Value.AddDays(1).ToString("yyyy-MM-dd"); // Add 1 day to include end date
                jql += $" AND created >= '{startDateStr}' AND created < '{endDateStr}'";
            }
            
            jql += " ORDER BY created DESC";
            
            _logger.LogInformation("Executing JQL: {JQL}", jql);

            var tickets = await _jiraService.SearchTicketsAsync(jql, 100); // Fetch up to 100 tickets for fast performance

            _logger.LogInformation("Fetched {Count} tickets from Jira", tickets.Count);

            // If no tickets found, log a warning
            if (tickets.Count == 0)
            {
                _logger.LogWarning("No tickets found for JQL query. Please verify the project name and issue type exist in your Jira instance.");
                _logger.LogWarning("Current query: {JQL}", jql);
                _logger.LogWarning("Try adjusting the query or check if tickets exist in Jira with this criteria.");
            }

            // Categorize tickets in parallel for better performance
            var categorizedTickets = new ConcurrentDictionary<string, ConcurrentBag<DashboardTicket>>();

            // Initialize all categories including "Others"
            foreach (var categoryKeyword in CategoryKeywords)
            {
                categorizedTickets[categoryKeyword.Category] = new ConcurrentBag<DashboardTicket>();
            }
            categorizedTickets["Others"] = new ConcurrentBag<DashboardTicket>();

            // Process tickets in parallel
            await Parallel.ForEachAsync(tickets, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (ticket, ct) =>
            {
                var category = await Task.Run(() => CategorizeTicket(ticket), ct);
                var dashboardTicket = ConvertToDashboardTicket(ticket);

                categorizedTickets[category].Add(dashboardTicket);
            });

            // Build response
            var categories = categorizedTickets.Select(kvp => new DashboardCategory
            {
                Name = kvp.Key,
                Count = kvp.Value.Count,
                Tickets = kvp.Value.OrderByDescending(t => t.CreatedDate).ToList()
            })
            .OrderByDescending(c => c.Count)
            .ToList();

            var processingTime = DateTime.UtcNow - startTime;

            var response = new DashboardResponse
            {
                TotalTickets = tickets.Count,
                Categories = categories,
                LastUpdated = DateTime.UtcNow,
                ProcessingTimeMs = (int)processingTime.TotalMilliseconds
            };

            _logger.LogInformation("Dashboard categorization completed in {Time}ms. Total: {Total}, Categories: {Categories}",
                processingTime.TotalMilliseconds, response.TotalTickets, response.Categories.Count);

            return response;
        }

        private string CategorizeTicket(TicketInfo ticket)
        {
            // Prepare search texts from Title, Description, and Test Cases
            var title = (ticket.Title ?? "").ToLower();
            var description = (ticket.Description ?? "").ToLower();
            var testCases = (ticket.TestCases ?? "").ToLower();
            var combinedText = $"{title} {description} {testCases}";

            // Count occurrences of each category's keywords
            var categoryCounts = new Dictionary<string, int>();

            foreach (var categoryKeyword in CategoryKeywords)
            {
                int totalCount = 0;

                foreach (var keyword in categoryKeyword.Keywords)
                {
                    var keywordLower = keyword.ToLower();
                    
                    // Count occurrences in title (highest priority)
                    totalCount += CountOccurrences(title, keywordLower) * 3;
                    
                    // Count occurrences in description (medium priority)
                    totalCount += CountOccurrences(description, keywordLower) * 2;
                    
                    // Count occurrences in test cases (lower priority)
                    totalCount += CountOccurrences(testCases, keywordLower);
                }

                if (totalCount > 0)
                {
                    categoryCounts[categoryKeyword.Category] = totalCount;
                }
            }

            // Return the category with the highest count
            if (categoryCounts.Count > 0)
            {
                return categoryCounts.OrderByDescending(x => x.Value).First().Key;
            }

            // No keyword found
            return "Others";
        }

        private int CountOccurrences(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return 0;

            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index += keyword.Length;
            }

            return count;
        }

        private DashboardTicket ConvertToDashboardTicket(TicketInfo ticket)
        {
            return new DashboardTicket
            {
                TicketKey = ticket.TicketKey,
                Title = ticket.Title,
                Status = ticket.Status,
                Priority = ticket.Priority,
                Assignee = ticket.Assignee,
                Reporter = ticket.Reporter,
                CreatedDate = ticket.CreatedDate,
                UpdatedDate = ticket.UpdatedDate,
                ResolvedDate = ticket.ResolvedDate,
                Description = TruncateText(ticket.Description, 200),
                Sprint = ticket.Sprint,
                Components = ticket.Components,
                Labels = ticket.Labels,
                PrLinks = ticket.PrLinks ?? new List<string>(),
                FixVersions = ticket.FixVersions ?? new List<string>(),
                JiraUrl = $"https://navex.atlassian.net/browse/{ticket.TicketKey}"
            };
        }

        private string TruncateText(string? text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text ?? "";

            return text.Substring(0, maxLength) + "...";
        }

        private class CategoryKeyword
        {
            public string Category { get; set; } = string.Empty;
            public string[] Keywords { get; set; } = Array.Empty<string>();
        }
    }
}
