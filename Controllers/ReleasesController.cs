using Microsoft.AspNetCore.Mvc;
using Alt_Support.Services;
using System.Text.RegularExpressions;

namespace Alt_Support.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReleasesController : ControllerBase
    {
        private readonly GitHubService _gitHubService;
        private readonly ILogger<ReleasesController> _logger;
        
        // Configure your repository here
        private const string GITHUB_OWNER = "tnwinc";
        private const string GITHUB_REPO = "epim";

        public ReleasesController(GitHubService gitHubService, ILogger<ReleasesController> logger)
        {
            _gitHubService = gitHubService;
            _logger = logger;
        }

        /// <summary>
        /// Get list of all releases (from branches and tags)
        /// </summary>
        [HttpGet("list")]
        public async Task<ActionResult> GetReleases()
        {
            try
            {
                var versions = await _gitHubService.GetAllReleasesAsync(GITHUB_OWNER, GITHUB_REPO);
                
                var releases = versions.Select(v => new
                {
                    BranchName = $"release/{v}",
                    Version = v
                }).ToList();
                
                return Ok(new
                {
                    Repository = $"{GITHUB_OWNER}/{GITHUB_REPO}",
                    TotalReleases = releases.Count,
                    Releases = releases
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching releases");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Debug endpoint to see all branches from GitHub
        /// </summary>
        [HttpGet("debug/branches")]
        public async Task<ActionResult> DebugBranches()
        {
            try
            {
                var allBranches = await _gitHubService.GetAllBranchesAsync(GITHUB_OWNER, GITHUB_REPO);
                
                return Ok(new
                {
                    Repository = $"{GITHUB_OWNER}/{GITHUB_REPO}",
                    TotalBranches = allBranches.Count,
                    Branches = allBranches.Take(50).ToList(),
                    ReleaseBranches = allBranches.Where(b => b.StartsWith("release/", StringComparison.OrdinalIgnoreCase)).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching branches");
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Debug endpoint to see all tags from GitHub
        /// </summary>
        [HttpGet("debug/tags")]
        public async Task<ActionResult> DebugTags()
        {
            try
            {
                var allTags = await _gitHubService.GetAllTagsAsync(GITHUB_OWNER, GITHUB_REPO);
                
                return Ok(new
                {
                    Repository = $"{GITHUB_OWNER}/{GITHUB_REPO}",
                    TotalTags = allTags.Count,
                    Tags = allTags
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tags");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get all merged PRs/tickets for a specific release
        /// </summary>
        [HttpGet("{releaseVersion}/tickets")]
        public async Task<ActionResult> GetReleaseTickets(string releaseVersion)
        {
            try
            {
                var branchName = $"release/{releaseVersion}";
                
                var mergedPRs = await _gitHubService.GetMergedPRsForBranchAsync(GITHUB_OWNER, GITHUB_REPO, branchName);
                
                // Extract ticket keys from PR titles or branch names (e.g., "EP-39712 - Fix something" or "bug/ep-39712")
                var ticketPattern = new Regex(@"([A-Z]+-\d+)", RegexOptions.IgnoreCase);
                
                var tickets = mergedPRs.Select(pr => 
                {
                    // First try to extract from PR title
                    var match = ticketPattern.Match(pr.Title);
                    var ticketKey = match.Success ? match.Groups[1].Value.ToUpper() : null;
                    
                    // If not found in title, try to extract from branch name
                    if (ticketKey == null && pr.Head?.Ref != null)
                    {
                        var branchMatch = ticketPattern.Match(pr.Head.Ref);
                        ticketKey = branchMatch.Success ? branchMatch.Groups[1].Value.ToUpper() : null;
                    }
                    
                    return new
                    {
                        PrNumber = pr.Number,
                        PrUrl = pr.HtmlUrl,
                        PrTitle = pr.Title,
                        TicketKey = ticketKey,
                        JiraUrl = ticketKey != null ? $"https://navex.atlassian.net/browse/{ticketKey}" : null,
                        MergedAt = pr.MergedAt,
                        Author = pr.User?.Login,
                        SourceBranch = pr.Head?.Ref
                    };
                })
                .OrderByDescending(t => t.MergedAt)
                .ToList();
                
                return Ok(new
                {
                    Release = releaseVersion,
                    Branch = branchName,
                    TotalPRs = tickets.Count,
                    TicketsWithJiraKey = tickets.Count(t => t.TicketKey != null),
                    Tickets = tickets
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching release tickets for {Release}", releaseVersion);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
