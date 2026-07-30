# Contract: User Management API

**Module**: Management  
**Namespace**: `LibreLms.Modules.Management.Endpoints`

## Endpoints

### GET /api/users

List users visible to the current user (scoped by role and organization).

**Query parameters**:
- `organizationId` (Guid?, optional) — filter by organization
- `role` (string?, optional) — filter by role ("SuperUser", "OrgAdmin", "Learner")
- `search` (string?, optional) — search by name or email

**Response 200**:
```json
{
  "users": [
    {
      "id": "guid",
      "name": "string",
      "email": "string",
      "role": "SuperUser | OrgAdmin | Learner",
      "organizationId": "guid",
      "organizationName": "string",
      "createdAt": "ISO 8601"
    }
  ]
}
```

**Authorization**: SuperUser (all users), OrgAdmin (users in own subtree)

---

### GET /api/users/{id:guid}

Get a single user by ID.

**Response 200**: User object (same shape as list item)  
**Response 404**: User not found or outside scope  
**Authorization**: SuperUser (any user), OrgAdmin (users in own subtree)

---

### POST /api/users

Create a new user.

**Request body**:
```json
{
  "name": "string",
  "email": "string",
  "password": "string",
  "role": "OrgAdmin | Learner",
  "organizationId": "guid"
}
```

**Validation**:
- `name`: required, 1-200 characters
- `email`: required, valid email format, unique
- `password`: required, minimum 8 characters
- `role`: required, "OrgAdmin" or "Learner" (SuperUser creation restricted)
- `organizationId`: required, must exist and be within user's scope

**Response 201**:
```json
{
  "id": "guid",
  "name": "string",
  "email": "string",
  "role": "string",
  "organizationId": "guid"
}
```

**Response 400**: Validation error  
**Response 403**: Outside user's scope  
**Response 409**: Email already exists  
**Authorization**: SuperUser (any org/role), OrgAdmin (own subtree, Learner or OrgAdmin roles only)

---

### PUT /api/users/{id:guid}

Update a user.

**Request body**:
```json
{
  "name": "string",
  "role": "SuperUser | OrgAdmin | Learner",
  "organizationId": "guid"
}
```

**Validation**:
- Cannot demote the last remaining SuperUser (FR-015)
- `organizationId` must be within user's scope if provided
- `role` changes to SuperUser restricted to existing SuperUsers

**Response 200**: Updated user object  
**Response 400**: Validation error (e.g., last SuperUser demotion)  
**Response 403**: Outside user's scope  
**Authorization**: SuperUser (any user, any change), OrgAdmin (users in subtree, Learner/OrgAdmin roles only)

---

### DELETE /api/users/{id:guid}

Remove a user (cancels active enrollments).

**Response 204**: No content — user removed  
**Response 400**: Cannot delete last SuperUser  
**Response 403**: Outside user's scope  
**Authorization**: SuperUser (any non-last-SuperUser), OrgAdmin (users in subtree, cannot delete SuperUsers)

---

## DTOs

### UserSummary

```csharp
record UserSummary(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid OrganizationId,
    string OrganizationName,
    DateTimeOffset CreatedAt
);
```

### CreateUserRequest

```csharp
record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    string Role,
    Guid OrganizationId
);
```

### UpdateUserRequest

```csharp
record UpdateUserRequest(
    string? Name,
    string? Role,
    Guid? OrganizationId
);
```
