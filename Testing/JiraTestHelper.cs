using Alt_Support.Services;
using Alt_Support.Configuration;
using Microsoft.Extensions.Options;
using System.Text;

namespace Alt_Support.Testing
{
    public static class JiraTestHelper
    {
        public static async Task TestJiraConnection(IJiraService jiraService)
        {
            Console.WriteLine("Testing Jira connection...");
            
            try
            {
                // Test direct ticket fetch (similar to Postman)
                Console.WriteLine("Testing direct ticket fetch for PRODSUP-28731...");
                var ticket = await jiraService.GetTicketAsync("PRODSUP-28731");
                
                if (ticket != null)
                {
                    Console.WriteLine($"✅ Direct fetch successful: {ticket.TicketKey} - {ticket.Title}");
                }
                else
                {
                    Console.WriteLine("❌ Direct fetch failed - ticket is null");
                }
                
                // Test search (used by autocomplete)
                Console.WriteLine("Testing search for PRODSUP-28731...");
                var searchResults = await jiraService.SearchTicketsAsync("key = \"PRODSUP-28731\"", 1);
                
                if (searchResults.Any())
                {
                    Console.WriteLine($"✅ Search successful: Found {searchResults.Count} results");
                    Console.WriteLine($"   First result: {searchResults[0].TicketKey} - {searchResults[0].Title}");
                }
                else
                {
                    Console.WriteLine("❌ Search failed - no results found");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
            }
        }
    }
}