# Tasks: Browse Paging/Count Visibility Fix

**Branch**: `bug/054-fix-browse-filter-after-paging` | **Plan**: [plan.md](plan.md) | **ADR**: [0012](../../../docs/adr/0012-browsecourses-visible-set-json-parameter.md)

## US1: Red — the defect is reproduced (before the fix)

- [ ] T001 [US1] `tests/Catalog.Tests/BrowseCoursesVisibilityTests.cs`
      (house marker pattern, real MSSQL): 13 marker courses; visible set =
      8 — page rows ⊆ visible, `TotalCount` == 8, pageSize 5 → page 1 full
      (5), page 2 = 3-item remainder, page 3 empty; `NULL` param → 13 (legacy);
      `[]` → 0 rows / 0 count; service-level `BrowseAsync` (null set →
      unfiltered, empty set → 0). Run against current code: RED (filtered
      TotalCount returns 13; `[]` returns 13 — the empty-set edge).
- [ ] T002 [US1] `19-course-visibility.spec.ts`: new pagination test —
      child org + verified learner (per-run unique), 14 admin-UI courses
      `ZZ Pag <ts> 01..14` (root-owned), hide the 14 + 2 seeded from the
      child org → 8 visible / 24 total. Assert: reported total == 8, pager
      advertises 1 page, no empty page. Run: RED (total 24 / page 2
      advertised and empty pre-fix).

## US2: The fix — filter, page and count in one place

- [ ] T003 [US2] Migration
      `src/Host/Migrations/Catalog/20260914100000_AddVisibleFilterToBrowseCoursesProcedure.cs`
      + `.Designer.cs` (copy the previous Catalog Designer body — model
      unchanged): `BrowseCourses` recreated with
      `@VisibleCourseIds NVARCHAR(MAX) = NULL`; predicate
      `AND (@VisibleCourseIds IS NULL OR c.Id IN (SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM
      OPENJSON(@VisibleCourseIds)))` on the
      row SELECT and the COUNT SELECT; `Down` restores the pre-054 SP
      verbatim. Verify: `dotnet ef migrations list --context
      CatalogDbContext` lists the new id (Designer gotcha, spec 052).
- [ ] T004 [US2] `CourseCatalogService.BrowseAsync`: serialize
      `visibleCourseIds` → `@VisibleCourseIds` (null → `DBNull`, empty →
      `[]`, else JSON array of GUID strings); delete the in-memory filter
      and its comment.
- [ ] T005 [US2] `Pages/Courses/Index.cshtml.cs`: resolve the visible set
      once per request (`OnGetAsync`: list + categories share it;
      `OnGetCourseListAsync`: clamp re-fetch reuses it). 2×
      `GetVisibleCoursesAsync` → 1× per full page.
- [ ] T006 [US2] Restart the app; `sp_help BrowseCourses` shows 7 params
      (7th `@VisibleCourseIds nvarchar(max)`); re-run T001: GREEN.

## US3: Gates

- [ ] T007 [US3] Gate 1: `dotnet build LibreLms.slnx` 0 errors; app
      restarted in-container (`Now listening` + 302 probe).
- [ ] T008 [US3] Gate 2: ArchitectureTests + full unit suite (env sourced
      in-shell) green (170 baseline + new); filler-clean after the last
      unit run; re-run T002 E2E: GREEN; full Playwright serial green
      (177 + 1 skip baseline + new test).
- [ ] T009 [US3] Independent verification (Constitution XVI): fresh
      no-context subagent, clean detached worktree on this branch —
      build, units, app readiness, filler-clean, Playwright. Verdict GREEN.
- [ ] T010 [US3] Gate 3 (post-merge, on master): rebuild, restart,
      re-run gate 2; mark all tasks `[X]`, set spec Status to Complete,
      commit F
