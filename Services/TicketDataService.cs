using Alt_Support.Models;
using Alt_Support.Data;
using Microsoft.EntityFrameworkCore;

namespace Alt_Support.Services
{
    public interface ITicketDataService
    {
        Task<TicketInfo?> GetTicketAsync(string ticketKey);
        Task<List<TicketInfo>> GetTicketsAsync(int skip = 0, int take = 100);
        Task<List<TicketInfo>> GetTicketsByProjectAsync(string projectKey, int skip = 0, int take = 100);
        Task<TicketInfo> SaveTicketAsync(TicketInfo ticket);
        Task<bool> DeleteTicketAsync(string ticketKey);
        Task<List<TicketInfo>> SearchTicketsAsync(string searchTerm, int skip = 0, int take = 100);
        Task<List<TicketInfo>> SearchTicketsWithJiraAsync(string searchTerm, int skip = 0, int take = 100);
        Task<int> GetTotalTicketCountAsync();
        Task BulkSaveTicketsAsync(List<TicketInfo> tickets);
        Task UpdateTicketRelatedTicketsAsync(string ticketKey, List<string> relatedTicketKeys);
    }

    public class TicketDataService : ITicketDataService
    {
        private readonly ApplicationDbContext _context;
        private readonly IJiraService _jiraService;
        private readonly ILogger<TicketDataService> _logger;

        public TicketDataService(ApplicationDbContext context, IJiraService jiraService, ILogger<TicketDataService> logger)
        {
            _context = context;
            _jiraService = jiraService;
            _logger = logger;
        }

