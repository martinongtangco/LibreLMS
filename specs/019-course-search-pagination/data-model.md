# Data Model: Course Browse Search, Filter, and Pagination

**Feature**: specs/019-course-search-pagination/spec.md  
**Date**: 2025-07-31

## Existing Entities (No Changes Required)

### Course (Domain Entity — `LibreLms.Modules.Catalog.Domain.Course`)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | `Guid` | Primary Key | Auto-generated |
| Title | `string` | Required, Max 200 | Full-Text Search indexed |
| ShortDescription | `string` | Required, Max 500 | Display in course cards |
| FullDescription | `string` | Required, Max 4000 | Display on detail page |
| Category | `string` | Required, Max 100 | Filter criterion |
| Duration | `string` | Required, Max 50 | Display in course cards |
| OrganizationId | `Guid` | Required | Ownership scoping |
| CreatedAt | `DateTimeOffset` | Required | Sort tiebreaker |

**Existing Indexes**:
- `PK_Courses` on `Id`
- `UK_Title_OrganizationId` on `(Title, OrganizationId)` — unique, used as FTS key index

## Database-Level Additions

### Full-Text Catalog

```
Name: LearningLmsFtCatalog
Type: Default full-text catalog for the database
Scope: One-time creation (reused by all FTS indexes in the database)
```

### Full-Text Index

```
Target: Courses.Title
Key Index: UK_Title_OrganizationId (existing)
Catalog: LearningLmsFtCatalog
Change Tracking: AUTO
Language: 1033 (English)
```

### Stored Procedure: `BrowseCourses`

**Purpose**: Combined search, filter, org-scoping, and pagination in a single T-SQL call.

**Parameters**:

| Name | Type | Default | Description |
|------|------|---------|-------------|
| `@SearchTerm` | `NVARCHAR(200)` | `NULL` | Search term for FTS on Title |
| `@Category` | `NVARCHAR(100)` | `NULL` | Exact category match |
| `@PageSize` | `INT` | `12` | Rows per page |
| `@PageNumber` | `INT` | `1` | 1-indexed page number |
| `@OrganizationId` | `UNIQUEIDENTIFIER` | `NULL` | If provided, scope to org-visible courses |

**Result Sets**:

Result Set 1 — Page of courses:
| Id | Title | ShortDescription | Category | Duration |
|----|-------|------------------|----------|----------|

Result Set 2 — Total count:
| TotalCount |
|------------|

**Search Logic**:
1. If FTS index exists and `@SearchTerm` is not empty → `CONTAINS(Title, @SearchTerm)`
2. If FTS not available or fallback needed → `Title LIKE '%' + @SearchTerm + '%'`
3. Category filter → `Category = @Category` (if not NULL/empty)
4. Org scope → applied by caller through visibility filtering (not in this SP; the Razor page model handles org-scoped visibility)
5. Pagination → `ORDER BY Title ASC OFFSET ... FETCH NEXT ...`

## Query Parameters (Transient — No Persistence)

### BrowseCoursesQuery

| Field | Type | Default | Source |
|-------|------|---------|--------|
| Search | `string?` | `null` | Search box input |
| Category | `string?` | `null` | Category dropdown |
| PageNumber | `int` | `1` | Pagination controls / URL |
| PageSize | `int` | `12` | Configuration |

## State Transitions

No state machine changes. This feature is read-only (query/filter/paginate). No course data is created, modified, or deleted.
