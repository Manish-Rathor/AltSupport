# Alt Support - Production Support Ticket Analysis System

A comprehensive .NET 8 application that automatically analyzes new Jira tickets and finds similar historical tickets to help production support teams work more efficiently.

## Features

### ?? Intelligent Ticket Analysis
- **Smart Similarity Detection**: Uses advanced algorithms to find similar tickets based on title, description, affected files, and labels
- **Multi-factor Analysis**: Combines text similarity, file path matching, and semantic analysis
- **Configurable Weights**: Customize how different factors contribute to similarity scores

### ?? Automatic Processing
- **Jira Webhook Integration**: Automatically processes new tickets as they are created
- **Real-time Analysis**: Instantly analyzes new tickets and attaches relevant historical information
- **Background Data Sync**: Keeps historical ticket data up to date

### ?? Comprehensive Data Management
- **Historical Ticket Storage**: Maintains a local database of ticket information for fast analysis
- **Multi-project Support**: Can analyze tickets across multiple Jira projects
- **File Path Extraction**: Automatically extracts and matches affected file paths from descriptions

### ?? RESTful API
- **Full REST API**: Complete API for integration with other tools
- **Swagger Documentation**: Built-in API documentation
- **Health Checks**: Monitor system health and database connectivity

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Jira Cloud instance with API access
- Valid Jira API token

### Configuration

1. **Update appsettings.json** with your Jira configuration:

```json
{
  "ApplicationConfiguration": {
    "Jira": {
      "BaseUrl": "https://your-company.atlassian.net",
      "Username": "your-email@company.com",
      "ApiToken": "your-jira-api-token",
      "ProjectKey": "SUPPORT",
      "TargetProjects": ["SUPPORT", "BUG", "STORY"],
      "MaxHistoricalTickets": 1000
    }
  }
}
```

2. **Run the application**:
```bash
dotnet run
```

3. **Access the API documentation** at: `https://localhost:5001`

### Setting Up Jira Webhook

1. Go to your Jira project settings
2. Navigate to **Webhooks**
3. Create a new webhook with:
   - **URL**: `https://your-domain.com/api/ticketanalysis/webhook/jira`
   - **Events**: Issue Created
   - **JQL Filter**: `project = YOUR_PROJECT_KEY`

## API Endpoints

### Ticket Analysis
- `POST /api/ticketanalysis/analyze` - Analyze a ticket for similarities
- `POST /api/ticketanalysis/webhook/jira` - Jira webhook endpoint
- `POST /api/ticketanalysis/sync-historical` - Manually sync historical data

### Ticket Management
- `GET /api/tickets` - Get tickets with pagination and filtering
- `GET /api/tickets/{ticketKey}` - Get specific ticket details
- `POST /api/tickets/{ticketKey}/refresh` - Refresh ticket from Jira
- `GET /api/tickets/statistics` - Get ticket statistics

### System
- `GET /health` - Health check endpoint
- `GET /status` - System status

## Example Usage

### Analyze a New Ticket

```bash
curl -X POST "https://localhost:5001/api/ticketanalysis/analyze" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Login page returns 500 error",
    "description": "Users getting 500 error when trying to login through the main page",
    "affectedFiles": ["src/auth/login.js", "controllers/AuthController.cs"],
    "projectKey": "SUPPORT",
    "minimumSimilarityThreshold": 0.3
  }'
```

### Response Example

```json
{
  "success": true,
  "message": "Analysis completed. Found 3 similar tickets.",
  "similarTickets": [
    {
      "ticketKey": "SUPPORT-123",
      "title": "Authentication service returns error 500",
      "similarityScore": 0.75,
      "matchReason": "Similar title (80%), Common affected files (70%) (Overall: 75%)",
      "createdDate": "2024-01-15T10:30:00Z",
      "resolvedDate": "2024-01-16T14:20:00Z",
      "status": "Done",
      "resolution": "Fixed",
      "affectedFiles": ["src/auth/login.js", "middleware/auth.js"],
      "pullRequestUrl": "https://github.com/company/repo/pull/456"
    }
  ],
  "totalMatches": 3
}
```

## Configuration Options

### Similarity Weights
Customize how different factors contribute to similarity scoring:

```json
{
  "Similarity": {
    "TitleWeight": 0.4,        // 40% weight for title similarity
    "DescriptionWeight": 0.3,   // 30% weight for description similarity
    "FilePathWeight": 0.25,     // 25% weight for file path similarity
    "LabelWeight": 0.05,        // 5% weight for label similarity
    "MinimumSimilarityThreshold": 0.3,
    "MaxSimilarTickets": 10
  }
}
```

### Historical Data Sync
Configure automatic background synchronization:

```json
{
  "EnableHistoricalDataSync": true,
  "HistoricalDataSyncIntervalHours": 24
}
```

## How It Works

### 1. Ticket Creation
When a new ticket is created in Jira, a webhook triggers the analysis process.

### 2. Data Extraction
The system extracts:
- Title and description text
- File paths from description and comments
- Labels and components
- Project information

### 3. Similarity Analysis
The system compares the new ticket against historical tickets using:
- **Text Similarity**: Jaccard similarity with word and bigram analysis
- **File Path Matching**: Exact and partial path matching
- **Label Comparison**: Common labels and components
- **Weighted Scoring**: Configurable weights for different factors

### 4. Results
Similar tickets are:
- Ranked by similarity score
- Attached as comments to the new ticket
- Stored as related ticket references
- Available via API for external tools

## Database

The system uses SQLite by default but can be configured for other databases:

```json
{
  "DatabaseConnectionString": "Data Source=tickets.db"
}
```

For SQL Server:
```json
{
  "DatabaseConnectionString": "Server=localhost;Database=AltSupport;Trusted_Connection=true;"
}
```

## Security

### API Token Security
- Store Jira API tokens securely
- Use environment variables for production
- Rotate tokens regularly

### Webhook Security
- Enable webhook signature validation
- Use HTTPS endpoints
- Configure proper authentication

## Monitoring

### Health Checks
The system provides health checks at `/health` that monitor:
- Database connectivity
- Jira API accessibility
- System performance

### Logging
Comprehensive logging is available for:
- Ticket analysis events
- API requests
- Background sync operations
- Error tracking

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License.

## Support

For support and questions:
- Check the API documentation at `/swagger`
- Review the logs for error details
- Ensure Jira configuration is correct
- Verify webhook setup in Jira

## Roadmap

- [ ] Machine learning-based similarity detection
- [ ] Integration with GitHub/GitLab for PR analysis
- [ ] Advanced analytics and reporting
- [ ] Multi-tenant support
- [ ] Custom field extraction rules