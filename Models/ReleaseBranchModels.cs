namespace Alt_Support.Models
{
    /// <summary>
    /// Response containing all release branches with their associated tickets
    /// </summary>
    public class ReleaseBranchResponse
    {
        public int TotalReleaseBranches { get; set; }
        public int TotalTickets { get; set; }
        public int TotalMergedPRs { get; set; }
        public DateTime LastUpdated { get; set; }
        public int ProcessingTimeMs { get; set; }
        public List<ReleaseBranchInfo> ReleaseBranches { get; set; } = new();
    }

    /// <summary>
    /// Information about a specific release branch and its associated tickets
    /// </summary>
    public class ReleaseBranchInfo
    {
        public string BranchName { get; set; } = string.Empty;  // e.g., release/9.91.0
        public string ReleaseVersion { get; set; } = string.Empty;  // e.g., 9.91.0 (extracted from branch name)
        public int TotalTickets { get; set; }
        public int TotalPRs { get; set; }
        public DateTime? EarliestMergeDate { get; set; }
        public DateTime? LatestMergeDate { get; set; }
        public List<ReleaseBranchTicket> Tickets { get; set; } = new();
    }

    /// <summary>
    /// Ticket information with PR details for release branch tracking
    /// </summary>
    public class ReleaseBranchTicket
    {
        public string TicketKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Assignee { get; set; } = string.Empty;
        public string Reporter { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string JiraUrl { get; set; } = string.Empty;
        public List<string> FixVersions { get; set; } = new();
        public List<ReleaseBranchPR> MergedPRs { get; set; } = new();
    }

    /// <summary>
    /// PR information for release branch tracking
    /// </summary>
    public class ReleaseBranchPR
    {
        public int PrNumber { get; set; }
        public string PrUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string BaseBranch { get; set; } = string.Empty;  // Target branch (e.g., release/9.91.0)
        public string HeadBranch { get; set; } = string.Empty;  // Source branch (e.g., bug/ep-39712)
        public bool IsMerged { get; set; }
        public DateTime? MergedAt { get; set; }
        public string MergedBy { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for fetching release branch data
    /// </summary>
    public class ReleaseBranchRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? BranchFilter { get; set; }  // Optional: filter by specific branch pattern
        public bool ForceRefresh { get; set; } = false;
    }
}
