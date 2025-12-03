using Alt_Support.Models;
using Alt_Support.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alt_Support.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketDataService _ticketDataService;
        private readonly IJiraService _jiraService;
        private readonly GitHubService _githubService;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(
            ITicketDataService ticketDataService, 
            IJiraService jiraService,
            GitHubService githubService,
            ILogger<TicketsController> logger)
        {
            _ticketDataService = ticketDataService;
            _jiraService = jiraService;
            _githubService = githubService;
            _logger = logger;
        }

        /// <summary>
        /// Get all tickets with pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TicketInfo>>> GetTickets(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20,
            [FromQuery] string? projectKey = null,
            [FromQuery] string? search = null,
            [FromQuery] bool includeJira = true)
        {
            try
            {
                var skip = (page - 1) * pageSize;
                List<TicketInfo> tickets;

                if (!string.IsNullOrEmpty(search))
                {
                    if (includeJira)
                    {
                        tickets = await _ticketDataService.SearchTicketsWithJiraAsync(search, skip, pageSize);
                    }
                    else
                    {
                        tickets = await _ticketDataService.SearchTicketsAsync(search, skip, pageSize);
                    }
                }
                else if (!string.IsNullOrEmpty(projectKey))
                {
                    tickets = await _ticketDataService.GetTicketsByProjectAsync(projectKey, skip, pageSize);
                }
                else
                {
                    tickets = await _ticketDataService.GetTicketsAsync(skip, pageSize);
                }

                var totalCount = await _ticketDataService.GetTotalTicketCountAsync();

                Response.Headers["X-Total-Count"] = totalCount.ToString();
                Response.Headers["X-Page"] = page.ToString();
                Response.Headers["X-Page-Size"] = pageSize.ToString();

                return Ok(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tickets");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get a specific ticket by key
        /// </summary>
        [HttpGet("{ticketKey}")]
        public async Task<ActionResult<TicketInfo>> GetTicket(string ticketKey)
        {
            try
            {
                var ticket = await _ticketDataService.GetTicketAsync(ticketKey);
                
                if (ticket == null)
                {
                    return NotFound($"Ticket {ticketKey} not found");
                }

                return Ok(ticket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving ticket {ticketKey}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Refresh ticket data from Jira
        /// </summary>
        [HttpPost("{ticketKey}/refresh")]
        public async Task<ActionResult<TicketInfo>> RefreshTicket(string ticketKey)
        {
            try
            {
                var jiraTicket = await _jiraService.GetTicketAsync(ticketKey);
                
                if (jiraTicket == null)
                {
                    return NotFound($"Ticket {ticketKey} not found in Jira");
                }

                var savedTicket = await _ticketDataService.SaveTicketAsync(jiraTicket);
                return Ok(savedTicket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error refreshing ticket {ticketKey}");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get tickets statistics
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult> GetStatistics()
        {
            try
            {
                var totalTickets = await _ticketDataService.GetTotalTicketCountAsync();
                
                // You can extend this with more statistics as needed
                var stats = new
                {
                    TotalTickets = totalTickets,
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving statistics");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Advanced search that queries both local database and Jira
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<object>> SearchTickets(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool includeJira = true,
            [FromQuery] bool saveJiraResults = false,
            [FromQuery] bool jiraOnly = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Search query is required");
                }

                var skip = (page - 1) * pageSize;
                List<TicketInfo> tickets;

                if (jiraOnly)
                {
                    // Search only Jira, don't include local results
                    tickets = await GetJiraTickets(query, pageSize);
                }
                else if (includeJira)
                {
                    tickets = await _ticketDataService.SearchTicketsWithJiraAsync(query, skip, pageSize);
                    
                    // Optionally save Jira results to local database
                    if (saveJiraResults)
                    {
                        var jiraTickets = tickets.Where(t => t.SimilarityScore < 0).ToList();
                        if (jiraTickets.Any())
                        {
                            foreach (var ticket in jiraTickets)
                            {
                                ticket.SimilarityScore = 0; // Reset score for saving
                            }
                            await _ticketDataService.BulkSaveTicketsAsync(jiraTickets);
                            _logger.LogInformation("Saved {Count} tickets from Jira search to local database", jiraTickets.Count);
                        }
                    }
                }
                else
                {
                    tickets = await _ticketDataService.SearchTicketsAsync(query, skip, pageSize);
                }

                var totalCount = tickets.Count;
                var localCount = jiraOnly ? 0 : tickets.Count(t => t.SimilarityScore >= 0);
                var jiraCount = jiraOnly ? totalCount : tickets.Count(t => t.SimilarityScore < 0);

                var response = new
                {
                    Query = query,
                    Page = page,
                    PageSize = pageSize,
                    TotalResults = totalCount,
                    LocalResults = localCount,
                    JiraResults = jiraCount,
                    IncludedJira = includeJira,
                    JiraOnly = jiraOnly,
                    Tickets = tickets.Select(t => new
                    {
                        t.TicketKey,
                        t.Title,
                        t.Description,
                        t.Status,
                        t.Priority,
                        t.Assignee,
                        t.ProjectKey,
                        t.CreatedDate,
                        t.UpdatedDate,
                        Source = jiraOnly ? "Jira" : (t.SimilarityScore >= 0 ? "Local" : "Jira")
                    })
                };

                Response.Headers["X-Total-Count"] = totalCount.ToString();
                Response.Headers["X-Local-Count"] = localCount.ToString();
                Response.Headers["X-Jira-Count"] = jiraCount.ToString();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing advanced search for query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Search Jira directly using JQL
        /// </summary>
        [HttpGet("search/jira")]
        public async Task<ActionResult<object>> SearchJiraDirectly(
            [FromQuery] string query,
            [FromQuery] int maxResults = 50)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Search query is required");
                }

                var tickets = await GetJiraTickets(query, maxResults);

                var response = new
                {
                    Query = query,
                    MaxResults = maxResults,
                    TotalResults = tickets.Count,
                    Source = "Jira API",
                    Tickets = tickets.Select(t => new
                    {
                        t.TicketKey,
                        t.Title,
                        t.Description,
                        t.Status,
                        t.Priority,
                        t.Assignee,
                        t.Reporter,
                        t.ProjectKey,
                        t.TicketType,
                        t.Labels,
                        t.Components,
                        t.CreatedDate,
                        t.UpdatedDate,
                        t.ResolvedDate,
                        Source = "Jira"
                    })
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Jira directly for query: {Query}", query);
                return StatusCode(500, $"Error searching Jira: {ex.Message}");
            }
        }

        /// <summary>
        /// Test Jira connection and return raw search results
        /// </summary>
        [HttpGet("debug/jira")]
        public async Task<ActionResult<object>> DebugJiraConnection([FromQuery] string? query = "")
        {
            try
            {
                var testQuery = string.IsNullOrEmpty(query) ? "project in (SUPPORT, BUG, STORY) ORDER BY created DESC" : query;
                
                var tickets = await _jiraService.SearchTicketsAsync(testQuery, 10);
                
                return Ok(new
                {
                    JiraQuery = testQuery,
                    ConnectionStatus = "Success",
                    TicketCount = tickets.Count,
                    SampleTickets = tickets.Take(3).Select(t => new
                    {
                        t.TicketKey,
                        t.Title,
                        t.ProjectKey,
                        t.Status,
                        t.Priority
                    })
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    ConnectionStatus = "Failed",
                    Error = ex.Message,
                    JiraConfig = new
                    {
                        BaseUrl = _jiraService.GetType().GetProperty("BaseUrl")?.GetValue(_jiraService)?.ToString() ?? "Unknown",
                        HasApiToken = !string.IsNullOrEmpty(_jiraService.GetType().GetProperty("ApiToken")?.GetValue(_jiraService)?.ToString())
                    }
                });
            }
        }

        /// <summary>
        /// Debug endpoint to test Jira connectivity
        /// </summary>
        [HttpGet("debug/test-jira/{ticketKey}")]
        public async Task<ActionResult<object>> TestJiraConnection(string ticketKey)
        {
            try
            {
                var results = new
                {
                    ticketKey = ticketKey,
                    directFetch = (object?)null,
                    searchFetch = (object?)null,
                    jqlQuery = "",
                    errors = new List<string>()
                };

                // Test direct fetch (like Postman)
                try
                {
                    var directTicket = await _jiraService.GetTicketAsync(ticketKey);
                    results = results with { directFetch = directTicket != null ? new { success = true, ticketKey = directTicket.TicketKey, title = directTicket.Title } : new { success = false, message = "No ticket returned" } };
                }
                catch (Exception ex)
                {
                    ((List<string>)results.errors).Add($"Direct fetch error: {ex.Message}");
                }

                // Test search fetch (like autocomplete)
                try
                {
                    var jqlQuery = BuildJqlQuery(ticketKey);
                    results = results with { jqlQuery = jqlQuery };
                    
                    var searchTickets = await _jiraService.SearchTicketsAsync(jqlQuery, 1);
                    results = results with { searchFetch = new { success = searchTickets.Any(), count = searchTickets.Count, tickets = searchTickets.Select(t => new { t.TicketKey, t.Title }).ToList() } };
                }
                catch (Exception ex)
                {
                    ((List<string>)results.errors).Add($"Search fetch error: {ex.Message}");
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test endpoint for ticket: {TicketKey}", ticketKey);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get autocomplete suggestions for search input
        /// </summary>
        [HttpGet("autocomplete")]
        public async Task<ActionResult<object>> GetAutocompleteSuggestions([FromQuery] string query, [FromQuery] int limit = 5)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Ok(new { suggestions = new List<object>() });
                }

                var suggestions = new List<object>();

                // Check if it's a specific ticket key pattern (e.g., PRODSUP-28864)
                if (System.Text.RegularExpressions.Regex.IsMatch(query.Trim(), @"^[A-Z]+-\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    // Use direct ticket fetch for exact ticket keys (like Postman)
                    var exactTicket = await _jiraService.GetTicketAsync(query.ToUpperInvariant());
                    if (exactTicket != null)
                    {
                        suggestions.Add(new
                        {
                            ticketKey = exactTicket.TicketKey,
                            title = exactTicket.Title,
                            status = exactTicket.Status,
                            priority = exactTicket.Priority,
                            projectKey = exactTicket.ProjectKey,
                            assignee = exactTicket.Assignee,
                            summary = $"{exactTicket.TicketKey} - {exactTicket.Title}",
                            prLinks = exactTicket.PrLinks
                        });
                    }
                }
                else
                {
                    // Use search for other queries
                    var jqlQuery = BuildJqlQuery(query);
                    var tickets = await _jiraService.SearchTicketsAsync(jqlQuery, limit);

                    suggestions.AddRange(tickets.Select(t => new
                    {
                        ticketKey = t.TicketKey,
                        title = t.Title,
                        status = t.Status,
                        priority = t.Priority,
                        projectKey = t.ProjectKey,
                        assignee = t.Assignee,
                        summary = $"{t.TicketKey} - {t.Title}",
                        prLinks = t.PrLinks
                    }));
                }

                return Ok(new { suggestions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting autocomplete suggestions for query: {Query}", query);
                return StatusCode(500, "Error retrieving suggestions");
            }
        }

        /// <summary>
        /// Get detailed ticket information including PR links and custom fields
        /// </summary>
        [HttpGet("details/{ticketKey}")]
        public async Task<ActionResult<object>> GetTicketDetails(string ticketKey)
        {
            try
            {
                
                //    Get ticket from Jira to get the most up-to-date info
                var ticket = await _jiraService.GetTicketAsync(ticketKey);
                
                if (ticket == null)
                {
                    return NotFound($"Ticket {ticketKey} not found");
                }

                // Extract PR links and other useful information
                var response = new
                {
                    ticketKey = ticket.TicketKey,
                    title = ticket.Title,
                    description = ticket.Description,
                    status = ticket.Status,
                    priority = ticket.Priority,
                    assignee = ticket.Assignee,
                    reporter = ticket.Reporter,
                    projectKey = ticket.ProjectKey,
                    ticketType = ticket.TicketType,
                    labels = ticket.Labels,
                    components = ticket.Components,
                    sprint = ticket.Sprint,
                    testCases = ticket.TestCases,
                    epimPriority = ticket.EPIMPriority,
                    deploymentTrainstop = ticket.DeploymentTrainstop,
                    fixVersions = ticket.FixVersions,
                    launchDarklyToggle = ticket.LaunchDarklyToggle,
                    createdDate = ticket.CreatedDate,
                    updatedDate = ticket.UpdatedDate,
                    resolvedDate = ticket.ResolvedDate,
                    
                    // Use PR links already extracted by JiraService (from both description and custom field)
                    prLinks = ticket.PrLinks ?? new List<string>(),
                    
                    // Additional fields that might be useful
                    jiraUrl = $"https://navex.atlassian.net/browse/{ticket.TicketKey}",
                    
                    // Raw fields for debugging
                    rawDescription = ticket.Description
                };
                
                _logger.LogInformation($"Ticket {ticketKey} has {ticket.PrLinks?.Count ?? 0} PR links");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ticket details for: {TicketKey}", ticketKey);
                return StatusCode(500, $"Error retrieving ticket details: {ex.Message}");
            }
        }

        private async Task<List<TicketInfo>> GetJiraTickets(string query, int maxResults)
        {
            // Build JQL query based on search term
            var jqlQuery = BuildJqlQuery(query);
            
            // Search Jira directly
            var jiraTickets = await _jiraService.SearchTicketsAsync(jqlQuery, maxResults);
            
            return jiraTickets;
        }

        private string BuildJqlQuery(string searchTerm)
        {
            var normalizedTerm = searchTerm.ToLowerInvariant().Trim();
            
            // Check if it's a specific ticket key pattern (e.g., PROJ-123)
            if (System.Text.RegularExpressions.Regex.IsMatch(normalizedTerm, @"^[A-Z]+-\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return $"key = {normalizedTerm.ToUpperInvariant()}";
            }
            
            // Check for field-specific searches (priority:high, status:open, etc.)
            if (normalizedTerm.Contains(":"))
            {
                var parts = normalizedTerm.Split(':', 2);
                var field = parts[0].Trim();
                var value = parts[1].Trim().Trim('"');
                
                return field.ToLowerInvariant() switch
                {
                    "priority" => $"priority = \"{value}\"",
                    "status" => $"status = \"{value}\"",
                    "assignee" => $"assignee = \"{value}\"",
                    "reporter" => $"reporter = \"{value}\"",
                    "project" => $"project = \"{value}\"",
                    "type" => $"issuetype = \"{value}\"",
                    "component" => $"component = \"{value}\"",
                    "label" => $"labels = \"{value}\"",
                    "created" => value.ToLowerInvariant() switch
                    {
                        "today" => "created >= startOfDay()",
                        "yesterday" => "created >= startOfDay(-1d) AND created < startOfDay()",
                        "week" => "created >= startOfWeek()",
                        "month" => "created >= startOfMonth()",
                        _ => $"created >= \"{value}\""
                    },
                    "updated" => value.ToLowerInvariant() switch
                    {
                        "today" => "updated >= startOfDay()",
                        "yesterday" => "updated >= startOfDay(-1d) AND updated < startOfDay()",
                        "week" => "updated >= startOfWeek()",
                        "month" => "updated >= startOfMonth()",
                        _ => $"updated >= \"{value}\""
                    },
                    _ => $"text ~ \"{searchTerm}*\" ORDER BY updated DESC" // Fallback to text search with wildcard
                };
            }
            
            // Default: search in summary and description with wildcard and order by updated
            // Escape special characters in search term
            var escapedTerm = searchTerm.Replace("\"", "\\\"");
            return $"text ~ \"{escapedTerm}*\" ORDER BY updated DESC";
        }

        private List<string> ExtractPRLinks(string? description)
        {
            if (string.IsNullOrEmpty(description))
                return new List<string>();

            var prLinks = new List<string>();
            
            // Regular expressions to match various PR link formats
            var patterns = new[]
            {
                @"https://github\.com/[^/]+/[^/]+/pull/\d+",
                @"https://github\.com/[^/]+/[^/]+/pr/\d+",
                @"github\.com/[^/]+/[^/]+/pull/\d+",
                @"github\.com/[^/]+/[^/]+/pr/\d+"
            };

            foreach (var pattern in patterns)
            {
                var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var matches = regex.Matches(description);
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var link = match.Value;
                    // Ensure it starts with https://
                    if (!link.StartsWith("https://"))
                    {
                        link = "https://" + link;
                    }
                    
                    if (!prLinks.Contains(link))
                    {
                        prLinks.Add(link);
                    }
                }
            }

            return prLinks;
        }

        /// <summary>
        /// Get PR details including file changes
        /// </summary>
        [HttpGet("pr-details")]
        public async Task<ActionResult<object>> GetPRDetails([FromQuery] string prUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(prUrl))
                {
                    return BadRequest("PR URL is required");
                }

                _logger.LogInformation($"Fetching PR details for: {prUrl}");

                var prDetails = await _githubService.GetPRDetailsAsync(prUrl);
                
                if (prDetails == null)
                {
                    return NotFound("PR not found or unable to fetch details");
                }

                return Ok(prDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching PR details for {prUrl}");
                return StatusCode(500, new { error = "Failed to fetch PR details", message = ex.Message });
            }
        }
    }
}