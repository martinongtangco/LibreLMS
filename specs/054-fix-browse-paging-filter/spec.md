# Bug Fix Specification: Course Browse Filters After Paging, So the Page Count Is Wrong

**Feature Branch**: `bug/054-fix-browse-filter-after-paging`

**Created**: 2026-09-14

**Status**: Complete (2026-09-14)

**Input**: Hardening-loop handoff, item 6:

> Course browse applies the organization visibility filter after the database
> has already paged the results, and then reports the unfiltered total. In
> `src/Modules/Catalog/Application/CourseCatalogService.cs`, `BrowseAsync`
> receives a page of rows from the `BrowseCourses` stored procedure, filters
> hidden courses out in memory, and returns that shortened list together with
> the procedure's own `totalCount`. The count therefore includes courses the
> caller cannot see. An organization that hides courses gets pages containing
> fewer than `pageSize` items and a pager advertising pages that render
> empty. Spec 047 made `IsHidden` take effect on the row list but did not
> carry it through to paging or counting.
>
> Additionally, `Pages/Courses/Index.cshtml.cs` calls
> `CourseVisibilityService.GetVisibleCoursesAsync` twice per request — once in
> `GetPagedCourses` and again in `GetCategoriesAsync` — fetching the same full
> catalog both times.
>
> Fix: push the visible-course-id set into the `BrowseCourses` procedure, as a
> table-valued parameter or a JSON array parameter, so filtering, paging and
> counting all happen in one place and agree. Resolve the visible set once per
> request and reuse it for both the list and the category dropdown.

## The Defect (verified against the code)

`CourseCatalogService.BrowseAsync` (src/Modules/Catalog/Application/
CourseCatalogService.cs):

1. Calls the `BrowseCourses` SP with 6 parameters (search, category,
   pageSize, pageNumber, sortBy, sortDirection). The SP pages and counts
   **all** matching courses — visibility is unknown to it.
2. Reads the page rows + the SP's `TotalCount` (unfiltered).
3. Filters the **page rows** by `visibleCourseIds` in memory ("avoids TVP
   complexity" — the original comment), and returns the filtered rows with
   the **unfiltered** `TotalCount`.

Consequences (all reproduced by construction, red-verified before the fix):

- The reported total includes hidden courses → the pager advertises
  `ceil(unfiltered / pageSize)` pages.
- A page near the end contains fewer than `pageSize` items (the hidden rows
  that filled it are stripped after the fact).
- Advertised pages can render **empty** (every row in the unfiltered page is
  hidden). `OnGetCourseListAsync` even carries a clamp workaround for
  exactly this ("past the last page" → re-fetch the last page), which proves
  the symptom was known.
- Edge: an org whose visible set is **empty** sees *everything* (the in-memory
  filter only applies when the set is non-empty).
- `Pages/Courses/Index.cshtml.cs` resolves `GetVisibleCoursesAsync` (full
  catalog fetch) twice per full-page request (`GetPagedCourses` +
  `GetCategoriesAsync`).

## Fix

1. **Push the visible set into the SP** (ADR 0012 — JSON array parameter):
   `BrowseCourses` gains a 7th parameter
   `@VisibleCourseIds NVARCHAR(MAX) = NULL` — a JSON array of course-id
   GUIDs (e.g. `["<guid>", ...]`). Both the row SELECT and the COUNT SELECT
   gain
   `AND (@VisibleCourseIds IS NULL OR c.Id IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM
   OPENJSON(@VisibleCourseIds)))`.
   Semantics: `NULL` = no visibility restriction (unauthenticated/no-org
   callers — legacy behavior, unchanged); a JSON array = rows and count
   restricted to the set; `[]` = nothing visible. Filtering, paging and
   counting now happen in one place and agree by construction.
   New EF migration under `src/Host/Migrations/Catalog/` following the
   `20260822123232_ExtendBrowseCoursesWithSort` pattern (idempotent
   DROP+CREATE; `Down` restores the pre-054 SP) — including the
   `.Designer.cs` partial (house gotcha: without it EF silently skips the
   migration).
2. **Service**: `BrowseAsync` serializes the `visibleCourseIds` set to the
   JSON parameter (`null` set → `NULL` param; empty set → `[]`) and drops
   the in-memory filter entirely (the empty-set edge is fixed for free).
3. **Page model**: resolve the visible course set **once per request** and
   reuse it: `OnGetAsync` passes the same set to the list and to the
   category dropdown (2× `GetVisibleCoursesAsync` → 1×);
   `OnGetCourseListAsync` resolves once and reuses it across the possible
   clamp re-fetch. The HTMX clamp stays (harmless safety net; with the
   count fixed, out-of-range pages can no longer arise from visibility).

## Done when

- With hidden courses present: every full page contains exactly `pageSize`
  items; the reported total equals the number of visible courses; no page in
  the pager renders empty.
- `tests/Catalog.Tests` covers the SP + service with a visible-id set
  (filtered rows, filtered count, `NULL` = unfiltered legacy, `[]` = empty).
- `19-course-visibility.spec.ts` extends to pagination with hidden courses
  (E2E red before the fix).
- Gates green, independent verification green, master (house loop).

## Out of scope

- The HTMX page-clamp logic (kept; re-verified benign after the fix).
- Admin surfaces (org-scoped already by spec 052).
- Caching the visible set across requests (out of scope; per-request
  resolution only).
