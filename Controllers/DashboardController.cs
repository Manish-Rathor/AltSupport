using Microsoft.AspNetCore.Mvc;
using Alt_Support.Services;
using Alt_Support.Models;

namespace Alt_Support.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        /// <summary>
        /// Get application-level dashboard data with categorized tickets
        /// </summary>
        [HttpGet("application-level")]
        public async Task<ActionResult<DashboardResponse>> GetApplicationLevelDashboard(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Fetching application-level dashboard data. StartDate: {StartDate}, EndDate: {EndDate}, ForceRefresh: {ForceRefresh}", 
                    startDate, endDate, forceRefresh);
                var dashboard = await _dashboardService.GetApplicationLevelDashboardAsync(startDate, endDate, forceRefresh);
                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching application-level dashboard");
                return StatusCode(500, new { error = "Failed to fetch dashboard data", message = ex.Message });
            }
        }

        /// <summary>
        /// Get tickets for a specific category
        /// </summary>
        [HttpGet("category/{categoryName}")]
        public async Task<ActionResult<CategoryTicketsResponse>> GetCategoryTickets(
            string categoryName, 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 50,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Fetching tickets for category: {Category}, Page: {Page}, StartDate: {StartDate}, EndDate: {EndDate}", 
                    categoryName, page, startDate, endDate);
                var result = await _dashboardService.GetTicketsByCategoryAsync(categoryName, page, pageSize, startDate, endDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tickets for category: {Category}", categoryName);
                return StatusCode(500, new { error = "Failed to fetch category tickets", message = ex.Message });
            }
        }

        /// <summary>
        /// Refresh dashboard data (clears cache and refetches)
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<DashboardResponse>> RefreshDashboard()
        {
            try
            {
                _logger.LogInformation("Refreshing dashboard data");
                var dashboard = await _dashboardService.RefreshDashboardAsync();
                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard");
                return StatusCode(500, new { error = "Failed to refresh dashboard", message = ex.Message });
            }
        }
    }
}
