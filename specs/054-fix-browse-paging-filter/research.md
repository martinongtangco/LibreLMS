# Research: Browse Paging/Count Visibility Fix

**Date**: 2026-09-14 | **Branch**: `bug/054-fix-browse-filter-after-paging`

## 1. Current SP shape (`BrowseCourses`)

Created by `20260822123232_ExtendBrowseCoursesWithSort` (Catalog context):
6 params — `@SearchTerm NVARCHAR(200) = NULL`, `@Category NVARCHAR(100) =
NULL`, `@PageSize INT = 10`, `@PageNumber INT = 1`, `@SortBy NVARCHAR(20) =
N'title'`, `@SortDirection NVARCHAR(4) = N'asc'`. Result set 1: 6 columns
(Id, Title, ShortDescription, Category, Duration, OrganizationId) paged via
OFFSET/FETCH with a 7-way CASE sort + `c.Id ASC` tiebreak. Result set 2:
`SELECT COUNT(*) AS TotalCount` over the same (unfiltered) predicate.
House migration pattern: `IF OBJECT_ID(...) IS NOT NULL DROP PROCEDURE` +
CREATE, in both Up and Down.

## 2. Caller inventory

`CourseCatalogService.BrowseAsync` is called from exactly one place:
`src/Host/Pages/Courses/Index.cshtml.cs` (learner catalog, `OnGetAsync` +
HTMX `OnGetCourseListAsync`). Two call shapes: with `visibleCourseIds`
(authenticated user with an org claim) and without (unauthenticated / no
org — shows all). No other project references the SP. Adding a defaulted
7th parameter is therefore invisible to every hypothetical other caller.

## 3. The double fetch

`OnGetAsync` → `GetPagedCourses` (calls `GetVisibleCoursesAsync(orgId,
scope)`) + `GetCategoriesAsync` (calls the same method again). One full
page request = two full-catalog visibility reads. The fix lifts the
resolution to the handler and passes the set down; the HTMX handler
resolves once and reuses it across the (rare) clamp re-fetch.

## 4. E2E test-data math (the red design)

pageSize = 12 (fixed in the page model). Seeded catalog: 10 root-owned
courses (child orgs inherit all of them). To exercise "full page exactly
pageSize", "total == visible", and "no empty advertised page" in one E2E:

- Create **14** extra root-owned courses via the admin UI (no course
  creation API exists — `POST /api/courses` is absent; the admin form is
  Title/ShortDescription/FullDescription/Category/Duration, ScormMode
  defaults to none). Total visible-eligible: **24**.
- Hide **16** of them from the child org (the 14 new — titles
  `ZZ Pag <ts> 01..14` so they sort to the end — plus 2 seeded).
  Visible: **8**.
- Pre-fix: SP total = 24 → pager advertises `ceil(24/12) = 2` pages; page 1
  = 12 unfiltered rows, in-memory filter strips the hidden → 8 items
  (partial page); page 2 = rows 13–24, all hidden → renders **empty**
  (the clamp re-fetches page 2 and returns nothing).
  Red assertions: total text == 8 (not 24), pager shows 1 page, no empty
  page reachable.
- Post-fix: SP total = 8 → 1 page, 8 items; everything agrees.

(The 8-visible keeps the post-fix page 1 partial too — the "full page
exactly pageSize" clause is covered by the unit tests, where the marker
set is sized to fill pages exactly; the E2E covers the three user-visible
symptoms.)

## 5. Unit test design (house pattern)

New `tests/Catalog.Tests/BrowseCoursesVisibilityTests.cs` (marker `AdmPg054C`,
same direct-INSERT/cleanup pattern as `BrowseCoursesSortTests`):

- 13 marker courses (fills exactly one 13-row page at pageSize 13; and
  exercises multi-page at pageSize 5: 3 full pages + 1 of 3).
- Visible set = 8 of the 13:
  - rows on the requested page are a subset of the visible set;
  - `TotalCount` == 8 (the visible count) — RED pre-fix (returns 13);
  - `pageSize` 5: page 1 full (5 items), page 2 = the 3-item remainder,
    page 3 empty; no page renders a row past the visible set.
- `NULL` param (legacy): TotalCount == 13 (unfiltered behavior preserved).
- `[]` (empty set): 0 rows, TotalCount == 0 — the org-hides-everything
  edge the old in-memory filter got wrong.
- Service level (`BrowseAsync` with the HashSet): same filtered total
  assertion through the service path (null set → unfiltered; empty set →
  0).

## 6. Migration gotchas (from spec 052)

- The `.Designer.cs` partial is REQUIRED (carries `[Migration]` +
  `[DbContext(typeof(CatalogDbContext))]`); without it EF silently skips
  the migration. Model is unchanged → Designer body = copy of the previous
  Catalog migration's Designer.
- Verify after authoring: `dotnet ef migrations list --context
  CatalogDbContext` shows the new id; after app restart,
  `sp_help BrowseCourses` shows 7 params with `@VisibleCourseIds
  nvarchar(max)`.
