# Implementation Plan: Fix Admin "Courses" Nav Highlight

## Changes Required

### 1. Replace substring matching with section-prefix matching
- In the active-link detection IIFE in `src/Host/Pages/Shared/_Layout.cshtml`, change the
  `linkMap` keys from full page targets to **section paths** (`/MyCourses`, `/Courses`,
  `/Admin/Dashboard`, `/Admin/Courses`, `/Admin/Enrollments`, `/Admin/Learners`,
  `/Admin/Organizations`, `/Admin/Upload`) and replace the `path.indexOf(urlPage) !== -1`
  test (plus the dead lowercase fallback branch) with:
  - exact match: `path === urlPage`, or
  - section-prefix match: `path.length > urlPage.length && path.indexOf(urlPage + '/') === 0`
- When multiple keys match, keep the **longest** key (most specific section) instead of
  `break`-ing on the first insertion-order hit.
- Keep the same `data-page` values and lookup; no CSS, no other JS (FR-007).
- File: `src/Host/Pages/Shared/_Layout.cshtml`

### 2. E2E tests (Principle XIII)
- New `tests/Playwright.Tests/tests/17-nav-active-highlight.spec.ts`:
  - SuperUser login (reuse the `adminApi`-style login pattern from 16-admin-pagination)
  - Story 1: `/Admin/Courses/Index` → `[data-page="admin-courses"]` has `active`,
    `[data-page="browse-courses"]` does not, and it is the only active nav link
  - Story 2: create 11 marker-prefixed filler courses via the admin API (self-contained,
    pattern of 16-admin-pagination; delete in afterAll), click "Next →", assert the admin
    Courses link is still active on page 2 and Browse Courses is not; also assert
    `/Admin/Enrollments/Index?pageNumber=2` and `/Admin/Learners/Index?pageNumber=2` keep
    their own links active (pathname-level checks, no data needed)
  - Story 3: `/Courses/Detail/{id}` (route value; filler course) → Browse Courses active;
    `/Admin/Courses/Edit?courseId=...` (filler course) → admin Courses active;
    `/Account/Login` (logged-out) and `/Account/Profile` (logged in) → no active nav link
- Reuse existing page objects (`AdminCoursesPage`) and `testUsers`.
- Files: `tests/Playwright.Tests/tests/17-nav-active-highlight.spec.ts` (new),
  `tests/Playwright.Tests/pages/AdminCoursesPage.ts` (extend only if a locator is missing)

## Verification (Principle XIII)
1. `dotnet build` succeeds — show output
2. App restarted (restart-host-app skill), `Now listening` + HTTP 200 — show output
3. Playwright: new 17-nav-active-highlight suite passes — show output
4. Regression: Playwright specs 02, 04, 05, 07, 10, 16 pass — show output
5. Merge to master, rebuild, restart, re-run the same Playwright set — show output
