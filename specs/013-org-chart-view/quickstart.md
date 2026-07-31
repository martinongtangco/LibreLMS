# Quickstart: Organization Chart View Validation

**Feature**: 013-org-chart-view
**Date**: 2025-08-01

## Prerequisites

- Docker running (for `mssql` and `valkey` sibling services)
- Devcontainer active (or .NET 10 SDK installed locally)
- Database seeded with test organizations (at least 2 levels of hierarchy)

## Setup

```bash
# From repo root
docker compose up -d mssql valkey

# Apply the new migration (IsDisabled column)
dotnet ef database update --project src/Modules/Management/Management.csproj --context ManagementDbContext

# Run the application
dotnet run --project src/Host/Host.csproj
```

The app starts at `https://localhost:5001` (or similar — check console output).

## Validation Scenarios

### 1. Chart Renders Correctly

1. Log in as SuperUser (credentials from seeder, check `ManagementSeeder.cs`)
2. Navigate to `/Admin/Organizations/Chart`
3. **Expected**: Full organizational hierarchy displayed as an SVG tree chart with connected nodes
4. **Expected**: Each node shows org name, user count badge, and course count badge
5. **Expected**: Auto-fit centers the chart in the viewport on initial load

### 2. Zoom In and Out

1. On the chart page, click the zoom-in button (or scroll mouse wheel up)
2. **Expected**: Chart scales up smoothly (within 200ms), connectors scale proportionally
3. Click zoom-out button (or scroll mouse wheel down)
4. **Expected**: Chart scales down smoothly
5. Drag the chart canvas
6. **Expected**: Chart pans in the direction of the drag

### 3. Context Menu Opens

1. Right-click on any organization node
2. **Expected**: Context menu appears at cursor with options: Edit, Disable/Enable, Add New User, Assign Existing User, Assign Course
3. Click outside the menu or press ESC
4. **Expected**: Menu closes

### 4. Create Child Organization

1. Right-click an org node → select "Add Child Organization"
2. Fill in name (required) and description (optional)
3. Submit the form
4. **Expected**: New node appears as a child of the selected org, connected by a line
5. **Expected**: No full page reload (HTMX swap)

### 5. Edit Organization

1. Right-click an org node → select "Edit Organization"
2. Change the name or description
3. Submit
4. **Expected**: Node updates with new details, no page reload
5. Try submitting with blank name
6. **Expected**: Validation error displayed, org not modified

### 6. Disable / Enable Organization

1. Right-click a non-root org node → select "Disable Organization"
2. Confirm the action
3. **Expected**: Node turns gray/dimmed; all descendant nodes also turn gray
4. Right-click the same node → select "Enable Organization"
5. **Expected**: Node and descendants return to normal appearance
6. Try disabling the root organization
7. **Expected**: Error message: root cannot be disabled

### 7. Add New User from Chart

1. Right-click an org node → select "Add New User"
2. Fill in user details (name, email, role)
3. Submit
4. **Expected**: User created and assigned to the org; user count badge increments

### 8. Assign Existing User from Chart

1. Right-click an org node → select "Assign Existing User"
2. Search for a user by name/email
3. Select a user and confirm
4. **Expected**: User assigned to the org; user count badge increments

### 9. Assign Course from Chart

1. Right-click an org node → select "Assign Course"
2. Select a course from the available list
3. Confirm
4. **Expected**: Course associated with the org; course count badge increments
5. Open the dialog again
6. **Expected**: Already-assigned course shown as assigned (not selectable for duplicate)

### 10. OrgAdmin Scope Enforcement

1. Log in as an OrgAdmin (credentials from seeder)
2. Navigate to `/Admin/Organizations/Chart`
3. **Expected**: Chart shows only the admin's own organization and its descendants
4. **Expected**: Sibling organizations and unrelated branches are NOT visible
5. Try right-clicking and editing — should work within scope
6. **Expected**: Actions only affect orgs within the visible subtree

## Architecture Test Validation

```bash
# Ensure module boundaries are not violated
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj
```

**Expected**: All tests pass. No cross-module boundary violations introduced.

## Rollback

If validation fails, the new `IsDisabled` column can be safely dropped:

```bash
dotnet ef migrations remove --project src/Modules/Management/Management.csproj --context ManagementDbContext
```
