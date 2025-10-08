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
                    User = prData?.User?.Login ?? "",
                    Files = files ?? new List<GitHubPRFile>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching PR details from {prUrl}");
                return null;
            }
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
        public string User { get; set; } = string.Empty;
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
        
        [JsonPropertyName("user")]
        public GitHubUser? User { get; set; }
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
