# API Examples and Sample Data

## Sample Webhook Request from Jira

When a new ticket is created in Jira, you'll receive a webhook like this:

```json
{
  "timestamp": 1704369660000,
  "webhookEvent": "jira:issue_created",
  "issue_event_type_name": "issue_created",
  "user": {
    "self": "https://your-company.atlassian.net/rest/api/3/user?accountId=123456789",
    "accountId": "123456789",
    "displayName": "John Doe",
    "emailAddress": "john.doe@company.com"
  },
  "issue": {
    "id": "10001",
    "key": "SUPPORT-456",
    "fields": {
      "summary": "User authentication fails with error 500 on login page",
      "description": {
        "type": "doc",
        "version": 1,
        "content": [
          {
            "type": "paragraph",
            "content": [
              {
                "type": "text",
                "text": "Multiple users are reporting authentication failures when trying to log in. The login page is returning a 500 internal server error. This appears to be affecting the following files: src/components/Login.jsx, api/controllers/AuthController.cs, and config/database.json. The error started appearing after the latest deployment. Similar issue might be related to PR #234."
              }
            ]
          }
        ]
      },
      "issuetype": {
        "id": "10001",
        "name": "Bug"
      },
      "status": {
        "id": "10001",
        "name": "To Do"
      },
      "priority": {
        "id": "10002",
        "name": "High"
      },
      "assignee": {
        "accountId": "987654321",
        "displayName": "Jane Smith",
        "emailAddress": "jane.smith@company.com"
      },
      "reporter": {
        "accountId": "123456789",
        "displayName": "John Doe",
        "emailAddress": "john.doe@company.com"
      },
      "project": {
        "id": "10000",
        "key": "SUPPORT",
        "name": "Production Support"
      },
      "labels": ["authentication", "login", "production", "urgent"],
      "components": [
        {
          "id": "10001",
          "name": "Authentication Service"
        },
        {
          "id": "10002",
          "name": "Web Frontend"
        }
      ],
      "created": "2024-01-04T10:15:30.000+0000",
      "updated": "2024-01-04T10:15:30.000+0000"
    }
  }
}
```

## Sample Analysis Request

```json
{
  "title": "User authentication fails with error 500 on login page",
  "description": "Multiple users are reporting authentication failures when trying to log in. The login page is returning a 500 internal server error. This appears to be affecting the following files: src/components/Login.jsx, api/controllers/AuthController.cs, and config/database.json. The error started appearing after the latest deployment.",
  "affectedFiles": [
    "src/components/Login.jsx",
    "api/controllers/AuthController.cs",
    "config/database.json"
  ],
  "pullRequestUrl": "https://github.com/company/repo/pull/234",
  "projectKey": "SUPPORT",
  "minimumSimilarityThreshold": 0.3,
  "maxResults": 10
}
```

## Sample Analysis Response

```json
{
  "success": true,
  "message": "Analysis completed. Found 4 similar tickets.",
  "similarTickets": [
    {
      "ticketKey": "SUPPORT-123",
      "title": "Authentication service returns 500 error after deployment",
      "description": "Users unable to authenticate, getting 500 error from auth service",
      "similarityScore": 0.82,
      "matchReason": "Similar title (85%), Similar description (78%), Common affected files (90%) (Overall: 82%)",
      "createdDate": "2023-12-15T09:20:00Z",
      "resolvedDate": "2023-12-15T14:30:00Z",
      "status": "Done",
      "resolution": "Fixed",
      "affectedFiles": [
        "api/controllers/AuthController.cs",
        "src/components/Login.jsx",
        "config/auth.json"
      ],
      "pullRequestUrl": "https://github.com/company/repo/pull/198"
    },
    {
      "ticketKey": "SUPPORT-89",
      "title": "Login page crashes with 500 internal server error",
      "description": "Login functionality broken after recent changes",
      "similarityScore": 0.67,
      "matchReason": "Similar title (70%), Common affected files (65%) (Overall: 67%)",
      "createdDate": "2023-11-28T16:45:00Z",
      "resolvedDate": "2023-11-29T10:15:00Z",
      "status": "Done",
      "resolution": "Fixed",
      "affectedFiles": [
        "src/components/Login.jsx",
        "api/middleware/auth.js"
      ],
      "pullRequestUrl": "https://github.com/company/repo/pull/167"
    },
    {
      "ticketKey": "SUPPORT-201",
      "title": "Database connection error affecting user auth",
      "description": "Authentication fails due to database connectivity issues",
      "similarityScore": 0.45,
      "matchReason": "Similar description (40%), Common affected files (50%) (Overall: 45%)",
      "createdDate": "2023-10-12T11:30:00Z",
      "resolvedDate": "2023-10-12T15:45:00Z",
      "status": "Done",
      "resolution": "Fixed",
      "affectedFiles": [
        "config/database.json",
        "api/controllers/AuthController.cs"
      ],
      "pullRequestUrl": "https://github.com/company/repo/pull/142"
    },
    {
      "ticketKey": "SUPPORT-178",
      "title": "Production deployment broke login functionality",
      "description": "Users cannot log in after latest production deployment",
      "similarityScore": 0.38,
      "matchReason": "Similar description (35%), General similarity (Overall: 38%)",
      "createdDate": "2023-09-22T08:15:00Z",
      "resolvedDate": "2023-09-22T12:30:00Z",
      "status": "Done",
      "resolution": "Fixed",
      "affectedFiles": [
        "deploy/production.yml",
        "src/components/Login.jsx"
      ],
      "pullRequestUrl": "https://github.com/company/repo/pull/121"
    }
  ],
  "totalMatches": 4
}
```

