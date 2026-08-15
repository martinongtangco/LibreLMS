# Implementation Plan: Fix Course Page Pagination

## Changes Required

### 1. Fix `page` binding in the HTMX handler (Defect 1)
- Add `[FromQuery]` to the `page` parameter of `OnGetCourseListAsync` so the query-string
  value sent by HTMX `hx-get` is bound (ASP.NET Core infers optional value-type parameters
  as `Form` source, which is why `page` was silently ignored).
- Remove the now-redundant `if (search != null || category != null)` reset block: filter
  changes already arrive as `page=1` (from the `#page-reset` hidden field in the search
  input / category select `hx-include`), and the `Math.Max(1, Math.Min(page, totalPages))`
  cap already handles it.
- File: `src/Host/Pages/Courses/Index.cshtml.cs`

### 2. Hide boundary pagination buttons (Defect 2)
- Render the Previous button only when `pageNumber > 1`.
- Render the Next button only when `pageNumber < totalPages`.
- Keep the page indicator span always rendered.
- File: `src/Host/Pages/Shared/_Pagination.cshtml`

### 3. E2E tests (Principle XIII)
- Extend `tests/Playwright.Tests/pages/CourseBrowsePage.ts` with pagination locators and
  actions (next/previous buttons, page indicator text, click helpers with HTMX settle waits).
- Add `tests/Playwright.Tests/tests/11-course-pagination.spec.ts` covering:
  - Next on page 1 → 13th course visible, "Page 2 of 2 (13 total)"
  - Previous hidden on page 1; Next hidden on last page; both visible on middle pages
  - Previous on page 2 → back to 12 courses, "Page 1 of 2 (13 total)"
  - No duplicate/missing courses across pages (union of both pages = 13 unique titles)
- File: `tests/Playwright.Tests/...`

## Verification
- `dotnet build` succeeds
- App restarted and responds on http://localhost:5000
- `curl` checks: `?handler=CourseList&page=2` returns 1 course; `page=999` returns the last
  valid page (capping intact)
- Playwright: new pagination suite passes; 02-course-browse suite shows no new failures
  (2 pre-existing seed-count failures from spec 027 notes remain)
