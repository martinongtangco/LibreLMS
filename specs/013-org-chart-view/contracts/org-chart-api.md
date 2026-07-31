# Contracts: Organization Chart View

**Feature**: 013-org-chart-view
**Date**: 2025-08-01

All endpoints are Razor Page handlers served within the management portal. They return either full pages, partial HTML for HTMX swaps, or JSON for the chart data.

## Chart Data Endpoint

### GET `/Admin/Organizations/Chart`

Returns the full org chart page.

**Authorization**: `SuperUser` or `OrgAdmin` role required.

**Response**: Full HTML page with embedded SVG org chart.

**Data payload** (embedded in page model):
```json
{
  "nodes": [
    {
      "id": "guid",
      "name": "string",
      "description": "string|null",
      "depth": 0,
      "x": 100,
      "y": 50,
      "isDisabled": false,
      "isRoot": true,
      "userCount": 5,
      "courseCount": 3,
      "hasChildren": true,
      "parentId": null
    }
  ]
}
```

## Chart Action Endpoints (HTMX targets)

### GET `/Admin/Organizations/Chart/EditDialog?id={guid}`

Returns a modal/partial form for editing an organization.

**Authorization**: `SuperUser` or `OrgAdmin` (within scope).

**Response**: HTML partial (`<form>`) for HTMX swap into `#modal-container`.

**Fields**: Name (required, max 200), Description (optional, max 2000), Enable/Disable toggle.

### POST `/Admin/Organizations/Chart/Update?id={guid}`

Updates an organization's name, description, and disabled status.

**Body**: `application/x-www-form-urlencoded` — `Name`, `Description`, `IsDisabled`

**Response**: 
- 200: HTML partial with updated node for HTMX swap
- 400: Error message if validation fails (duplicate name, blank name)
- 403: Error if OrgAdmin tries to edit outside their subtree

### POST `/Admin/Organizations/Chart/CreateChild?parentId={guid}`

Creates a new child organization under the specified parent.

**Body**: `application/x-www-form-urlencoded` — `Name`, `Description`

**Response**:
- 200: HTML partial with the new node and updated connectors for HTMX swap
- 400: Error if validation fails
- 403: Error if OrgAdmin tries to create outside their subtree

### POST `/Admin/Organizations/Chart/Disable?id={guid}`

Disables an organization and cascades to all descendants.

**Response**:
- 200: HTML partial with updated (grayed out) nodes for HTMX swap
- 400: Error if attempting to disable root organization
- 403: Error if OrgAdmin tries to disable outside their subtree

### POST `/Admin/Organizations/Chart/Enable?id={guid}`

Enables a previously disabled organization and cascades to all descendants.

**Response**:
- 200: HTML partial with updated (restored) nodes for HTMX swap
- 403: Error if OrgAdmin tries to enable outside their subtree

### GET `/Admin/Organizations/Chart/AddUserDialog?orgId={guid}`

Returns a modal/partial form for creating a new user assigned to the organization.

**Response**: HTML partial form for HTMX swap. Fields: Name, Email, Role selection.

### POST `/Admin/Organizations/Chart/CreateUser?orgId={guid}`

Creates a new user and assigns them to the specified organization.

**Body**: `application/x-www-form-urlencoded` — `Name`, `Email`, `Role`

**Response**:
- 200: Updated node partial with incremented user count
- 400: Validation error

### GET `/Admin/Organizations/Chart/AssignUserDialog?orgId={guid}`

Returns a modal/partial with a search/select list of existing users.

**Response**: HTML partial with user search and selection UI.

### POST `/Admin/Organizations/Chart/AssignUser?orgId={guid}&userId={guid}`

Assigns an existing user to the specified organization.

**Response**:
- 200: Updated node partial with incremented user count
- 400: Error if user not found or already assigned
- 403: Error if OrgAdmin scope violation

### GET `/Admin/Organizations/Chart/AssignCourseDialog?orgId={guid}`

Returns a modal/partial with a list of courses available for assignment.

**Response**: HTML partial with course list and multi-select UI.

### POST `/Admin/Organizations/Chart/AssignCourse?orgId={guid}&courseId={guid}`

Associates a course with the specified organization.

**Response**:
- 200: Updated node partial with incremented course count
- 400: Error if course not found or already assigned
- 403: Error if OrgAdmin scope violation

## HTMX Swap Targets

| Target Element | Purpose |
|----------------|---------|
| `#chart-svg` | Main SVG chart area — swapped for node additions/updates |
| `#modal-container` | Modal/dialog overlay for forms (edit, create user, assign, etc.) |
| `#notification-area` | Toast/notification area for success/error messages |

## Error Responses

All endpoints return appropriate HTTP status codes with HTML error partials for HTMX consumption:

| Status | Meaning | Example |
|--------|---------|---------|
| 400 | Bad request / validation error | Duplicate org name, blank required field |
| 403 | Forbidden / scope violation | OrgAdmin accessing outside subtree |
| 404 | Not found | Organization ID does not exist |
| 409 | Conflict | Root org cannot be disabled |