## Generated Jira Comment

When the system finds similar tickets, it automatically adds a comment like this to the new ticket:

```
?? **Similar Tickets Found**

The following tickets might be related to this issue:

• **SUPPORT-123** - Authentication service returns 500 error after deployment
  ?? Similarity: 82% (Similar title (85%), Similar description (78%), Common affected files (90%) (Overall: 82%))
  ?? Created: 2023-12-15 | ? Resolved: 2023-12-15
  ?? PR: https://github.com/company/repo/pull/198
  ?? Files: api/controllers/AuthController.cs, src/components/Login.jsx, config/auth.json

• **SUPPORT-89** - Login page crashes with 500 internal server error
  ?? Similarity: 67% (Similar title (70%), Common affected files (65%) (Overall: 67%))
  ?? Created: 2023-11-28 | ? Resolved: 2023-11-29
  ?? PR: https://github.com/company/repo/pull/167
  ?? Files: src/components/Login.jsx, api/middleware/auth.js

• **SUPPORT-201** - Database connection error affecting user auth
  ?? Similarity: 45% (Similar description (40%), Common affected files (50%) (Overall: 45%))
  ?? Created: 2023-10-12 | ? Resolved: 2023-10-12
  ?? PR: https://github.com/company/repo/pull/142
  ?? Files: config/database.json, api/controllers/AuthController.cs

• **SUPPORT-178** - Production deployment broke login functionality
  ?? Similarity: 38% (Similar description (35%), General similarity (Overall: 38%))
  ?? Created: 2023-09-22 | ? Resolved: 2023-09-22
  ?? PR: https://github.com/company/repo/pull/121
  ?? Files: deploy/production.yml, src/components/Login.jsx

---
*This analysis was generated automatically by the Production Support system.*
```

## Sample Ticket Data Structure

Here's how tickets are stored in the database:

```json
{
  "id": 1,
  "ticketKey": "SUPPORT-456",
  "title": "User authentication fails with error 500 on login page",
  "description": "Multiple users are reporting authentication failures...",
  "ticketType": "Bug",
  "status": "In Progress",
  "priority": "High",
  "assignee": "Jane Smith",
  "reporter": "John Doe",
  "projectKey": "SUPPORT",
  "labels": ["authentication", "login", "production", "urgent"],
  "components": ["Authentication Service", "Web Frontend"],
  "affectedFiles": [
    "src/components/Login.jsx",
    "api/controllers/AuthController.cs",
    "config/database.json"
  ],
  "pullRequestUrl": "https://github.com/company/repo/pull/234",
  "resolution": "",
  "createdDate": "2024-01-04T10:15:30Z",
  "updatedDate": "2024-01-04T10:15:30Z",
  "resolvedDate": null,
  "relatedTickets": ["SUPPORT-123", "SUPPORT-89", "SUPPORT-201"],
  "similarityScore": 0.0
}
```

## Error Responses

### Invalid Request
```json
{
  "error": "Bad Request",
  "message": "Title is required for analysis",
  "statusCode": 400
}
```

### Service Error
```json
{
  "error": "Internal Server Error",
  "message": "Internal server error during ticket analysis",
  "statusCode": 500
}
```

## Health Check Response

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:01.2345678",
  "entries": {
    "Alt_Support.Data.ApplicationDbContext": {
      "data": {},
      "description": null,
      "duration": "00:00:01.1234567",
      "status": "Healthy",
      "tags": []
    }
  }
}
```

## Statistics Response

```json
{
  "totalTickets": 1247,
  "lastUpdated": "2024-01-04T15:30:45Z"
}
```