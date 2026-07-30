# Contract: Course Management API (Organization-scoped)

**Module**: Management (delegates to Catalog/Scorm modules)  
**Namespace**: `LibreLms.Modules.Management.Endpoints`

> This contract defines the admin-facing course management endpoints.
> The existing public course browse API (`/api/courses`) remains unchanged
> but will return org-scoped results based on the authenticated user.

## Endpoints

### GET /api/admin/courses

List courses visible to the current user (own org + inherited from ancestors, minus hidden).

**Query parameters**:
- `organizationId` (Guid?, optional) — list courses for a specific org (must be within scope)
- `includeInherited` (bool, default true) — include courses inherited from ancestor orgs
- `source` (string?, optional) — filter by "local" or "inherited"

**Response 200**:
```json
{
  "courses": [
    {
      "id": "guid",
      "title": "string",
      "organizationId": "guid",
      "organizationName": "string",
      "source": "local | inherited",
      "isHidden": false,
      "scormPackageAvailable": true,
      "enrollmentCount": 0,
      "completionRate": 0.75
    }
  ]
}
```

**Authorization**: SuperUser (all courses), OrgAdmin (own subtree), Learner (enrolled courses only)

---

### POST /api/admin/courses/upload

Upload a SCORM package for a specific organization.

**Request**: `multipart/form-data`
- `package` (file) — .zip SCORM package
- `organizationId` (Guid) — target organization
- `title` (string) — course title (optional, extracted from manifest if not provided)

**Validation**:
- File must be a valid .zip archive
- Must contain a valid imsmanifest.xml
- Organization must be within user's scope
- Title must be unique within the target organization

**Response 201**:
```json
{
  "courseId": "guid",
  "scormPackageId": "guid",
  "title": "string",
  "organizationId": "guid"
}
```

**Response 400**: Invalid package or validation error  
**Response 403**: Organization outside user's scope  
**Response 409**: Duplicate course title in organization  
**Authorization**: SuperUser (any org), OrgAdmin (own org or descendants)

---

### PUT /api/admin/courses/{courseId:guid}/visibility

Show or hide an inherited course for a specific organization.

**Request body**:
```json
{
  "organizationId": "guid",
  "isHidden": true
}
```

**Validation**:
- Course must be inherited (not locally uploaded) for the target organization
- Organization must be within user's scope

**Response 200**:
```json
{
  "courseId": "guid",
  "organizationId": "guid",
  "isHidden": true
}
```

**Response 400**: Cannot override visibility of local course  
**Response 403**: Organization outside user's scope  
**Authorization**: SuperUser (any), OrgAdmin (own org only)

---

### DELETE /api/admin/courses/{courseId:guid}

Delete a locally uploaded course.

**Response 204**: No content — course deleted  
**Response 400**: Course has active enrollments (must cancel first)  
**Response 403**: Course outside user's scope  
**Authorization**: SuperUser (any course), OrgAdmin (locally uploaded courses in own subtree)

---

## DTOs

### AdminCourseSummary

```csharp
record AdminCourseSummary(
    Guid Id,
    string Title,
    Guid OrganizationId,
    string OrganizationName,
    string Source,        // "local" or "inherited"
    bool IsHidden,
    bool ScormPackageAvailable,
    int EnrollmentCount,
    double? CompletionRate
);
```

### CourseVisibilityRequest

```csharp
record CourseVisibilityRequest(
    Guid OrganizationId,
    bool IsHidden
);
```
