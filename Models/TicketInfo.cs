using System.ComponentModel.DataAnnotations;

namespace Alt_Support.Models
{
    public class TicketInfo
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string TicketKey { get; set; } = string.Empty;
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public string TicketType { get; set; } = string.Empty; // Bug, Story, Task, etc.
        
        public string Status { get; set; } = string.Empty;
        
        public string Priority { get; set; } = string.Empty;
        
        public string Assignee { get; set; } = string.Empty;
        
        public string Reporter { get; set; } = string.Empty;
        
        public string ProjectKey { get; set; } = string.Empty;
        
        public List<string> Labels { get; set; } = new List<string>();
        
        public List<string> Components { get; set; } = new List<string>();
        
        public List<string> AffectedFiles { get; set; } = new List<string>();
        
        public string PullRequestUrl { get; set; } = string.Empty;
        
        // Multiple PR links extracted from custom field and description
        public List<string> PrLinks { get; set; } = new List<string>();
        
        public string Sprint { get; set; } = string.Empty;
        
        public string TestCases { get; set; } = string.Empty;
        
        public string Resolution { get; set; } = string.Empty;
        
        public string EPIMPriority { get; set; } = string.Empty;
        
        public string DeploymentTrainstop { get; set; } = string.Empty;
        
        public List<string> FixVersions { get; set; } = new List<string>();
        
        public string LaunchDarklyToggle { get; set; } = string.Empty;
        
        public DateTime CreatedDate { get; set; }
        
        public DateTime? UpdatedDate { get; set; }
        
        public DateTime? ResolvedDate { get; set; }
        
        // For storing related tickets that were found
        public List<string> RelatedTickets { get; set; } = new List<string>();
        
        // Similarity score when matching
        public double SimilarityScore { get; set; } = 0.0;
    }
}