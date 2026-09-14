# Quickstart: Browse Paging/Count Visibility Fix

**Branch**: `bug/054-fix-browse-filter-after-paging` | **Date**: 2026-09-14

## What this changes

1. `BrowseCourses` SP gains `@VisibleCourseIds NVARCHAR(MAX) = NULL`
   (JSON array of GUIDs; `OPENJSON`); predicate added to the row SELECT and
   the COUNT SELECT — filtering, paging and counting agree (ADR 0012).
   New Catalog migration (idempotent DROP+CREATE + `.Designer.cs`).
2. `CourseCatalogService.BrowseAsync` passes the set as JSON
   (null → `NULL`, empty → `[]`) and the in-memory post-paging filter is
   deleted (the empty-set edge is fixed for free).
3. `Pages/Courses/Index.cshtml.cs` resolves the visible set once per
   request (was: twice on a full page).

## Verify

```sh
# unit (env sourced in the same shell)
source .../scratchpad/run-env.sh && set +H
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --nologo

# SP shape after app restart
EXEC sp_help BrowseCourses   # 7 params; #7 @VisibleCourseIds nvarchar(max)

# E2E (in-container)
docker exec sbxtestwspeckit-devcontainer-1 sh -c \
  "cd /workspace/tests/Playwright.Tests && CI=1 PLAYWRIGHT_BROWSERS_PATH=/ms-playwright npx playwright test --reporter=line"
```

## Expected

- `BrowseCoursesVisibilityTests`: SP + service totals equal the visible
  count; `NULL` = legacy unfiltered; `[]` = 0.
- `19-course-visibility` pagination block: total = visible (8), one
  advertised page, no empty page (24 total / 8 visible fixture).
- Baselines: units 170 + new; E2E 177 + 1 skip + new; filler-clean before
  E2E (Catalog perf seed).
