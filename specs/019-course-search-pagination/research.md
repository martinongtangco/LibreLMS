# Research: Course Browse Search, Filter, and Pagination

**Feature**: specs/019-course-search-pagination/spec.md  
**Date**: 2025-07-31

## Decisions

### Decision 1: SQL Server Full-Text Search (FTS) for Course Title Search

**Decision**: Use SQL Server Full-Text Search with a Full-Text Catalog and Full-Text Index on the `Courses.Title` column.

**Rationale**:
- SQL Server 2022 (used in this project per `docker-compose.yml`) has mature FTS support
- FTS provides tokenized, language-aware search with ranking (relevance scores) — superior to `LIKE '%term%'` for both accuracy and performance
- FTS avoids full table scans; uses index seeks for search terms
- Supports the user's explicit requirement for T-SQL search
- The `CONTAINS` predicate in T-SQL supports inflectional forms, thesaurus expansion, and weighted ranking

**Alternatives considered**:
- `LIKE '%term%'`: Simple but causes full table scans; no ranking; poor performance at scale
- LINQ `Contains()`: Translates to `LIKE` under EF Core; same performance issues
- External search engine (Elasticsearch): Overkill for a single-column search on a table that will not exceed 100K rows

**Implementation approach**:
1. Create a Full-Text Catalog on the `LearningLms` database
2. Create a Full-Text Index on `Courses.Title` using the existing unique index `(Title, OrganizationId)` as the key index
3. Write a T-SQL stored procedure `SearchCourses` that uses `CONTAINS` with pagination (`OFFSET/FETCH`)
4. Fall back to `LIKE`-based search if the FTS index is not yet populated (graceful degradation)

### Decision 2: Pagination via T-SQL OFFSET/FETCH

**Decision**: Use T-SQL `ORDER BY ... OFFSET @PageSize * (@PageNumber - 1) ROWS FETCH NEXT @PageSize ROWS ONLY` for pagination.

**Rationale**:
- Native SQL Server pagination (available since SQL Server 2012) — no application-side slicing
- Combined with FTS search in a single stored procedure call — one round-trip for search + filter + paginate
- `OFFSET/FETCH` is optimized by the query planner for indexed columns
- Works naturally with the `CONTAINS` predicate for FTS search

**Alternatives considered**:
- Keyset pagination (`WHERE id < last_id`): Faster for deep pagination but requires cursor state; more complex for filter changes
- Application-side `Skip().Take()`: Loads all matching rows into memory; violates the user's performance requirement

### Decision 3: Stored Procedure for Combined Search, Filter, and Pagination

**Decision**: Create a single stored procedure `BrowseCourses` that handles search, category filtering, org-scoping, and pagination in one database call.

**Rationale**:
- One round-trip to the database regardless of filter complexity
- The stored procedure returns both the page of results AND the total count (for pagination controls) using a second result set
- Keeps T-SQL logic centralized and testable
- The Catalog module's `CourseCatalogService` calls the stored procedure via EF Core's `FromSqlRaw()`

**Alternatives considered**:
- Separate procedures for search and count: Two round-trips; more complex orchestration
- Raw SQL in C#: Scatters SQL across the codebase; harder to maintain and version

### Decision 4: HTMX Page Partial Update for Pagination

**Decision**: Extend the existing HTMX pattern to handle pagination — the course list partial will include pagination controls that trigger `hx-get` requests for the next/previous page.

**Rationale**:
- The project already uses HTMX 2.0.4 for partial page updates on the Courses page
- Pagination controls can swap only the `#course-list` div — no full page reload
- Query parameters (`search`, `category`, `page`) flow naturally through HTMX's `hx-get` with `hx-include`
- Consistent with the existing search/filter interaction pattern

**Alternatives considered**:
- Full page reloads: Simpler but slower; inconsistent with existing HTMX pattern
- Client-side JavaScript pagination: Loads all data upfront; defeats the purpose of server-side pagination

### Decision 5: Fix HTMX Search/Filter Bug — Missing Parameter Binding

**Decision**: Fix the HTMX search input to include both `search` AND `category` parameters in the request, and vice versa for the category dropdown.

**Root cause**: The current `hx-get` on the search input only sends its own value. When the user types in search while a category is selected, the category is lost (and vice versa). Both inputs need to include all filter parameters.

**Rationale**:
- The fix uses `hx-include` to include sibling form elements in the HTMX request
- Wrapping both inputs in a `<form>` with `hx-include` ensures all parameters are sent together
- This is the minimal change to fix the broken behavior without restructuring the page

**Alternatives considered**:
- Separate handlers for each filter: Would require server-side state management; over-engineered
- JavaScript event handlers: Adds complexity; HTMX already handles parameter inclusion

### Decision 6: Page Size Default

**Decision**: Default to 12 courses per page.

**Rationale**:
- The existing `_CourseList.cshtml` uses a `.metric-cards` grid layout (implied by card rendering)
- 12 cards fits well in a 3x4 or 4x3 grid on desktop without excessive scrolling
- Matches the assumption in the spec

## Database Schema Impact

### New Database Objects (no changes to existing tables)

```sql
-- Full-Text Catalog (one per database, reused)
CREATE FULLTEXT CATALOG LearningLmsFtCatalog AS DEFAULT;

-- Full-Text Index on Courses.Title
CREATE FULLTEXT INDEX ON Courses(Title)
KEY INDEX UK_Title_OrganizationId  -- existing unique index
CATALOG LearningLmsFtCatalog;

-- Stored procedure: combined search, filter, pagination
CREATE PROCEDURE BrowseCourses
    @SearchTerm NVARCHAR(200) = NULL,
    @Category NVARCHAR(100) = NULL,
    @OrganizationIdScope UNIQUEIDENTIFIER = NULL,  -- NULL = no org scoping
    @VisibleCourseIds UNIQUEIDENTIFIER[] = NULL,    -- TVP for org visibility
    @PageSize INT = 12,
    @PageNumber INT = 1
AS
-- Returns Result Set 1: courses for the page
-- Returns Result Set 2: total count
```

Note: The `Courses.Title` column already has a unique index with `OrganizationId` (`UK_Title_OrganizationId`) which serves as the key index for the Full-Text Index.

## Module Boundary Considerations

- **Catalog module**: The `CourseCatalogService` gains a new `BrowseAsync` method that calls the stored procedure. The method signature takes pagination parameters.
- **No new contracts needed**: The Browse Courses page is in the Host project (Razor Pages), which already references the Catalog module directly. No cross-module boundary is crossed.
- **Host project**: The `CourseIndexModel` gains `PageNumber` and `PageSize` properties and updates the HTMX handlers to pass all filter parameters.
- **Infrastructure**: The `CatalogDbContext` may need a raw SQL call to the stored procedure. EF Core's `Database.ExecuteSqlRaw()` or `FromSqlRaw()` is used.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| FTS index population delay | Search returns no results immediately after course creation | Use `CHANGE_TRACKING AUTO` on FTS index; accept eventual consistency (seconds) |
| MSSQL 2022 Express FTS limitations | FTS may not be available in Express edition | Graceful fallback to `LIKE` query; detect FTS availability at startup |
| Org visibility + FTS interaction | Org-scoped results must be filtered before FTS ranking | Apply visibility filter in WHERE clause alongside CONTAINS |
