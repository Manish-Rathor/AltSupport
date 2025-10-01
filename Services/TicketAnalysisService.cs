using Alt_Support.Models;
using Alt_Support.Services;
using Alt_Support.Configuration;
using Microsoft.Extensions.Options;

namespace Alt_Support.Services
{
    public interface ITicketAnalysisService
    {
        Task<TicketAnalysisResponse> AnalyzeNewTicketAsync(TicketAnalysisRequest request);
        Task ProcessNewTicketWebhookAsync(JiraWebhookRequest webhookRequest);
        Task SyncHistoricalDataAsync();
    }

    public class TicketAnalysisService : ITicketAnalysisService
    {
        private readonly IJiraService _jiraService;
        private readonly ITicketDataService _ticketDataService;
        private readonly ISimilarityService _similarityService;
        private readonly ApplicationConfiguration _config;
        private readonly ILogger<TicketAnalysisService> _logger;

        public TicketAnalysisService(
            IJiraService jiraService,
            ITicketDataService ticketDataService,
            ISimilarityService similarityService,
            IOptions<ApplicationConfiguration> config,
            ILogger<TicketAnalysisService> logger)
        {
            _jiraService = jiraService;
            _ticketDataService = ticketDataService;
            _similarityService = similarityService;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<TicketAnalysisResponse> AnalyzeNewTicketAsync(TicketAnalysisRequest request)
        {
            try
            {
                _logger.LogInformation($"Starting analysis for ticket with title: {request.Title}");

                // Get historical tickets from database
                var historicalTickets = await GetRelevantHistoricalTicketsAsync(request.ProjectKey);
                
                if (!historicalTickets.Any())
                {
                    _logger.LogWarning("No historical tickets found for analysis");
                    return new TicketAnalysisResponse
                    {
                        Success = true,
                        Message = "No historical tickets available for comparison",
                        SimilarTickets = new List<SimilarityResult>(),
                        TotalMatches = 0
                    };
                }

                // Find similar tickets
                var similarTickets = _similarityService.FindSimilarTickets(request, historicalTickets);

                _logger.LogInformation($"Found {similarTickets.Count} similar tickets");

                return new TicketAnalysisResponse
                {
                    Success = true,
                    Message = $"Analysis completed. Found {similarTickets.Count} similar tickets.",
                    SimilarTickets = similarTickets,
                    TotalMatches = similarTickets.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing new ticket");
                return new TicketAnalysisResponse
                {
                    Success = false,
                    Message = $"Error during analysis: {ex.Message}",
                    SimilarTickets = new List<SimilarityResult>(),
                    TotalMatches = 0
                };
            }
        }

        public async Task ProcessNewTicketWebhookAsync(JiraWebhookRequest webhookRequest)
        {
            try
            {
                _logger.LogInformation($"Processing webhook for event: {webhookRequest.WebhookEvent}");

                if (webhookRequest.WebhookEvent != "jira:issue_created")
                {
                    _logger.LogDebug($"Ignoring webhook event: {webhookRequest.WebhookEvent}");
                    return;
                }

                var ticketKey = webhookRequest.Issue.Key;
                _logger.LogInformation($"Processing new ticket: {ticketKey}");

                // Get full ticket details from Jira
                var ticketDetails = await _jiraService.GetTicketAsync(ticketKey);
                if (ticketDetails == null)
                {
                    _logger.LogWarning($"Could not retrieve ticket details for {ticketKey}");
                    return;
                }

                // Save ticket to database
                await _ticketDataService.SaveTicketAsync(ticketDetails);

                // Analyze for similar tickets
                var analysisRequest = new TicketAnalysisRequest
                {
                    Title = ticketDetails.Title,
                    Description = ticketDetails.Description,
                    AffectedFiles = ticketDetails.AffectedFiles,
                    PullRequestUrl = ticketDetails.PullRequestUrl,
                    ProjectKey = ticketDetails.ProjectKey,
                    MinimumSimilarityThreshold = _config.Similarity.MinimumSimilarityThreshold,
                    MaxResults = _config.Similarity.MaxSimilarTickets
                };

                var analysisResult = await AnalyzeNewTicketAsync(analysisRequest);

                if (analysisResult.Success && analysisResult.SimilarTickets.Any())
                {
                    // Update the ticket with related ticket information
                    var relatedTicketKeys = analysisResult.SimilarTickets.Select(st => st.TicketKey).ToList();
                    await _ticketDataService.UpdateTicketRelatedTicketsAsync(ticketKey, relatedTicketKeys);

                    // Create comment with similar tickets information
                    var comment = GenerateSimilarTicketsComment(analysisResult.SimilarTickets);
                    await _jiraService.AddCommentToTicketAsync(ticketKey, comment);

                    _logger.LogInformation($"Added similar tickets comment to {ticketKey}");
                }
                else
                {
                    _logger.LogInformation($"No similar tickets found for {ticketKey}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing webhook for ticket {webhookRequest.Issue?.Key}");
            }
        }

        public async Task SyncHistoricalDataAsync()
        {
            try
            {
                _logger.LogInformation("Starting historical data sync");

                foreach (var projectKey in _config.Jira.TargetProjects)
                {
                    _logger.LogInformation($"Syncing project: {projectKey}");

                    var tickets = await _jiraService.GetProjectTicketsAsync(projectKey, _config.Jira.MaxHistoricalTickets);
                    
                    if (tickets.Any())
                    {
                        await _ticketDataService.BulkSaveTicketsAsync(tickets);
                        _logger.LogInformation($"Synced {tickets.Count} tickets for project {projectKey}");
                    }
                }

                _logger.LogInformation("Historical data sync completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing historical data");
            }
        }

        private async Task<List<TicketInfo>> GetRelevantHistoricalTicketsAsync(string projectKey)
        {
            // Get tickets from the same project or all projects if no specific project
            if (!string.IsNullOrEmpty(projectKey))
            {
                return await _ticketDataService.GetTicketsByProjectAsync(projectKey, 0, 1000);
            }
            else
            {
                return await _ticketDataService.GetTicketsAsync(0, 1000);
            }
        }

        private string GenerateSimilarTicketsComment(List<SimilarityResult> similarTickets)
        {
            var comment = "?? **Similar Tickets Found**\n\n";
            comment += "The following tickets might be related to this issue:\n\n";

            foreach (var ticket in similarTickets.Take(5)) // Limit to top 5 for readability
            {
                comment += $"• **{ticket.TicketKey}** - {ticket.Title}\n";
                comment += $"  ?? Similarity: {ticket.SimilarityScore:P0} ({ticket.MatchReason})\n";
                comment += $"  ?? Created: {ticket.CreatedDate:yyyy-MM-dd}";
                
                if (ticket.ResolvedDate.HasValue)
                {
                    comment += $" | ? Resolved: {ticket.ResolvedDate:yyyy-MM-dd}";
                }
                else
                {
                    comment += $" | ?? Status: {ticket.Status}";
                }

                if (!string.IsNullOrEmpty(ticket.PullRequestUrl))
                {
                    comment += $"\n  ?? PR: {ticket.PullRequestUrl}";
                }

                if (ticket.AffectedFiles.Any())
                {
                    comment += $"\n  ?? Files: {string.Join(", ", ticket.AffectedFiles.Take(3))}";
                    if (ticket.AffectedFiles.Count > 3)
                    {
                        comment += $" (and {ticket.AffectedFiles.Count - 3} more)";
                    }
                }

                comment += "\n\n";
            }

            comment += "---\n";
            comment += "*This analysis was generated automatically by the Production Support system.*";

            return comment;
        }
    }
}