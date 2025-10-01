using Alt_Support.Data;
using Alt_Support.Models;
using Microsoft.EntityFrameworkCore;

namespace Alt_Support
{
    public static class SampleDataSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Check if data already exists
            if (await context.Tickets.AnyAsync())
            {
                return; // Database has been seeded
            }

            var sampleTickets = new List<TicketInfo>
            {
                new TicketInfo
                {
                    TicketKey = "AUTO-101",
                    Title = "Automation test failure in login module",
                    Description = "The automation tests for the login module are failing consistently. Need to investigate and fix the test scripts. The issue appears to be related to element locators.",
                    Status = "Open",
                    Priority = "High",
                    Assignee = "john.doe",
                    Reporter = "test.team",
                    ProjectKey = "AUTO",
                    TicketType = "Bug",
                    Labels = new List<string> { "automation", "testing", "login", "urgent" },
                    Components = new List<string> { "Login", "Testing", "UI" },
                    CreatedDate = DateTime.UtcNow.AddDays(-5),
                    UpdatedDate = DateTime.UtcNow.AddDays(-2)
                },
                new TicketInfo
                {
                    TicketKey = "AUTO-102",
                    Title = "Implement automation for user registration flow",
                    Description = "Create automated test scripts for the complete user registration workflow including email verification and profile setup. This will help ensure quality in future releases.",
                    Status = "In Progress",
                    Priority = "Medium",
                    Assignee = "jane.smith",
                    Reporter = "product.team",
                    ProjectKey = "AUTO",
                    TicketType = "Story",
                    Labels = new List<string> { "automation", "registration", "workflow" },
                    Components = new List<string> { "Registration", "Email", "Profile" },
                    CreatedDate = DateTime.UtcNow.AddDays(-10),
                    UpdatedDate = DateTime.UtcNow.AddDays(-1)
                },
                new TicketInfo
                {
                    TicketKey = "BUG-201",
                    Title = "Login button not responding on mobile devices",
                    Description = "Users report that the login button on mobile devices becomes unresponsive after entering credentials. This affects both iOS and Android platforms.",
                    Status = "Open",
                    Priority = "Critical",
                    Assignee = "mobile.dev",
                    Reporter = "support.team",
                    ProjectKey = "BUG",
                    TicketType = "Bug",
                    Labels = new List<string> { "mobile", "login", "critical", "ios", "android" },
                    Components = new List<string> { "Mobile", "Login", "UI" },
                    CreatedDate = DateTime.UtcNow.AddDays(-3),
                    UpdatedDate = DateTime.UtcNow.AddHours(-6)
                },
                new TicketInfo
                {
                    TicketKey = "EPIC-301",
                    Title = "Priority handling system enhancement",
                    Description = "Enhance the priority handling system to better categorize and route tickets based on business impact and urgency. Include automated priority assignment based on keywords.",
                    Status = "Planning",
                    Priority = "High",
                    Assignee = "system.architect",
                    Reporter = "product.owner",
                    ProjectKey = "EPIC",
                    TicketType = "Epic",
                    Labels = new List<string> { "priority", "enhancement", "system", "automation" },
                    Components = new List<string> { "Priority System", "Routing", "Business Logic" },
                    CreatedDate = DateTime.UtcNow.AddDays(-7),
                    UpdatedDate = DateTime.UtcNow.AddDays(-1)
                },
                new TicketInfo
                {
                    TicketKey = "TASK-401",
                    Title = "Setup CI/CD automation pipeline",
                    Description = "Configure continuous integration and deployment automation pipeline for faster and more reliable releases. Include automated testing and deployment to staging environment.",
                    Status = "Done",
                    Priority = "Medium",
                    Assignee = "devops.team",
                    Reporter = "tech.lead",
                    ProjectKey = "TASK",
                    TicketType = "Task",
                    Labels = new List<string> { "automation", "cicd", "deployment", "pipeline" },
                    Components = new List<string> { "CI/CD", "Automation", "Deployment" },
                    CreatedDate = DateTime.UtcNow.AddDays(-15),
                    UpdatedDate = DateTime.UtcNow.AddDays(-3),
                    ResolvedDate = DateTime.UtcNow.AddDays(-3),
                    Resolution = "Fixed"
                },
                new TicketInfo
                {
                    TicketKey = "STORY-501",
                    Title = "User profile automation testing",
                    Description = "As a QA engineer, I want automated tests for user profile management so that we can ensure profile updates work correctly across all scenarios.",
                    Status = "Open",
                    Priority = "Low",
                    Assignee = "qa.engineer",
                    Reporter = "qa.lead",
                    ProjectKey = "STORY",
                    TicketType = "Story",
                    Labels = new List<string> { "automation", "testing", "profile", "qa" },
                    Components = new List<string> { "Profile", "Testing", "User Management" },
                    CreatedDate = DateTime.UtcNow.AddDays(-8),
                    UpdatedDate = DateTime.UtcNow.AddDays(-4)
                },
                new TicketInfo
                {
                    TicketKey = "BUG-601",
                    Title = "High priority search results not sorting correctly",
                    Description = "When searching for tickets with high priority, the results are not being sorted correctly by priority level. Critical and high priority tickets should appear first.",
                    Status = "In Review",
                    Priority = "High",
                    Assignee = "backend.dev",
                    Reporter = "support.agent",
                    ProjectKey = "BUG",
                    TicketType = "Bug",
                    Labels = new List<string> { "priority", "search", "sorting", "bug" },
                    Components = new List<string> { "Search", "Priority", "Backend" },
                    CreatedDate = DateTime.UtcNow.AddDays(-2),
                    UpdatedDate = DateTime.UtcNow.AddHours(-12)
                }
            };

            await context.Tickets.AddRangeAsync(sampleTickets);
            await context.SaveChangesAsync();
        }
    }
}