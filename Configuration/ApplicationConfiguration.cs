namespace Alt_Support.Configuration
{
    public class JiraConfiguration
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ApiToken { get; set; } = string.Empty;
        public string ProjectKey { get; set; } = string.Empty;
        public List<string> TargetProjects { get; set; } = new List<string>();
        public int MaxHistoricalTickets { get; set; } = 1000;
        public bool EnableWebhookValidation { get; set; } = true;
        public string WebhookSecret { get; set; } = string.Empty;
    }

    public class SimilarityConfiguration
    {
        public double TitleWeight { get; set; } = 0.4;
        public double DescriptionWeight { get; set; } = 0.3;
        public double FilePathWeight { get; set; } = 0.25;
        public double LabelWeight { get; set; } = 0.05;
        public double MinimumSimilarityThreshold { get; set; } = 0.3;
        public int MaxSimilarTickets { get; set; } = 10;
        public bool EnableSemanticAnalysis { get; set; } = true;
        public bool EnableFilePathMatching { get; set; } = true;
    }

    public class GitHubConfiguration
    {
        public string Token { get; set; } = string.Empty;
        public string UserAgent { get; set; } = "Alt-Support-App";
    }

    public class ApplicationConfiguration
    {
        public JiraConfiguration Jira { get; set; } = new JiraConfiguration();
        public GitHubConfiguration GitHub { get; set; } = new GitHubConfiguration();
        public SimilarityConfiguration Similarity { get; set; } = new SimilarityConfiguration();
        public string DatabaseConnectionString { get; set; } = string.Empty;
        public bool EnableHistoricalDataSync { get; set; } = true;
        public int HistoricalDataSyncIntervalHours { get; set; } = 24;
    }
}