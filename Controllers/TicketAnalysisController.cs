using Alt_Support.Models;
using Alt_Support.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alt_Support.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TicketAnalysisController : ControllerBase
    {
        private readonly ITicketAnalysisService _analysisService;
        private readonly ILogger<TicketAnalysisController> _logger;

        public TicketAnalysisController(ITicketAnalysisService analysisService, ILogger<TicketAnalysisController> logger)
        {
            _analysisService = analysisService;
            _logger = logger;
        }

        /// <summary>
        /// Analyze a ticket for similar historical tickets
        /// </summary>
        [HttpPost("analyze")]
        public async Task<ActionResult<TicketAnalysisResponse>> AnalyzeTicket([FromBody] TicketAnalysisRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Title))
                {
                    return BadRequest("Title is required for analysis");
                }

                var result = await _analysisService.AnalyzeNewTicketAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing ticket");
                return StatusCode(500, "Internal server error during ticket analysis");
            }
        }

        /// <summary>
        /// Webhook endpoint for Jira ticket creation events
        /// </summary>
        [HttpPost("webhook/jira")]
        public async Task<IActionResult> ProcessJiraWebhook([FromBody] JiraWebhookRequest webhookRequest)
        {
            try
            {
                _logger.LogInformation($"Received webhook: {webhookRequest.WebhookEvent}");

                await _analysisService.ProcessNewTicketWebhookAsync(webhookRequest);
                
                return Ok(new { message = "Webhook processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Jira webhook");
                return StatusCode(500, "Internal server error processing webhook");
            }
        }

        /// <summary>
        /// Manually trigger historical data sync
        /// </summary>
        [HttpPost("sync-historical")]
        public async Task<IActionResult> SyncHistoricalData()
        {
            try
            {
                await _analysisService.SyncHistoricalDataAsync();
                return Ok(new { message = "Historical data sync completed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing historical data");
                return StatusCode(500, "Internal server error during sync");
            }
        }
    }
}