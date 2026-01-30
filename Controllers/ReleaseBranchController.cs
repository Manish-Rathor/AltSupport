using Microsoft.AspNetCore.Mvc;
using Alt_Support.Services;
using Alt_Support.Models;

namespace Alt_Support.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReleaseBranchController : ControllerBase
    {
        private readonly IReleaseBranchService _releaseBranchService;
        private readonly IJiraService _jiraService;
        private readonly GitHubService _gitHubService;
        private readonly ILogger<ReleaseBranchController> _logger;

        public ReleaseBranchController(
            IReleaseBranchService releaseBranchService, 
            IJiraService jiraService,
            GitHubService gitHubService,
            ILogger<ReleaseBranchController> logger)
        {
            _releaseBranchService = releaseBranchService;
            _jiraService = jiraService;
            _gitHubService = gitHubService;
            _logger = logger;
        }

        /// <summary>
        /// Debug endpoint to test fetching tickets and PR links
        /// </summary>
        [HttpGet("debug")]
        public async Task<ActionResult> DebugReleaseBranches([FromQuery] int days = 30)
        {
            try
            {
                var endDate = DateTime.Now;
                var startDate = DateTime.Now.AddDays(-days);
                var startDateStr = startDate.ToString("yyyy-MM-dd");
                var endDateStr = endDate.AddDays(1).ToString("yyyy-MM-dd");
                
                var jql = $"updated >= '{startDateStr}' AND updated < '{endDateStr}' ORDER BY updated DESC";
                
                _logger.LogInformation("Debug: Executing JQL: {JQL}", jql);
                
                var tickets = await _jiraService.SearchTicketsAsync(jql, 100);
                
                var debugInfo = new
                {
                    JqlQuery = jql,
                    TotalTicketsFound = tickets.Count,
                    TicketsWithPRLinks = tickets.Count(t => t.PrLinks != null && t.PrLinks.Any()),
                    SampleTickets = tickets.Take(20).Select(t => new
                    {
                        t.TicketKey,
                        t.Title,
                        t.Status,
                        PrLinksCount = t.PrLinks?.Count ?? 0,
                        PrLinks = t.PrLinks ?? new List<string>()
                    }).ToList()
                };
                
                return Ok(debugInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug endpoint error");
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Test fetching PR links from Jira Development panel
        /// </summary>
        [HttpGet("debug/devlinks")]
        public async Task<ActionResult> DebugDevLinks([FromQuery] string issueKey)
        {
            try
            {
                if (string.IsNullOrEmpty(issueKey))
                {
                    return BadRequest(new { error = "Please provide an issueKey query parameter, e.g., ?issueKey=EP-39712" });
                }
                
                var prLinks = await _jiraService.GetDevelopmentPRLinksAsync(issueKey);
                
                return Ok(new
                {
                    IssueKey = issueKey,
                    PrLinksCount = prLinks.Count,
                    PrLinks = prLinks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug dev links endpoint error");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Test GitHub API by fetching a specific PR
        /// </summary>
        [HttpGet("debug/pr")]
        public async Task<ActionResult> DebugPR([FromQuery] string prUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(prUrl))
                {
                    return BadRequest(new { error = "Please provide a prUrl query parameter, e.g., ?prUrl=https://github.com/owner/repo/pull/123" });
                }
                
                var prDetails = await _gitHubService.GetPRDetailsAsync(prUrl);
                
                if (prDetails == null)
                {
                    return Ok(new { 
                        error = "Failed to fetch PR details",
                        possibleReasons = new[] {
                            "GitHub token not configured in appsettings.json",
                            "Token doesn't have access to this repository",
                            "Invalid PR URL",
                            "PR doesn't exist"
                        }
                    });
                }
                
                return Ok(new
                {
                    PrNumber = prDetails.Number,
                    Title = prDetails.Title,
                    State = prDetails.State,
                    BaseBranch = prDetails.BaseBranch,
                    HeadBranch = prDetails.HeadBranch,
                    IsMerged = prDetails.IsMerged,
                    MergedAt = prDetails.MergedAt,
                    MergedBy = prDetails.MergedBy,
                    Author = prDetails.User,
                    FilesCount = prDetails.Files?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug PR endpoint error");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get all release branches with their associated tickets
        /// Groups tickets by the target branch they were merged into
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ReleaseBranchResponse>> GetReleaseBranches(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? branchFilter = null,
            [FromQuery] bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Fetching release branches. StartDate: {StartDate}, EndDate: {EndDate}, Filter: {Filter}, ForceRefresh: {ForceRefresh}", 
                    startDate, endDate, branchFilter, forceRefresh);
                
                var response = await _releaseBranchService.GetReleaseBranchesAsync(startDate, endDate, branchFilter, forceRefresh);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching release branches");
                return StatusCode(500, new { error = "Failed to fetch release branch data", message = ex.Message });
            }
        }

        /// <summary>
        /// Get only release branches (filters by "release/" prefix)
        /// </summary>
        [HttpGet("releases-only")]
        public async Task<ActionResult<ReleaseBranchResponse>> GetReleaseBranchesOnly(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Fetching release branches only. StartDate: {StartDate}, EndDate: {EndDate}, ForceRefresh: {ForceRefresh}", 
                    startDate, endDate, forceRefresh);
                
                // Filter to only show branches that start with "release/"
                var response = await _releaseBranchService.GetReleaseBranchesAsync(startDate, endDate, "release/", forceRefresh);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching release branches");
                return StatusCode(500, new { error = "Failed to fetch release branch data", message = ex.Message });
            }
        }

        /// <summary>
        /// Get details for a specific release branch
        /// </summary>
        [HttpGet("{branchName}")]
        public async Task<ActionResult<ReleaseBranchInfo>> GetReleaseBranchDetails(
            string branchName,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // URL decode the branch name (e.g., "release%2F9.91.0" -> "release/9.91.0")
                var decodedBranchName = Uri.UnescapeDataString(branchName);
                
                _logger.LogInformation("Fetching details for release branch: {BranchName}", decodedBranchName);
                
                var branchDetails = await _releaseBranchService.GetReleaseBranchDetailsAsync(decodedBranchName, startDate, endDate);
                
                if (branchDetails == null)
                {
                    return NotFound(new { error = $"Release branch '{decodedBranchName}' not found" });
                }
                
                return Ok(branchDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching release branch details for {BranchName}", branchName);
                return StatusCode(500, new { error = "Failed to fetch release branch details", message = ex.Message });
            }
        }
    }
}
