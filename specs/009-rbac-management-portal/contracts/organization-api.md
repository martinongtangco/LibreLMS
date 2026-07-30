# Contract: Organization Management API

**Module**: Management  
**Namespace**: `LibreLms.Modules.Management.Endpoints`

## Endpoints

### GET /api/organizations

List organizations visible to the current user (scoped by role).

**Query parameters**:
- `parentId` (Guid?, optional) — filter by parent organization (list children of a specific org)

**Response 200**:
```json
{
  "organizations": [
    {
      "id": "guid",
      "name": "string",
      "description": "string | null",
      "parentId": "guid | null",
      "createdAt": "ISO 8601",
      "learnerCount": 0,
      "courseCount": 0
    }
  ]
}
```

**Authorization**: SuperUser (all orgs), OrgAdmin (own subtree), Learner (own org only)

---

### GET /api/organizations/{id:guid}

Get a single organization by ID.

**Response 200**:
```json
{
  "id": "guid",
  "name": "string",
  "description": "string | null",
  "parentId": "guid | null",
  "createdAt": "ISO 8601",
  "children": [
    { "id": "guid", "name": "string" }
  ],
  "learnerCount": 0,
  "courseCount": 0
}
```

**Response 404**: Organization not found or outside user's scope  
**Authorization**: SuperUser (any org), OrgAdmin (own subtree)

---

### POST /api/organizations

Create a new organization.

**Request body**:
```json
{
  "name": "string",
  "description": "string | null",
  "parentId": "guid | null"
}
```

**Validation**:
- `name`: required, 1-200 characters, unique within parent
- `parentId`: if provided, must exist and be within user's scope
- `parentId`: if null, only SuperUser can create root-level orgs (and only one root exists)

**Response 201**:
```json
{
  "id": "guid",
  "name": "string",
  "parentId": "guid | null"
}
```

**Response 400**: Validation error  
**Response 403**: Outside user's scope  
**Authorization**: SuperUser (any location), OrgAdmin (under own org only)

---

### PUT /api/organizations/{id:guid}

Update an existing organization.

**Request body**:
```json
{
  "name": "string",
  "description": "string | null"
}
```

**Response 200**: Updated organization object  
**Response 400**: Validation error  
**Response 403**: Outside user's scope  
**Authorization**: SuperUser (any org), OrgAdmin (own subtree)

---

### DELETE /api/organizations/{id:guid}

Delete an organization (soft delete).

**Response 204**: No content — deletion successful  
**Response 400**: Organization has active children, learners, or courses (must resolve dependents first)  
**Response 403**: Outside user's scope  
**Response 405**: Cannot delete root organization  
**Authorization**: SuperUser (any non-root org), OrgAdmin (own subtree, non-own-org)

---

## DTOs

### OrganizationSummary

```csharp
record OrganizationSummary(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentId,
    DateTimeOffset CreatedAt,
    int LearnerCount,
    int CourseCount
);
```

### CreateOrganizationRequest

```csharp
record CreateOrganizationRequest(
    string Name,
    string? Description,
    Guid? ParentId
);
```

### UpdateOrganizationRequest

```csharp
record UpdateOrganizationRequest(
    string Name,
    string? Description
);
```
