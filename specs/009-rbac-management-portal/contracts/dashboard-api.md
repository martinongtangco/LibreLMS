# Contract: Dashboard API

**Module**: Management  
**Namespace**: `LibreLms.Modules.Management.Endpoints`

## Endpoints

### GET /api/dashboard

Get dashboard metrics scoped to the current user's role and organization.

**Response 200** (SuperUser — system-wide):
```json
{
  "scope": "system",
  "metrics": {
    "totalOrganizations": 12,
    "totalLearners": 450,
    "totalOrgAdmins": 8,
    "totalCourses": 35,
    "activeEnrollments": 520,
    "completedEnrollments": 180,
    "overallCompletionRate": 0.26,
    "recentActivity": [
      {
        "type": "enrollment",
        "description": "Alice enrolled in Introduction to Python",
        "timestamp": "ISO 8601",
        "organizationName": "Engineering"
      }
    ]
  }
}
```

**Response 200** (OrgAdmin — subtree-scoped):
```json
{
  "scope": "organization",
  "organizationId": "guid",
  "organizationName": "Engineering",
  "metrics": {
    "totalSubOrganizations": 3,
    "totalLearners": 45,
    "totalCourses": 8,
    "inheritedCourses": 5,
    "activeEnrollments": 60,
    "completedEnrollments": 20,
    "completionRate": 0.25,
    "recentActivity": [
      {
        "type": "completion",
        "description": "Bob completed Advanced SQL",
        "timestamp": "ISO 8601"
      }
    ]
  }
}
```

**Response 200** (Learner — personal):
```json
{
  "scope": "personal",
  "metrics": {
    "enrolledCourses": 3,
    "completedCourses": 1,
    "inProgressCourses": 2,
    "completionRate": 0.33,
    "recentActivity": [
      {
        "type": "progress",
        "description": "Started Introduction to Python",
        "timestamp": "ISO 8601"
      }
    ]
  }
}
```

**Authorization**: All authenticated users (response shape varies by role)  
**Performance**: Must return within 3 seconds for organizations with up to 1,000 learners (SC-004)

---

## DTOs

### DashboardResponse

```csharp
record DashboardResponse(
    string Scope,           // "system" | "organization" | "personal"
    Guid? OrganizationId,
    string? OrganizationName,
    DashboardMetrics Metrics
);

record DashboardMetrics(
    int? TotalOrganizations,
    int? TotalSubOrganizations,
    int TotalLearners,
    int? TotalOrgAdmins,
    int TotalCourses,
    int? InheritedCourses,
    int ActiveEnrollments,
    int CompletedEnrollments,
    double? CompletionRate,
    DashboardActivity[] RecentActivity
);

record DashboardActivity(
    string Type,            // "enrollment" | "completion" | "progress" | "org-created"
    string Description,
    DateTimeOffset Timestamp,
    string? OrganizationName
);
```
