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
                var response = await _httpClient.GetAsync($"/rest/api/3/issue/{ticketKey}?expand=changelog,renderedFields&fields=*all");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get ticket {ticketKey}: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                // Log raw JSON for debugging (first 2000 chars)
                _logger.LogInformation($"Raw Jira response for {ticketKey}: {content.Substring(0, Math.Min(2000, content.Length))}...");
                
                var options = new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                options.Converters.Add(new JiraDateTimeConverter());
                options.Converters.Add(new JiraNullableDateTimeConverter());
                
                var jiraIssue = JsonSerializer.Deserialize<JiraIssueResponse>(content, options);
                
                // Log what fields we have
                _logger.LogInformation($"Ticket {ticketKey} - Has Description: {jiraIssue?.Fields?.Description != null}, Has CustomField10144: {jiraIssue?.Fields?.PRLinksField != null}");

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
                _logger.LogInformation($"Executing JQL query: {jqlQuery}");
                
                var requestBody = new
                {
                    jql = jqlQuery,
                    maxResults = maxResults,
                    fields = new[] { 
                        "summary", "description", "issuetype", "status", "priority", 
                        "assignee", "reporter", "project", "labels", "components", 
                        "created", "updated", "resolutiondate", "resolution", "customfield_10144",
                        "customfield_10146", "customfield_10020", "customfield_10074", "customfield_10252",
                        "fixVersions", "customfield_10304"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/rest/api/3/search/jql", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Failed to search tickets with JQL '{jqlQuery}': {response.StatusCode} - {errorContent}");
                    
                    // If we get a 410 or 400, the JQL might be invalid, try a simpler query
                    if (response.StatusCode == System.Net.HttpStatusCode.Gone || 
                        response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        _logger.LogInformation("Attempting fallback search with simpler JQL");
                        // Try a simpler text search without special operators
                        var simpleJql = ExtractSimpleSearchTerm(jqlQuery);
                        if (simpleJql != jqlQuery)
                        {
                            return await SearchTicketsWithSimpleJql(simpleJql, maxResults);
                        }
                    }
                    
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

                _logger.LogInformation($"Found {tickets.Count} tickets for query: {jqlQuery}");
                return tickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching tickets with JQL: {jqlQuery}");
                return new List<TicketInfo>();
            }
        }

        private string ExtractSimpleSearchTerm(string jqlQuery)
        {
            // Extract the search term from complex JQL and create a simpler query
            var match = System.Text.RegularExpressions.Regex.Match(jqlQuery, @"~\s*""([^""]+)""");
            if (match.Success)
            {
                var searchTerm = match.Groups[1].Value.TrimEnd('*');
                return $"summary ~ \"{searchTerm}*\" OR description ~ \"{searchTerm}*\" ORDER BY updated DESC";
            }
            return jqlQuery;
        }

        private async Task<List<TicketInfo>> SearchTicketsWithSimpleJql(string jqlQuery, int maxResults)
        {
            try
            {
                _logger.LogInformation($"Executing fallback JQL query: {jqlQuery}");
                
                var requestBody = new
                {
                    jql = jqlQuery,
                    maxResults = maxResults,
                    fields = new[] { 
                        "summary", "description", "issuetype", "status", "priority", 
                        "assignee", "reporter", "project", "labels", "components", 
                        "created", "updated", "resolutiondate", "resolution", "customfield_10144",
                        "customfield_10146", "customfield_10020", "customfield_10074", "customfield_10252",
                        "fixVersions", "customfield_10304"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/rest/api/3/search/jql", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Fallback search also failed: {response.StatusCode} - {errorContent}");
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

                _logger.LogInformation($"Fallback search found {tickets.Count} tickets");
                return tickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in fallback search with JQL: {jqlQuery}");
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
                Description = jiraIssue.Fields?.Description != null ? ExtractFormattedTextFromDescription(jiraIssue.Fields.Description) : "",
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
            
            // Extract PR links from both description field (rich text) and custom field
            ticket.PrLinks = GetAllPRLinks(jiraIssue.Fields?.Description, jiraIssue.Fields?.PRLinksField);
            
            // Extract sprint information
            ticket.Sprint = ExtractSprintName(jiraIssue.Fields?.Sprint);
            
            // Extract test cases from custom field 10146
            ticket.TestCases = ExtractFormattedTextFromCustomField10146(jiraIssue.Fields?.TestCasesField);
            
            // Extract additional custom fields
            ticket.EPIMPriority = jiraIssue.Fields?.EPIMPriority?.Value ?? "";
            // TODO: Deployment Trainstop field ID is unknown - need to find the correct custom field
            ticket.DeploymentTrainstop = ""; // Placeholder until we find the correct field
            ticket.FixVersions = jiraIssue.Fields?.FixVersions?.Select(v => v.Name).ToList() ?? new List<string>();
            ticket.LaunchDarklyToggle = jiraIssue.Fields?.LaunchDarklyToggle != null ? string.Join(", ", jiraIssue.Fields.LaunchDarklyToggle) : "";
            
            _logger.LogInformation($"Ticket {ticket.TicketKey}: Found {ticket.PrLinks.Count} PR links, Sprint: {ticket.Sprint}, EPIM Priority: {ticket.EPIMPriority}, Deployment Trainstop: {ticket.DeploymentTrainstop}, Launch Darkly: {ticket.LaunchDarklyToggle}, Fix Versions: {string.Join(", ", ticket.FixVersions)}");

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
            {
                _logger.LogInformation("CustomField10144 is null or has no content");
                return prLinks;
            }

            _logger.LogInformation($"CustomField10144 has {customField.Content.Count} content blocks");

            foreach (var contentBlock in customField.Content)
            {
                _logger.LogInformation($"Content block - Type: {contentBlock.Type}, HasContent: {contentBlock.Content != null}, HasAttrs: {contentBlock.Attrs != null}, Url: {contentBlock.Attrs?.Url}");
                
                // Handle blockCard type (embedded PR links at the block level)
                if (contentBlock.Type == "blockCard")
                {
                    _logger.LogInformation($"BlockCard found - Has Attrs: {contentBlock.Attrs != null}, Url value: {contentBlock.Attrs?.Url ?? "NULL"}");
                    
                    if (contentBlock.Attrs?.Url != null)
                    {
                        var url = contentBlock.Attrs.Url;
                        _logger.LogInformation($"Found blockCard URL in custom field: {url}");

                        // Check if it's a GitHub PR link
                        if (url.Contains("github.com") && (url.Contains("/pull/") || url.Contains("/pr/")))
                        {
                            if (!prLinks.Contains(url))
                            {
                                prLinks.Add(url);
                                _logger.LogInformation($"Added PR link from blockCard in custom field: {url}");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("BlockCard has no URL in Attrs!");
                    }
                    continue;
                }
                
                if (contentBlock.Content == null) continue;

                _logger.LogInformation($"Content block has {contentBlock.Content.Count} items");

                foreach (var contentItem in contentBlock.Content)
                {
                    _logger.LogInformation($"Content item - Type: {contentItem.Type}, HasMarks: {contentItem.Marks != null}, HasText: {!string.IsNullOrEmpty(contentItem.Text)}");
                    
                    // Handle inlineCard type (embedded links)
                    if (contentItem.Type == "inlineCard" && contentItem.Attrs?.Url != null)
                    {
                        var url = contentItem.Attrs.Url;
                        _logger.LogInformation($"Found inlineCard URL in custom field: {url}");

                        // Check if it's a GitHub PR link
                        if (url.Contains("github.com") && (url.Contains("/pull/") || url.Contains("/pr/")))
                        {
                            if (!prLinks.Contains(url))
                            {
                                prLinks.Add(url);
                                _logger.LogInformation($"Added PR link from inlineCard in custom field: {url}");
                            }
                        }
                        continue;
                    }

                    if (contentItem.Marks == null) continue;

                    _logger.LogInformation($"Content item has {contentItem.Marks.Count} marks");

                    foreach (var mark in contentItem.Marks)
                    {
                        _logger.LogInformation($"Mark - Type: {mark.Type}, Href: {mark.Attrs?.Href}");
                        
                        if (mark.Type == "link" && mark.Attrs?.Href != null)
                        {
                            var href = mark.Attrs.Href;
                            _logger.LogInformation($"Found link in custom field: {href}");
                            
                            // Check if it's a GitHub PR link
                            if (href.Contains("github.com") && (href.Contains("/pull/") || href.Contains("/pr/")))
                            {
                                if (!prLinks.Contains(href))
                                {
                                    prLinks.Add(href);
                                    _logger.LogInformation($"Added PR link from custom field: {href}");
                                }
                            }
                        }
                    }
                }
            }

            return prLinks;
        }

        private List<string> GetAllPRLinks(DescriptionField? description, CustomField10144? customField)
        {
            var allPRLinks = new List<string>();
            
            // Get PR links from description (extract from rich text)
            var descriptionLinks = ExtractPRLinksFromDescription(description);
            _logger.LogInformation($"Found {descriptionLinks.Count} PR links from description");
            allPRLinks.AddRange(descriptionLinks);
            
            // Get PR links from custom field
            var customFieldLinks = ExtractPRLinksFromCustomField(customField);
            _logger.LogInformation($"Found {customFieldLinks.Count} PR links from custom field");
            foreach (var link in customFieldLinks)
            {
                if (!allPRLinks.Contains(link))
                {
                    allPRLinks.Add(link);
                }
            }
            
            return allPRLinks;
        }
        
        private List<string> ExtractPRLinksFromDescription(DescriptionField? description)
        {
            var prLinks = new List<string>();
            
            if (description?.Content == null)
                return prLinks;

            // Recursively search through content for links
            ExtractLinksFromContent(description.Content, prLinks);
            
            return prLinks;
        }
        
        private void ExtractLinksFromContent(List<ContentItem> content, List<string> prLinks)
        {
            _logger.LogInformation($"ExtractLinksFromContent: Processing {content.Count} content items");
            
            foreach (var item in content)
            {
                _logger.LogInformation($"Content item - Type: {item.Type}, HasMarks: {item.Marks != null}, HasText: {!string.IsNullOrEmpty(item.Text)}, HasContent: {item.Content != null}");
                
                // Check if this item has marks (which contain links)
                if (item.Marks != null)
                {
                    _logger.LogInformation($"Item has {item.Marks.Count} marks");
                    foreach (var mark in item.Marks)
                    {
                        _logger.LogInformation($"Mark - Type: {mark.Type}, Href: {mark.Attrs?.Href}");
                        if (mark.Type == "link" && mark.Attrs?.Href != null)
                        {
                            var href = mark.Attrs.Href;
                            _logger.LogInformation($"Found link: {href}");
                            // Check if it's a GitHub PR link
                            if (href.Contains("github.com") && (href.Contains("/pull/") || href.Contains("/pr/")))
                            {
                                if (!prLinks.Contains(href))
                                {
                                    prLinks.Add(href);
                                    _logger.LogInformation($"Found PR link in description: {href}");
                                }
                            }
                        }
                    }
                }
                
                // Check the text for plain PR links (not hyperlinked)
                if (!string.IsNullOrEmpty(item.Text))
                {
                    _logger.LogInformation($"Checking text for PR links: {item.Text.Substring(0, Math.Min(100, item.Text.Length))}");
                    var textLinks = ExtractPRLinks(item.Text);
                    if (textLinks.Count > 0)
                    {
                        _logger.LogInformation($"Found {textLinks.Count} PR links in text");
                    }
                    foreach (var link in textLinks)
                    {
                        if (!prLinks.Contains(link))
                        {
                            prLinks.Add(link);
                            _logger.LogInformation($"Found PR link in text: {link}");
                        }
                    }
                }
                
                // Recursively process nested content
                if (item.Content != null)
                {
                    _logger.LogInformation($"Item has nested content with {item.Content.Count} items");
                    ExtractLinksFromContent(item.Content, prLinks);
                }
            }
        }
        
        private string ExtractSprintName(List<SprintInfo>? sprints)
        {
            if (sprints == null || sprints.Count == 0)
                return "";
            
            // Get the most recent active or closed sprint
            var activeSprint = sprints.FirstOrDefault(s => s.State == "active");
            if (activeSprint != null)
                return activeSprint.Name;
            
            // If no active sprint, get the most recent closed sprint
            var closedSprint = sprints.FirstOrDefault(s => s.State == "closed");
            if (closedSprint != null)
                return closedSprint.Name;
            
            // Otherwise, return the first sprint
            return sprints.First().Name;
        }

        private string ExtractFormattedTextFromCustomField10146(CustomField10146? testCasesField)
        {
            if (testCasesField?.Content == null)
            {
                return "";
            }

            var textBuilder = new StringBuilder();
            ExtractFormattedTextFromContent(testCasesField.Content, textBuilder, 0);

            return textBuilder.ToString().Trim();
        }
        
        private string ExtractFormattedTextFromDescription(DescriptionField description)
        {
            var textBuilder = new StringBuilder();
            ExtractFormattedTextFromContent(description.Content, textBuilder, 0);
            return textBuilder.ToString().Trim();
        }
        
        private void ExtractFormattedTextFromContent(List<ContentItem>? content, StringBuilder textBuilder, int listItemCounter)
        {
            if (content == null) return;
            
            int currentListNumber = 1;
            
            foreach (var item in content)
            {
                switch (item.Type?.ToLower())
                {
                    case "paragraph":
                        // Process paragraph content
                        if (item.Content != null)
                        {
                            ExtractFormattedTextFromContent(item.Content, textBuilder, 0);
                        }
                        // Add single line break after paragraph (hardBreaks within will add more)
                        textBuilder.AppendLine();
                        break;
                        
                    case "text":
                        // Add the text content
                        if (!string.IsNullOrEmpty(item.Text))
                        {
                            // Check if this text has link marks
                            if (item.Marks != null && item.Marks.Count > 0)
                            {
                                foreach (var mark in item.Marks)
                                {
                                    if (mark.Type == "link" && mark.Attrs?.Href != null)
                                    {
                                        // Format as [text](url) for frontend to convert to clickable link
                                        textBuilder.Append($"[{item.Text}]({mark.Attrs.Href})");
                                        break; // Only process first link
                                    }
                                }
                                // If no link mark was found, just append the text
                                if (!item.Marks.Any(m => m.Type == "link"))
                                {
                                    textBuilder.Append(item.Text);
                                }
                            }
                            else
                            {
                                textBuilder.Append(item.Text);
                            }
                        }
                        else
                        {
                            // Handle case where text is empty but there might be a link mark
                            if (item.Marks != null && item.Marks.Count > 0)
                            {
                                foreach (var mark in item.Marks)
                                {
                                    if (mark.Type == "link" && mark.Attrs?.Href != null)
                                    {
                                        textBuilder.Append($"[link]({mark.Attrs.Href})");
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                        
                    case "hardbreak":
                        // Add a line break
                        textBuilder.AppendLine();
                        break;
                        
                    case "orderedlist":
                        // Process ordered list content with numbering
                        if (item.Content != null)
                        {
                            ExtractFormattedTextFromContent(item.Content, textBuilder, 1);
                        }
                        textBuilder.AppendLine();
                        break;
                        
                    case "bulletlist":
                        // Process bullet list content
                        if (item.Content != null)
                        {
                            ExtractFormattedTextFromContent(item.Content, textBuilder, -1);
                        }
                        textBuilder.AppendLine();
                        break;
                        
                    case "listitem":
                        // Add list item marker (numbered or bullet)
                        if (listItemCounter > 0)
                        {
                            // Ordered list
                            textBuilder.Append($"{currentListNumber}. ");
                            currentListNumber++;
                        }
                        else if (listItemCounter < 0)
                        {
                            // Bullet list
                            textBuilder.Append("• ");
                        }
                        
                        if (item.Content != null)
                        {
                            ExtractFormattedTextFromContent(item.Content, textBuilder, 0);
                        }
                        break;
                        
                    case "heading":
                        // Add heading with single line break
                        if (item.Content != null)
                        {
                            ExtractFormattedTextFromContent(item.Content, textBuilder, 0);
                        }
                        textBuilder.AppendLine();
                        break;
                        
                    case "emoji":
                        // Skip emojis or add a placeholder
                        textBuilder.Append(" ");
                        break;
                    case "inlinecard":
                        // Handle inline card links (commonly used for embedded URLs)
                    if (item.Attrs?.Url != null)
                    {
                        var url = item.Attrs.Url;
                        // Extract a meaningful reference text based on URL type
                        string linkText = "Link";

                        if (url.Contains("atlassian.net/browse/"))
                        {
                            // Extract ticket key (e.g., "PRODSUP-12464")
                            linkText = url.Substring(url.LastIndexOf('/') + 1);
                        }
                        else if (url.Contains("github.com") && url.Contains("/pull/"))
                        {
                            // Extract PR number and format as "PR #8086"
                            var prNumber = url.Substring(url.LastIndexOf('/') + 1);
                            linkText = $"PR #{prNumber}";
                        }
                        else if (url.Contains("github.com") && url.Contains("/pr/"))
                        {
                            // Extract PR number and format as "PR #8086"
                            var prNumber = url.Substring(url.LastIndexOf('/') + 1);
                            linkText = $"PR #{prNumber}";
                        }
                        else if (url.Contains("figma.com"))
                        {
                            // For Figma links
                            linkText = "Figma";
                        }
                        else if (url.Contains("confluence"))
                        {
                            // For Confluence links
                            linkText = "Confluence";
                        }
                        else if (url.Contains("sharepoint") || url.Contains(".sharepoint.com"))
                        {
                            // For SharePoint links
                            linkText = "SharePoint";
                        }

                        textBuilder.Append($"[{linkText}]({url})");
                    }
                    break; 

                    default:
                        // For other types, recursively process nested content
                        if (item.Content != null)
                        {
                            ExtractFormattedTextFromContent(item.Content, textBuilder, 0);
                        }
                        break;
                }
            }
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