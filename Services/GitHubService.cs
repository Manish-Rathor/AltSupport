using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Alt_Support.Configuration;

namespace Alt_Support.Services
{
    public class GitHubService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GitHubService> _logger;
        private readonly string _githubToken;
        private readonly string _userAgent;

        public GitHubService(HttpClient httpClient, ILogger<GitHubService> logger, IOptions<ApplicationConfiguration> config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _githubToken = config.Value.GitHub?.Token ?? "";
            _userAgent = config.Value.GitHub?.UserAgent ?? "Alt-Support-App";

            // Configure HttpClient for GitHub API
            _httpClient.BaseAddress = new Uri("https://api.github.com/");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _userAgent);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            
            if (!string.IsNullOrEmpty(_githubToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _githubToken);
            }
        }

        public async Task<GitHubPRDetails?> GetPRDetailsAsync(string prUrl)
        {
            try
            {
                // Parse PR URL: https://github.com/owner/repo/pull/number
                var uri = new Uri(prUrl);
                var pathParts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                
                if (pathParts.Length < 4 || pathParts[2] != "pull")
                {
                    _logger.LogWarning($"Invalid GitHub PR URL format: {prUrl}");
                    return null;
                }

                var owner = pathParts[0];
                var repo = pathParts[1];
                var prNumber = pathParts[3];

                _logger.LogInformation($"Fetching PR details for {owner}/{repo}/pull/{prNumber}");

                // Fetch PR details
                var prResponse = await _httpClient.GetAsync($"repos/{owner}/{repo}/pulls/{prNumber}");
                if (!prResponse.IsSuccessStatusCode)
                {
                    var errorContent = await prResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Failed to fetch PR details: {prResponse.StatusCode} - {errorContent}");
                    
                    if (prResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning($"PR not found. This could mean: 1) The repository is private and requires authentication, 2) The PR doesn't exist, or 3) The token doesn't have access to this repository.");
                    }
                    return null;
                }

                var prJson = await prResponse.Content.ReadAsStringAsync();
                var prData = JsonSerializer.Deserialize<GitHubPR>(prJson);

                // Fetch PR files
                var filesResponse = await _httpClient.GetAsync($"repos/{owner}/{repo}/pulls/{prNumber}/files");
                if (!filesResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to fetch PR files: {filesResponse.StatusCode}");
                    return null;
                }

                var filesJson = await filesResponse.Content.ReadAsStringAsync();
                var files = JsonSerializer.Deserialize<List<GitHubPRFile>>(filesJson);

                return new GitHubPRDetails
                {
                    Number = prData?.Number ?? 0,
                    Title = prData?.Title ?? "",
                    State = prData?.State ?? "",
                    HtmlUrl = prData?.HtmlUrl ?? prUrl,
                    CreatedAt = prData?.CreatedAt ?? DateTime.MinValue,
                    UpdatedAt = prData?.UpdatedAt ?? DateTime.MinValue,
                    MergedAt = prData?.MergedAt,
                    MergedBy = prData?.MergedBy?.Login ?? "",
                    User = prData?.User?.Login ?? "",
                    BaseBranch = prData?.Base?.Ref ?? "",  // Target branch (e.g., release/9.91.0)
                    HeadBranch = prData?.Head?.Ref ?? "",  // Source branch (e.g., bug/ep-39712)
                    IsMerged = prData?.Merged ?? false,
                    Files = files ?? new List<GitHubPRFile>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching PR details from {prUrl}");
                return null;
            }
        }

        /// <summary>
        /// Get all branches from a repository (paginated to get all)
        /// </summary>
        public async Task<List<string>> GetAllBranchesAsync(string owner, string repo)
        {
            var allBranches = new List<string>();
            int page = 1;
            int perPage = 100;
            
            try
            {
                _logger.LogInformation($"Fetching all branches for {owner}/{repo}");
                
                while (true)
                {
                    var response = await _httpClient.GetAsync($"repos/{owner}/{repo}/branches?per_page={perPage}&page={page}");
                    
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Failed to fetch branches: {response.StatusCode} - {responseContent}");
                        throw new Exception($"GitHub API error: {response.StatusCode} - {responseContent}");
                    }
                    
                    var branches = JsonSerializer.Deserialize<List<GitHubBranch>>(responseContent);
                    
                    if (branches == null || branches.Count == 0)
                        break;
                    
                    allBranches.AddRange(branches.Select(b => b.Name));
                    _logger.LogInformation($"Page {page}: fetched {branches.Count} branches, total so far: {allBranches.Count}");
                    
                    if (branches.Count < perPage)
                        break; // Last page
                    
                    page++;
                    
                    if (page > 20) // Safety limit - max 2000 branches
                        break;
                }
                
                _logger.LogInformation($"Total branches fetched: {allBranches.Count}");
                return allBranches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching branches for {owner}/{repo}");
                throw;
            }
        }

        /// <summary>
        /// Get all release branches from a repository
        /// </summary>
        public async Task<List<string>> GetReleaseBranchesAsync(string owner, string repo)
        {
            try
            {
                _logger.LogInformation($"Fetching release branches for {owner}/{repo}");
                
                var allBranches = await GetAllBranchesAsync(owner, repo);
                
                // Filter to only release branches and sort by version descending
                var releaseBranches = allBranches
                    .Where(b => b.StartsWith("release/", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(b => b)
                    .ToList();
                
                _logger.LogInformation($"Found {releaseBranches.Count} release branches out of {allBranches.Count} total branches");
                return releaseBranches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching release branches for {owner}/{repo}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Get all tags from a repository (paginated)
        /// </summary>
        public async Task<List<string>> GetAllTagsAsync(string owner, string repo)
        {
            var allTags = new List<string>();
            int page = 1;
            int perPage = 100;
            
            try
            {
                _logger.LogInformation($"Fetching all tags for {owner}/{repo}");
                
                while (true)
                {
                    var response = await _httpClient.GetAsync($"repos/{owner}/{repo}/tags?per_page={perPage}&page={page}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Failed to fetch tags: {response.StatusCode}");
                        break;
                    }
                    
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tags = JsonSerializer.Deserialize<List<GitHubTag>>(responseContent);
                    
                    if (tags == null || tags.Count == 0)
                        break;
                    
                    allTags.AddRange(tags.Select(t => t.Name));
                    
                    if (tags.Count < perPage)
                        break;
                    
                    page++;
                    if (page > 20)
                        break;
                }
                
                _logger.LogInformation($"Total tags fetched: {allTags.Count}");
                return allTags;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching tags for {owner}/{repo}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Get all releases (branches + tags that look like versions)
        /// </summary>
        public async Task<List<string>> GetAllReleasesAsync(string owner, string repo)
        {
            var releases = new HashSet<string>();
            
            // Get release branches
            var branches = await GetReleaseBranchesAsync(owner, repo);
            foreach (var branch in branches)
            {
                var version = branch.Replace("release/", "");
                releases.Add(version);
            }
            
            // Get tags that look like versions (e.g., 9.75.0, v9.75.0)
            var tags = await GetAllTagsAsync(owner, repo);
            foreach (var tag in tags)
            {
                var version = tag.TrimStart('v', 'V');
                // Check if it looks like a version number (e.g., 9.75.0)
                if (System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+"))
                {
                    releases.Add(version);
                }
            }
            
            // Sort by version descending
            return releases
                .OrderByDescending(v => v, new VersionComparer())
                .ToList();
        }

        /// <summary>
        /// Get all merged PRs for a specific base branch
        /// </summary>
        public async Task<List<GitHubPRListItem>> GetMergedPRsForBranchAsync(string owner, string repo, string baseBranch)
        {
            try
            {
                _logger.LogInformation($"Fetching merged PRs for {owner}/{repo} base:{baseBranch}");
                
                // Fetch closed PRs for the base branch (merged PRs are closed)
                var response = await _httpClient.GetAsync(
                    $"repos/{owner}/{repo}/pulls?state=closed&base={Uri.EscapeDataString(baseBranch)}&per_page=100&sort=updated&direction=desc");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to fetch PRs: {response.StatusCode}");
                    return new List<GitHubPRListItem>();
                }
                
                var json = await response.Content.ReadAsStringAsync();
                var prs = JsonSerializer.Deserialize<List<GitHubPRListItem>>(json);
                
                // Filter to only merged PRs
                var mergedPRs = prs?.Where(pr => pr.MergedAt.HasValue).ToList() ?? new List<GitHubPRListItem>();
                
                _logger.LogInformation($"Found {mergedPRs.Count} merged PRs for {baseBranch}");
                return mergedPRs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching merged PRs for {owner}/{repo} base:{baseBranch}");
                return new List<GitHubPRListItem>();
            }
        }
    }

    public class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null || y == null) return 0;
            
            var xParts = x.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
            var yParts = y.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
            
            for (int i = 0; i < Math.Max(xParts.Length, yParts.Length); i++)
            {
                var xVal = i < xParts.Length ? xParts[i] : 0;
                var yVal = i < yParts.Length ? yParts[i] : 0;
                
                if (xVal != yVal)
                    return xVal.CompareTo(yVal);
            }
            return 0;
        }
    }

    // GitHub API Models
    public class GitHubPRDetails
    {
        public int Number { get; set; }
        public string Title { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? MergedAt { get; set; }
        public string MergedBy { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string BaseBranch { get; set; } = string.Empty;  // Target branch (e.g., release/9.91.0)
        public string HeadBranch { get; set; } = string.Empty;  // Source branch (e.g., bug/ep-39712)
        public bool IsMerged { get; set; }
        public List<GitHubPRFile> Files { get; set; } = new List<GitHubPRFile>();
    }

    public class GitHubPR
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;
        
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
        
        [JsonPropertyName("merged_at")]
        public DateTime? MergedAt { get; set; }
        
        [JsonPropertyName("merged")]
        public bool Merged { get; set; }
        
        [JsonPropertyName("merged_by")]
        public GitHubUser? MergedBy { get; set; }
        
        [JsonPropertyName("user")]
        public GitHubUser? User { get; set; }
        
        [JsonPropertyName("base")]
        public GitHubBranchRef? Base { get; set; }  // Target branch
        
        [JsonPropertyName("head")]
        public GitHubBranchRef? Head { get; set; }  // Source branch
    }
    
    public class GitHubBranchRef
    {
        [JsonPropertyName("ref")]
        public string Ref { get; set; } = string.Empty;  // Branch name (e.g., release/9.91.0)
        
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;
    }

    public class GitHubBranch
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class GitHubTag
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class GitHubPRListItem
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;
        
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
        
        [JsonPropertyName("merged_at")]
        public DateTime? MergedAt { get; set; }
        
        [JsonPropertyName("user")]
        public GitHubUser? User { get; set; }
        
        [JsonPropertyName("head")]
        public GitHubBranchRef? Head { get; set; }
    }

    public class GitHubUser
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;
    }

    public class GitHubPRFile
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonPropertyName("additions")]
        public int Additions { get; set; }
        
        [JsonPropertyName("deletions")]
        public int Deletions { get; set; }
        
        [JsonPropertyName("changes")]
        public int Changes { get; set; }
        
        [JsonPropertyName("patch")]
        public string? Patch { get; set; }
    }
}
