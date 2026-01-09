using Alt_Support.Models;

namespace Alt_Support.Services
{
    public interface IDashboardService
    {
        Task<DashboardResponse> GetApplicationLevelDashboardAsync(DateTime? startDate = null, DateTime? endDate = null, bool forceRefresh = false);
        Task<CategoryTicketsResponse> GetTicketsByCategoryAsync(string categoryName, int page, int pageSize, DateTime? startDate = null, DateTime? endDate = null);
        Task<DashboardResponse> RefreshDashboardAsync();
    }
}
