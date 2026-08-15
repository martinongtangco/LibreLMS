# Tasks: Fix Course Page Pagination

## Story 1 - Fix `page` Query Binding (P1)

- [x] T1.1 Identify root cause: `int page = 1` inferred as Form binding source; query-string `page` never bound (verified at runtime)
- [ ] T1.2 Add `[FromQuery]` to `page` parameter in `OnGetCourseListAsync`
- [ ] T1.3 Remove redundant filter-reset block; keep `Math.Max(1, Math.Min(page, totalPages))` capping

## Story 2 - Hide Boundary Buttons (P2)

- [ ] T2.1 Render Previous button only when `pageNumber > 1` in `_Pagination.cshtml`
- [ ] T2.2 Render Next button only when `pageNumber < totalPages` in `_Pagination.cshtml`

## Story 3 - E2E Tests

- [ ] T3.1 Add pagination locators/actions to `CourseBrowsePage.ts`
- [ ] T3.2 Add `11-course-pagination.spec.ts`: next shows 13th course, prev/next hidden at boundaries, back navigation works, no duplicates across pages

## Verification (Principle XIII)

- [ ] T4.1 `dotnet build` passes (show output)
- [ ] T4.2 App restarted, `Now listening` + HTTP 200 (show output)
- [ ] T4.3 `curl` proof: `page=2` → 1 course; `page=999` → capped last page
- [ ] T4.4 Playwright new suite passes; 02-course-browse has no new failures (show output)
- [ ] T4.5 Merge to master, rebuild, restart, re-run Playwright (show output)
