# Phase 1 Data Model: Organization Tree Branching

**Feature**: `specs/036-org-tree-branching` | **Date**: 2026-08-24

This feature is **display-only**: no database schema, migration, or module-entity changes. Two
structures are in play — the existing domain entity (unchanged) and the page-level view model
(one field added).

## Entity: `Organization` (existing — UNCHANGED)

Location: `src/Modules/Management/Domain/Organization.cs`

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | PK (from `Entity<Guid>`) |
| `Name` | `string` | Unique within its parent |
| `Description` | `string?` | Optional free text |
| `ParentId` | `Guid?` | `null` exactly for the root |
| `CreatedAt` | `DateTimeOffset` | Sibling ordering key (existing) |
| `IsDeleted` | `bool` | Soft delete; already excluded by `ListAllAsync` |
| `IsDisabled` | `bool` | Soft disable; subtree-wide inactive semantics |

**Relationships**: self-referential parent→child (`ParentId` / `Children`); exactly one root
(`ParentId == null`) — enforced by existing service validation, unchanged here.

**State rules relevant to rendering** (existing, restated for the view):
- `IsDeleted == true` → never rendered (already filtered by `ListAllAsync`).
- `IsDisabled == true` → node **and all descendants** render with the disabled treatment (R4).
- Root (`ParentId == null`) → renders with the root treatment (R3); root cannot be disabled
  (existing rule).

**Validation rules** (existing, unchanged): single root; name unique within parent; non-empty name.

## View Model: `OrgTreeNode` (page-level record — EXTENDED)

Location: `src/Host/Pages/Admin/Organizations/Index.cshtml.cs`

```text
OrgTreeNode(
  Guid Id,
  string Name,
  string? Description,
  Guid? ParentId,
  bool IsDisabled,      // NEW: own flag OR any ancestor's flag (subtree propagation)
  List<OrgTreeNode> Children
)
```

- **Construction**: `IndexModel.BuildTree` (already recursive) computes `IsDisabled` as
  `org.IsDisabled || ancestorDisabled` while walking the tree; siblings keep service ordering
  (`CreatedAt`).
- **Data source**: `OrganizationService.ListAllAsync()` — already returns full entities (incl.
  `IsDisabled`) filtered to `!IsDeleted`. No service, contract, or DbContext change.
- **Invariants the view relies on** (guaranteed by existing service logic):
  1. Exactly one node has `ParentId == null` (the root).
  2. Every other node's `ParentId` matches exactly one rendered node's `Id` (no orphans in
     `Children` linkage after `BuildTree`).
  3. The structure is acyclic (enforced by the parent-validation rules at write time).

## No changes to

- `ManagementDbContext` / migrations (no schema work)
- `OrganizationService` or `Management.Contracts` (no new DTOs or interfaces)
- The chart view's `OrgChartNodeDto` pipeline (separate surface, spec 013)