        public async Task<TicketInfo?> GetTicketAsync(string ticketKey)
        {
            try
            {
                return await _context.Tickets
                    .FirstOrDefaultAsync(t => t.TicketKey == ticketKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ticket {ticketKey}");
                return null;
            }
        }

        public async Task<List<TicketInfo>> GetTicketsAsync(int skip = 0, int take = 100)
        {
            try
            {
                return await _context.Tickets
                    .OrderByDescending(t => t.CreatedDate)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tickets");
                return new List<TicketInfo>();
            }
        }

        public async Task<List<TicketInfo>> GetTicketsByProjectAsync(string projectKey, int skip = 0, int take = 100)
        {
            try
            {
                return await _context.Tickets
                    .Where(t => t.ProjectKey == projectKey)
                    .OrderByDescending(t => t.CreatedDate)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting tickets for project {projectKey}");
                return new List<TicketInfo>();
            }
        }

        public async Task<TicketInfo> SaveTicketAsync(TicketInfo ticket)
        {
            try
            {
                var existingTicket = await _context.Tickets
                    .FirstOrDefaultAsync(t => t.TicketKey == ticket.TicketKey);

                if (existingTicket != null)
                {
                    // Update existing ticket
                    existingTicket.Title = ticket.Title;
                    existingTicket.Description = ticket.Description;
                    existingTicket.TicketType = ticket.TicketType;
                    existingTicket.Status = ticket.Status;
                    existingTicket.Priority = ticket.Priority;
                    existingTicket.Assignee = ticket.Assignee;
                    existingTicket.Reporter = ticket.Reporter;
                    existingTicket.ProjectKey = ticket.ProjectKey;
                    existingTicket.Labels = ticket.Labels;
                    existingTicket.Components = ticket.Components;
                    existingTicket.AffectedFiles = ticket.AffectedFiles;
                    existingTicket.PullRequestUrl = ticket.PullRequestUrl;
                    existingTicket.PrLinks = ticket.PrLinks;
                    existingTicket.Resolution = ticket.Resolution;
                    existingTicket.UpdatedDate = ticket.UpdatedDate;
                    existingTicket.ResolvedDate = ticket.ResolvedDate;
                    existingTicket.RelatedTickets = ticket.RelatedTickets;

                    _context.Tickets.Update(existingTicket);
                    await _context.SaveChangesAsync();
                    return existingTicket;
                }
                else
                {
                    // Add new ticket
                    _context.Tickets.Add(ticket);
                    await _context.SaveChangesAsync();
                    return ticket;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving ticket {ticket.TicketKey}");
                throw;
            }
        }

        public async Task<bool> DeleteTicketAsync(string ticketKey)
        {
            try
            {
                var ticket = await _context.Tickets
                    .FirstOrDefaultAsync(t => t.TicketKey == ticketKey);

                if (ticket != null)
                {
                    _context.Tickets.Remove(ticket);
                    await _context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting ticket {ticketKey}");
                return false;
            }
        }

        public async Task<List<TicketInfo>> SearchTicketsAsync(string searchTerm, int skip = 0, int take = 100)
        {
            try
            {
                var normalizedSearchTerm = searchTerm.ToLowerInvariant();

                return await _context.Tickets
                    .Where(t => 
                        t.Title.ToLower().Contains(normalizedSearchTerm) ||
                        t.Description.ToLower().Contains(normalizedSearchTerm) ||
                        t.TicketKey.ToLower().Contains(normalizedSearchTerm))
                    .OrderByDescending(t => t.CreatedDate)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching tickets with term: {searchTerm}");
                return new List<TicketInfo>();
            }
        }

        public async Task<List<TicketInfo>> SearchTicketsWithJiraAsync(string searchTerm, int skip = 0, int take = 100)
        {
            try
            {
                // Search local database first
                var localTickets = await SearchTicketsAsync(searchTerm, skip, take);
                
                // Build JQL query for Jira search
                var jqlQuery = BuildJqlQuery(searchTerm);
                
                // Search Jira
                var jiraTickets = await _jiraService.SearchTicketsAsync(jqlQuery, take);
                
                // Combine and deduplicate results (prioritize local data)
                var combinedTickets = CombineSearchResults(localTickets, jiraTickets);
                
                // Apply pagination to combined results
                return combinedTickets.Skip(skip).Take(take).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tickets with Jira integration for term: {SearchTerm}", searchTerm);
                // Fallback to local search only
                return await SearchTicketsAsync(searchTerm, skip, take);
            }
        }

        private string BuildJqlQuery(string searchTerm)
        {
            var normalizedTerm = searchTerm.ToLowerInvariant().Trim();
            
            // Check if it's a specific ticket key pattern (e.g., PROJ-123)
            if (System.Text.RegularExpressions.Regex.IsMatch(normalizedTerm, @"^[A-Z]+-\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return $"key = \"{normalizedTerm.ToUpperInvariant()}\"";
            }
            
            // Check for field-specific searches (priority:high, status:open, etc.)
            if (normalizedTerm.Contains(":"))
            {
                var parts = normalizedTerm.Split(':', 2);
                var field = parts[0].Trim();
                var value = parts[1].Trim().Trim('"');
                
                return field.ToLowerInvariant() switch
                {
                    "priority" => $"priority = \"{value}\"",
                    "status" => $"status = \"{value}\"",
                    "assignee" => $"assignee = \"{value}\"",
                    "reporter" => $"reporter = \"{value}\"",
                    "project" => $"project = \"{value}\"",
                    "type" => $"issuetype = \"{value}\"",
                    "component" => $"component = \"{value}\"",
                    "label" => $"labels = \"{value}\"",
                    _ => $"text ~ \"{searchTerm}\"" // Fallback to text search
                };
            }
            
            // Default: search in summary, description, and comments
            return $"(summary ~ \"{searchTerm}\" OR description ~ \"{searchTerm}\" OR comment ~ \"{searchTerm}\")";
        }

        private List<TicketInfo> CombineSearchResults(List<TicketInfo> localTickets, List<TicketInfo> jiraTickets)
        {
            var combinedResults = new List<TicketInfo>(localTickets);
            var localTicketKeys = new HashSet<string>(localTickets.Select(t => t.TicketKey), StringComparer.OrdinalIgnoreCase);
            
            // Add Jira tickets that aren't already in local results
            foreach (var jiraTicket in jiraTickets)
            {
                if (!localTicketKeys.Contains(jiraTicket.TicketKey))
                {
                    // Mark as from Jira for potential saving
                    jiraTicket.SimilarityScore = -1; // Use negative score to indicate Jira origin
                    combinedResults.Add(jiraTicket);
                }
            }
            
            // Sort by creation date (newest first) and then by similarity score
            return combinedResults
                .OrderByDescending(t => t.SimilarityScore >= 0) // Local tickets first
                .ThenByDescending(t => t.CreatedDate)
                .ToList();
        }

        public async Task<int> GetTotalTicketCountAsync()
        {
            try
            {
                return await _context.Tickets.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total ticket count");
                return 0;
            }
        }

        public async Task BulkSaveTicketsAsync(List<TicketInfo> tickets)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                foreach (var ticket in tickets)
                {
                    var existingTicket = await _context.Tickets
                        .FirstOrDefaultAsync(t => t.TicketKey == ticket.TicketKey);

                    if (existingTicket != null)
                    {
                        // Update existing ticket
                        _context.Entry(existingTicket).CurrentValues.SetValues(ticket);
                    }
                    else
                    {
                        // Add new ticket
                        _context.Tickets.Add(ticket);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Successfully bulk saved {tickets.Count} tickets");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error bulk saving {tickets.Count} tickets");
                throw;
            }
        }

        public async Task UpdateTicketRelatedTicketsAsync(string ticketKey, List<string> relatedTicketKeys)
        {
            try
            {
                var ticket = await _context.Tickets
                    .FirstOrDefaultAsync(t => t.TicketKey == ticketKey);

                if (ticket != null)
                {
                    ticket.RelatedTickets = relatedTicketKeys;
                    _context.Tickets.Update(ticket);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Updated related tickets for {ticketKey}: {string.Join(", ", relatedTicketKeys)}");
                }
                else
                {
                    _logger.LogWarning($"Ticket {ticketKey} not found for updating related tickets");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating related tickets for {ticketKey}");
                throw;
            }
        }
    }
}