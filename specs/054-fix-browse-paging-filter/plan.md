# Implementation Plan: Browse Paging/Count Visibility Fix

**Branch**: `bug/054-fix-browse-filter-after-paging` | **Date**: 2026-09-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/054-fix-browse-paging-filter/spec.md`

**ADR**: [docs/adr/0012](../../../docs/adr/0012-browsecourses-visible-set-json-parameter.md) —
the visible set moves into `BrowseCourses` as a JSON parameter (OPENJSON),
NULL = legacy unfiltered (written BEFORE code, per Principle IV).

## Summary

`BrowseCourses` gains `@VisibleCourseIds NVARCHAR(MAX) = NULL`; the
predicate
`AND (@VisibleCourseIds IS NULL OR c.Id IN (SELECT [value] FROM
OPENJSON(@VisibleCourseIds) WITH ([value] UNIQUEIDENTIFIER)))` is added to
both the row SELECT and the COUNT SELECT (new Catalog migration, house
idempotent DROP+CREATE + `.Designer.cs`). `BrowseAsync` serializes the
visible set (null → NULL param, empty → `[]`) and drops the in-memory
filter. `Pages/Courses/Index.cshtml.cs` resolves the visible set once per
request (list + category dropdown; clamp re-fetch reuses it). Red first:
Catalog.Tests (SP + service) and an E2E pagination block in
`19-course-visibility.spec.ts`.

## Technical Context

**Language/Version**: C# / .NET 10; T-SQL (SQL Server 2022)
**Primary Dependencies**: none new
**Storage**: MSSQL — one SP re-created via migration; Valkey untouched
**Testing**: new `tests/Catalog.Tests/BrowseCoursesVisibilityTests.cs`
(real MSSQL, house marker pattern) + `19-course-visibility.spec.ts` E2E
extension + full gate 2
**Constraints**: 177-passed + 1-skip E2E baseline and 170-unit baseline stay
green; `NULL` param default keeps all other callers behavior-identical

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I/II**: one parameter, one predicate, one migration; the C# filter is
  deleted, not augmented. Explainable: "the SP doesn't page and count what
  you can't see." ✅
- **III**: Catalog module owns the SP + service; the page model (Host)
  only re-plumbs a value it already computes. No cross-module contract
  change. ✅
- **IV**: ADR 0012 written and committed with this plan (JSON vs TVP
  decision). ✅
- **XIII**: red-verify (unit + E2E against the post-paging-filter build)
  before the fix; gates with evidence. ✅
- **XV**: the SP migration needs the `.Designer.cs` partial or EF silently
  skips it (spec 052 gotcha) — check `dotnet ef migrations list` after
  authoring. ✅
- **XVI**: independent verification before merge, as with every loop item. ✅

## Phase 0 — Research

See [research.md](research.md): current SP shape, caller inventory
(only `Pages/Courses/Index.cshtml.cs` calls `BrowseAsync`), E2E course
creation path (admin UI form — no API), test-data math for the pagination
red (24 total / 8 visible design).

## Phase 1 — Design

1. Migration
   `src/Host/Migrations/Catalog/20260914100000_AddVisibleFilterToBrowseCoursesProcedure.cs`
   (+ `.Designer.cs`): idempotent DROP+CREATE of `BrowseCourses` with the
   7th parameter + predicate in both SELECTs; `Down` restores the pre-054
   SP verbatim.
2. `CourseCatalogService.BrowseAsync`: serialize `visibleCourseIds`
   (HashSet) → `@VisibleCourseIds` (`null` → DBNull, empty → `[]`, else
   JSON array of GUID strings); delete the in-memory filter.
3. `Pages/Courses/Index.cshtml.cs`: `ResolveVisibleSetAsync()` (null when
   unscoped); `GetPagedCourses(..., visibleSet)` and
   `GetCategoriesAsync(visibleSet)` take it; `OnGetAsync` +
   `OnGetCourseListAsync` each resolve once.

## Phase 2 — Tasks

See [tasks.md](tasks.md).

## Local evidence trail (gate 2)

- Red: new unit tests fail against current code (TotalCount unfiltered);
  E2E pagination block fails (pager advertises an empty page / inflated
  total).
- Green: migration applied (SP has 7 params — `sp_help BrowseCourses`),
  unit + E2E pass, full gate 2 (170 units baseline + new tests, 177+1 E2E
  baseline + new tests), filler-clean before E2E.
