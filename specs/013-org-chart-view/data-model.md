# Data Model: Interactive Organization Chart View

**Feature**: 013-org-chart-view
**Date**: 2025-08-01

## Domain Model Changes

### Organization (existing — modified)

**File**: `src/Modules/Management/Domain/Organization.cs`

New property:
| Property | Type | Description |
|----------|------|-------------|
| `IsDisabled` | `bool` | Soft-disable flag. `false` by default. When `true`, the org and all descendants are inactive. Distinct from `IsDeleted`. |

No changes to existing properties. Full entity:
| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | Primary key (existing) |
| `Name` | `string` | Max 200 chars, unique within parent (existing) |
| `Description` | `string?` | Max 2000 chars (existing) |
| `ParentId` | `Guid?` | Null for root (existing) |
| `Parent` | `Organization?` | Navigation (existing) |
| `Children` | `ICollection<Organization>` | Navigation (existing) |
| `CreatedAt` | `DateTimeOffset` | (existing) |
| `IsDeleted` | `bool` | Soft delete (existing) |
| `IsDisabled` | `bool` | **NEW** — soft disable, cascade to descendants |

### State Transitions: Organization

```
[Active] --disable()--> [Disabled]
[Disabled] --enable()--> [Active]
[Active/Disabled] --delete()--> [Deleted] (IsDeleted = true)
```

**Rules**:
- Root organization cannot be disabled
- Disabling an organization automatically disables all descendants
- Enabling an organization automatically enables all descendants
- Deleted organizations are excluded from all queries (existing behavior)
- Disabled organizations are included in queries but visually distinct

## New DTOs

### OrgChartNodeDto

Returned by `OrganizationService.GetChartTreeAsync()`. Flat list of nodes with pre-computed layout positions.

| Field | Type | Source |
|-------|------|--------|
| `Id` | `Guid` | `Organization.Id` |
| `Name` | `string` | `Organization.Name` |
| `Description` | `string?` | `Organization.Description` |
| `Depth` | `int` | Computed during tree traversal (root = 0) |
| `X` | `int` | Computed by tree layout algorithm (horizontal position in SVG) |
| `Y` | `int` | Computed by tree layout algorithm (vertical position in SVG) |
| `IsDisabled` | `bool` | `Organization.IsDisabled` |
| `IsRoot` | `bool` | `!Organization.ParentId.HasValue` |
| `UserCount` | `int` | Count of `Student` records with matching `OrganizationId` |
| `CourseCount` | `int` | Count of `Course` records with matching `OrganizationId` |
| `HasChildren` | `bool` | `Children.Any()` |
| `ParentId` | `Guid?` | `Organization.ParentId` |

## New Service Methods

### OrganizationService

| Method | Signature | Description |
|--------|-----------|-------------|
| `GetChartTreeAsync` | `Task<IList<OrgChartNodeDto>> GetChartTreeAsync(Guid? rootOrgId = null)` | Returns all active organizations (or scoped subtree) with layout positions and summary counts |
| `DisableAsync` | `Task DisableAsync(Guid id)` | Sets `IsDisabled = true` on the org and cascades to all descendants. Prevents disabling root. |
| `EnableAsync` | `Task EnableAsync(Guid id)` | Sets `IsDisabled = false` on the org and cascades to all descendants. |
| `GetByIdWithStatusAsync` | `Task<(Organization, int UserCount, int CourseCount)> GetByIdWithStatusAsync(Guid id)` | Fetches org with current summary counts for edit dialog |

## Database Migration

**Table**: `Organizations` (existing in `ManagementDbContext`)

**Change**: Add column
```sql
ALTER TABLE Organizations ADD IsDisabled BIT NOT NULL DEFAULT 0;
```

EF Core migration will be generated automatically. No index changes needed.

## No New Entities

This feature does not introduce new domain entities. It extends the existing `Organization` entity with one property and adds presentation-layer DTOs.
