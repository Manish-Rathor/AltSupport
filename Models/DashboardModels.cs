namespace Alt_Support.Models
{
    public class DashboardResponse
    {
        public int TotalTickets { get; set; }
        public List<DashboardCategory> Categories { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public int ProcessingTimeMs { get; set; }
    }

    public class DashboardCategory
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<DashboardTicket> Tickets { get; set; } = new();
    }

    public class DashboardTicket
    {
        public string TicketKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Assignee { get; set; } = string.Empty;
        public string Reporter { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Sprint { get; set; } = string.Empty;
        public List<string> Components { get; set; } = new();
        public List<string> Labels { get; set; } = new();
        public List<string> PrLinks { get; set; } = new();
        public List<string> FixVersions { get; set; } = new();
        public string JiraUrl { get; set; } = string.Empty;
    }

    public class CategoryTicketsResponse
    {
        public string CategoryName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public List<DashboardTicket> Tickets { get; set; } = new();
    }
}
