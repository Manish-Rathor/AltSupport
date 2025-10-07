-- Migration script to add PrLinks column to existing TicketInfo table
-- This is only needed if you have existing data and want to preserve it
-- Otherwise, Entity Framework will recreate the table automatically

-- For SQLite, we need to recreate the table since ALTER TABLE has limitations
-- Backup existing data first
CREATE TABLE TicketInfo_backup AS SELECT * FROM TicketInfo;

-- Drop the original table
DROP TABLE TicketInfo;

-- Recreate table with new schema (this will be done automatically by EF)
-- Just run the application and EF will create the new schema

-- If you need to restore data manually:
-- INSERT INTO TicketInfo (Id, TicketKey, Title, Description, TicketType, Status, Priority, Assignee, Reporter, ProjectKey, Labels, Components, AffectedFiles, PullRequestUrl, PrLinks, Resolution, CreatedDate, UpdatedDate, ResolvedDate, RelatedTickets, SimilarityScore)
-- SELECT Id, TicketKey, Title, Description, TicketType, Status, Priority, Assignee, Reporter, ProjectKey, Labels, Components, AffectedFiles, PullRequestUrl, '[]' as PrLinks, Resolution, CreatedDate, UpdatedDate, ResolvedDate, RelatedTickets, SimilarityScore
-- FROM TicketInfo_backup;

-- Clean up backup table
-- DROP TABLE TicketInfo_backup;



