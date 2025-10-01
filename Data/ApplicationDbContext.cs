using Microsoft.EntityFrameworkCore;
using Alt_Support.Models;
using System.Text.Json;

namespace Alt_Support.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<TicketInfo> Tickets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TicketInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TicketKey).IsUnique();
                entity.HasIndex(e => e.Title);
                entity.HasIndex(e => e.ProjectKey);
                entity.HasIndex(e => e.CreatedDate);
                entity.HasIndex(e => e.Status);

                // Configure JSON columns for lists
                entity.Property(e => e.Labels)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                entity.Property(e => e.Components)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                entity.Property(e => e.AffectedFiles)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                entity.Property(e => e.RelatedTickets)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                // Set maximum lengths
                entity.Property(e => e.TicketKey).HasMaxLength(50);
                entity.Property(e => e.Title).HasMaxLength(500);
                entity.Property(e => e.Description).HasMaxLength(4000);
                entity.Property(e => e.TicketType).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.Priority).HasMaxLength(50);
                entity.Property(e => e.Assignee).HasMaxLength(100);
                entity.Property(e => e.Reporter).HasMaxLength(100);
                entity.Property(e => e.ProjectKey).HasMaxLength(20);
                entity.Property(e => e.PullRequestUrl).HasMaxLength(500);
                entity.Property(e => e.Resolution).HasMaxLength(100);
            });
        }
    }
}