# Data Model: Admin List Pagination with Page Size Toggle

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-21

This feature is **read-only over existing tables**: no new tables, no column changes, no
state machines. It adds (1) three stored procedures that page/filter/sort existing data,
(2) small additive contract records, and (3) transient page-state per page model.

## Existing entities (unchanged schema)

All tables live in `dbo` of the single MSSQL database.

### Courses (Catalog module — `CatalogDbContext`)

| Column | Type | Role in this feature |
|---|---|---|
| Id | uniqueidentifier PK | row identity + sort tie-break |
| Title | nvarchar | search term, default sort key |
| ShortDescription | nvarchar | returned, displayed |
| Category | nvarchar(100) | filter + sortable column |
| Duration | nvarchar | sortable column |
| OrganizationId | uniqueidentifier | **newly returned** by `BrowseCourses` (for page-bounded org-name resolution) |

### Students (Enrollment module — `EnrollmentDbContext`)

| Column | Type | Role in this feature |
|---|---|---|
| Id | uniqueidentifier PK | row identity + tie-break |
| Name | nvarchar | search term (name/email), default sort key for Learners |
| Email | nvarchar | search term |
| Roles | nvarchar | exact-role filter |
| OrganizationId | uniqueidentifier | **newly returned** (org-name resolution) |
| CreatedAt | datetimeoffset | returned (already displayed context) |

### Enrollments (Enrollment module — `EnrollmentDbContext`)

| Column | Type | Role in this feature |
|---|---|---|
| Id | uniqueidentifier PK | row identity + tie-break (DESC, newest-first) |
| StudentId | uniqueidentifier FK → Students | join key |
| CourseId | uniqueidentifier FK → Courses | join key (cross-module, ADR 0008) |
| EnrolledAt | datetimeoffset | default sort key (DESC) |

### Organizations (Management module — referenced via contract only)

Joined **in C#, not SQL**: org names for the page's distinct `OrganizationId`s are resolved
through `IOrganizationLookup.GetOrganizationAsync` (see research.md R4).

## Stored procedures (new / extended — the feature's core data access)

Full signatures in [contracts/stored-procedures.md](contracts/stored-procedures.md).

| Procedure | Context | Inputs | Result set 1 | Result set 2 |
|---|---|---|---|---|
| `dbo.BrowseCourses` *(extended)* | Catalog | `@SearchTerm`, `@Category`, `@PageSize`, `@PageNumber`, **+ `@SortBy`, `@SortDirection`** | page of courses **(+ `OrganizationId`)** | `TotalCount` (filtered) |
| `dbo.AdminListEnrollments` *(new)* | Enrollment | `@StudentName`, `@CourseTitle`, `@PageSize`, `@PageNumber` | page of enrollment rows (enrollment + student + course + org id) | `TotalCount` (filtered) |
| `dbo.AdminListLearners` *(new)* | Enrollment | `@Search`, `@Role`, `@PageSize`, `@PageNumber` | page of student rows | `TotalCount` (filtered) |

Common procedure rules:
- `SET NOCOUNT ON`; `@PageSize <= 0` → 10; `@PageNumber <= 0` → 1 (procedure-level floor; the
  page model additionally allowlists/clamps).
- Empty-string search/category/role parameters are treated as NULL (no filter).
- Ordering is always the user-visible key **plus PK tie-breaker** (research.md R6).
- Two result sets: rows, then a single-row count — the exact shape the existing
  `CourseCatalogService.BrowseAsync` reader already consumes.

## Additive contract records (Enrollment.Contracts)

All additions are **additive**; existing methods keep their signatures.

```text
IEnrollmentAdmin
+ Task<AdminEnrollmentPageResult> ListPagedAsync(
      string? studentName, string? courseTitle, int pageNumber, int pageSize)

record AdminEnrollmentRow(
    Guid EnrollmentId, Guid StudentId, string StudentName, string StudentEmail,
    Guid CourseId, string CourseTitle, Guid OrganizationId, DateTimeOffset EnrolledAt)

record AdminEnrollmentPageResult(
    IList<AdminEnrollmentRow> Items, int TotalCount)

IUserProvisioning
+ Task<StudentPageResult> ListPagedAsync(
      string? search, string? roleFilter, int pageNumber, int pageSize)

record StudentPageResult(
    IList<StudentProvisionedDto> Items, int TotalCount)

IUserLookup
  record UserSummary(Guid Id, string Name, string Email)
→    record UserSummary(Guid Id, string Name, string Email, Guid OrganizationId)
      (additive last position; single implementor: Enrollment's UserLookupService)
```

Catalog module (not a cross-module contract, application-level):

```text
CourseItemDto(Id, Title, ShortDescription, Category, Duration)
→ CourseItemDto(Id, Title, ShortDescription, Category, Duration, Guid OrganizationId)

BrowseAsync(searchTerm, category, pageNumber, pageSize, visibleCourseIds)
+ BrowseAsync(..., sortBy = "title", sortDirection = "asc")   // optional trailing params
```

## Transient page state (Host page models)

Shared shape for all three admin index pages:

```text
PageNumber : int   bound (GET), default 1, clamped to 1..totalPages
PageSize   : int   bound (GET), default 10, allowlisted to {10, 30, 50, 100} (else 10)
TotalCount : int   from the procedure's count result set (filtered total)
```

Per-page additional bound state (existing, unchanged): Courses `Search`/`Category`/`SortBy`
(`title|category|duration`) /`SortDirection` (`asc|desc`); Enrollments `student`/`course`;
Learners `search`/`role` (`org` remains bound-but-unapplied — pre-existing gap, out of scope,
flagged in spec Assumptions).

Derived: `totalPages = max(1, ceil(TotalCount / PageSize))`; `effectivePage =
max(1, min(PageNumber, totalPages))`; pagination links always carry the effective values +
current filters + current `pageSize`.

## Validation rules (from spec)

| Rule | Source | Enforced in |
|---|---|---|
| Page size ∈ {10, 30, 50, 100}, default 10 | FR-005/006/012 | page model (allowlist) + procedure floor |
| Page number clamped to 1..totalPages | FR-011 | page model (clamp) + procedure floor |
| Whitespace-only search → no search | edge case | page model (trim/null) + procedure (`'' → NULL`) |
| Sort column/direction allowlisted | FR-014 | page model + procedure (unknown → default) |
| Total reflects filtered rows | FR-013 | procedure count result set |
| Deterministic order | FR-015 | procedure ORDER BY + PK tie-break |
