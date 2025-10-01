using Alt_Support.Models;
using Alt_Support.Configuration;
using Alt_Support.Converters;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Alt_Support.Services
{
    public interface IJiraService
    {
        Task<TicketInfo?> GetTicketAsync(string ticketKey);
        Task<List<TicketInfo>> GetProjectTicketsAsync(string projectKey, int maxResults = 100);
        Task<bool> AddCommentToTicketAsync(string ticketKey, string comment);
        Task<bool> UpdateTicketCustomFieldAsync(string ticketKey, string fieldId, object value);
        Task<List<TicketInfo>> SearchTicketsAsync(string jqlQuery, int maxResults = 100);
    }

    public class JiraService : IJiraService
    {
        private readonly HttpClient _httpClient;
        private readonly JiraConfiguration _config;
        private readonly ILogger<JiraService> _logger;

        public JiraService(HttpClient httpClient, IOptions<ApplicationConfiguration> config, ILogger<JiraService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value.Jira;
            _logger = logger;

            // Configure HTTP client
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.Username}:{_config.ApiToken}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<TicketInfo?> GetTicketAsync(string ticketKey)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/rest/api/3/issue/{ticketKey}?expand=changelog&fields=*all");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get ticket {ticketKey}: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                options.Converters.Add(new JiraDateTimeConverter());
                options.Converters.Add(new JiraNullableDateTimeConverter());
                
                var jiraIssue = JsonSerializer.Deserialize<JiraIssueResponse>(content, options);

                return ConvertToTicketInfo(jiraIssue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ticket {ticketKey}");
                return null;
            }
        }

        public async Task<List<TicketInfo>> GetProjectTicketsAsync(string projectKey, int maxResults = 100)
        {
            try
            {
                var jql = $"project = {projectKey} ORDER BY created DESC";
                return await SearchTicketsAsync(jql, maxResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting tickets for project {projectKey}");
                return new List<TicketInfo>();
            }
        }

        public async Task<List<TicketInfo>> SearchTicketsAsync(string jqlQuery, int maxResults = 100)
        {
            try
            {
                var requestBody = new
                {
                    jql = jqlQuery,
                    maxResults = maxResults,
                    fields = new[] { 
                        "summary", "description", "issuetype", "status", "priority", 
                        "assignee", "reporter", "project", "labels", "components", 
                        "created", "updated", "resolutiondate", "resolution", "customfield_10144"
                    },
                    expand = new[] { "changelog" }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/rest/api/3/search", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to search tickets: {response.StatusCode}");
                    return new List<TicketInfo>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                options.Converters.Add(new JiraDateTimeConverter());
                options.Converters.Add(new JiraNullableDateTimeConverter());
                
                var searchResult = JsonSerializer.Deserialize<JiraSearchResponse>(responseContent, options);

                var tickets = new List<TicketInfo>();
                if (searchResult?.Issues != null)
                {
                    foreach (var issue in searchResult.Issues)
                    {
                        var ticket = ConvertToTicketInfo(issue);
                        if (ticket != null)
                        {
                            tickets.Add(ticket);
                        }
                    }
                }

                return tickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching tickets with JQL: {jqlQuery}");
                return new List<TicketInfo>();
            }
        }

        public async Task<bool> AddCommentToTicketAsync(string ticketKey, string comment)
        {
            try
            {
                var requestBody = new
                {
                    body = new
                    {
                        type = "doc",
                        version = 1,
                        content = new[]
                        {
                            new
                            {
                                type = "paragraph",
                                content = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = comment
                                    }
                                }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/rest/api/3/issue/{ticketKey}/comment", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully added comment to ticket {ticketKey}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to add comment to ticket {ticketKey}: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding comment to ticket {ticketKey}");
                return false;
            }
        }

        public async Task<bool> UpdateTicketCustomFieldAsync(string ticketKey, string fieldId, object value)
        {
            try
            {
                var requestBody = new
                {
                    fields = new Dictionary<string, object>
                    {
                        [fieldId] = value
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"/rest/api/3/issue/{ticketKey}", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully updated custom field {fieldId} for ticket {ticketKey}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to update custom field {fieldId} for ticket {ticketKey}: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating custom field {fieldId} for ticket {ticketKey}");
                return false;
            }
        }

        private TicketInfo? ConvertToTicketInfo(JiraIssueResponse? jiraIssue)
        {
            if (jiraIssue == null) return null;

            var ticket = new TicketInfo
            {
                TicketKey = jiraIssue.Key,
                Title = jiraIssue.Fields?.Summary ?? "",
                Description = ExtractTextFromDescription(jiraIssue.Fields?.Description),
                TicketType = jiraIssue.Fields?.Issuetype?.Name ?? "",
                Status = jiraIssue.Fields?.Status?.Name ?? "",
                Priority = jiraIssue.Fields?.Priority?.Name ?? "",
                Assignee = jiraIssue.Fields?.Assignee?.DisplayName ?? "",
                Reporter = jiraIssue.Fields?.Reporter?.DisplayName ?? "",
                ProjectKey = jiraIssue.Fields?.Project?.Key ?? "",
                Labels = jiraIssue.Fields?.Labels ?? new List<string>(),
                Components = jiraIssue.Fields?.Components?.Select(c => c.Name).ToList() ?? new List<string>(),
                CreatedDate = jiraIssue.Fields?.Created ?? DateTime.MinValue,
                UpdatedDate = jiraIssue.Fields?.Updated,
                ResolvedDate = jiraIssue.Fields?.Resolutiondate,
                Resolution = jiraIssue.Fields?.Resolution?.Name ?? ""
            };

            // Extract file paths from description and comments
            ticket.AffectedFiles = ExtractFilePathsFromText(ticket.Description);
            
            // Extract PR links from both description and custom field
            ticket.PrLinks = GetAllPRLinks(ticket.Description, jiraIssue.Fields?.PRLinksField);

            return ticket;
        }

        private string ExtractTextFromDescription(DescriptionField? description)
        {
            if (description?.Content == null) return "";

            var textBuilder = new StringBuilder();
            ExtractTextFromContent(description.Content, textBuilder);
            return textBuilder.ToString();
        }

        private void ExtractTextFromContent(List<ContentItem> content, StringBuilder textBuilder)
        {
            foreach (var item in content)
            {
                if (!string.IsNullOrEmpty(item.Text))
                {
                    textBuilder.Append(item.Text).Append(" ");
                }

                if (item.Content != null)
                {
                    ExtractTextFromContent(item.Content, textBuilder);
                }
            }
        }

        private List<string> ExtractFilePathsFromText(string text)
        {
            var filePaths = new List<string>();
            
            // Simple regex patterns to match common file path patterns
            var patterns = new[]
            {
                @"[\w\\\/]+\.\w+", // Basic file extension pattern
                @"src[\\\/][\w\\\/]+\.\w+", // Source file pattern
                @"[\w\\\/]*\.cs", // C# files
                @"[\w\\\/]*\.js", // JavaScript files
                @"[\w\\\/]*\.ts", // TypeScript files
                @"[\w\\\/]*\.java", // Java files
                @"[\w\\\/]*\.py", // Python files
                @"[\w\\\/]*\.sql", // SQL files
                @"[\w\\\/]*\.xml", // XML files
                @"[\w\\\/]*\.json" // JSON files
            };

            foreach (var pattern in patterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var filePath = match.Value.Trim();
                    if (!string.IsNullOrEmpty(filePath) && !filePaths.Contains(filePath))
                    {
                        filePaths.Add(filePath);
                    }
                }
            }

            return filePaths;
        }

        private List<string> ExtractPRLinks(string? description)
        {
            if (string.IsNullOrEmpty(description))
                return new List<string>();

            var prLinks = new List<string>();
            
            // Regular expressions to match various PR link formats
            var patterns = new[]
            {
                @"https://github\.com/[^/\s]+/[^/\s]+/pull/\d+",
                @"https://github\.com/[^/\s]+/[^/\s]+/pr/\d+",
                @"github\.com/[^/\s]+/[^/\s]+/pull/\d+",
                @"github\.com/[^/\s]+/[^/\s]+/pr/\d+"
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

        private List<string> ExtractPRLinksFromCustomField(CustomField10144? customField)
        {
            var prLinks = new List<string>();
            
            if (customField?.Content == null)
                return prLinks;

            foreach (var contentBlock in customField.Content)
            {
                if (contentBlock.Content == null) continue;

                foreach (var contentItem in contentBlock.Content)
                {
                    if (contentItem.Marks == null) continue;

                    foreach (var mark in contentItem.Marks)
                    {
                        if (mark.Type == "link" && mark.Attrs?.Href != null)
                        {
                            var href = mark.Attrs.Href;
                            // Check if it's a GitHub PR link
                            if (href.Contains("github.com") && (href.Contains("/pull/") || href.Contains("/pr/")))
                            {
                                if (!prLinks.Contains(href))
                                {
                                    prLinks.Add(href);
                                }
                            }
                        }
                    }
                }
            }

            return prLinks;
        }

        private List<string> GetAllPRLinks(string? description, CustomField10144? customField)
        {
            var allPRLinks = new List<string>();
            
            // Get PR links from description
            var descriptionLinks = ExtractPRLinks(description);
            allPRLinks.AddRange(descriptionLinks);
            
            // Get PR links from custom field
            var customFieldLinks = ExtractPRLinksFromCustomField(customField);
            foreach (var link in customFieldLinks)
            {
                if (!allPRLinks.Contains(link))
                {
                    allPRLinks.Add(link);
                }
            }
            
            return allPRLinks;
        }
    }

    // Jira API Response Models
    public class JiraIssueResponse
    {
        public string Key { get; set; } = string.Empty;
        public TicketFields? Fields { get; set; }
    }

    public class JiraSearchResponse
    {
        public List<JiraIssueResponse>? Issues { get; set; }
        public int Total { get; set; }
    }
}