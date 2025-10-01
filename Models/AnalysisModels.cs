namespace Alt_Support.Models
{
    public class SimilarityResult
    {
        public string TicketKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double SimilarityScore { get; set; }
        public string MatchReason { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public List<string> AffectedFiles { get; set; } = new List<string>();
        public string PullRequestUrl { get; set; } = string.Empty;
    }

    public class TicketAnalysisRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> AffectedFiles { get; set; } = new List<string>();
        public string PullRequestUrl { get; set; } = string.Empty;
        public string ProjectKey { get; set; } = string.Empty;
        public double MinimumSimilarityThreshold { get; set; } = 0.3;
        public int MaxResults { get; set; } = 10;
    }

    public class TicketAnalysisResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<SimilarityResult> SimilarTickets { get; set; } = new List<SimilarityResult>();
        public int TotalMatches { get; set; }
    }
}