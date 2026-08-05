# Tasks: Course Browse Search, Filter, and Pagination

**Input**: Design documents from `/specs/019-course-search-pagination/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — user requested performance testing tests in addition to integration tests.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Database Objects and Migration)

**Purpose**: Create the Full-Text Search index, stored procedure, and EF Core migration — the database foundation everything depends on.

- [ ] T001 Create EF Core migration `AddFullTextIndexAndBrowseProcedure` in `src/Modules/Catalog/Infrastructure/Migrations/` with raw SQL to create Full-Text Catalog `LearningLmsFtCatalog` and Full-Text Index on `Courses.Title` using key index `UK_Title_OrganizationId` with `CHANGE_TRACKING AUTO`

- [ ] T002 Add T-SQL stored procedure `BrowseCourses` in the migration's `Up()` method with parameters `@SearchTerm NVARCHAR(200)`, `@Category NVARCHAR(100)`, `@PageSize INT = 12`, `@PageNumber INT = 1`; Result Set 1 returns `Id, Title, ShortDescription, Category, Duration` for the page; Result Set 2 returns `TotalCount`; use `CONTAINS(Title, @SearchTerm)` for FTS search with fallback to `LIKE '%' + @SearchTerm + '%'` when FTS is unavailable; use `ORDER BY Title ASC OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY` for pagination

- [ ] T003 Add the inverse SQL in the migration's `Down()` method to drop the stored procedure, Full-Text Index, and Full-Text Catalog

---

## Phase 2: Foundational (Application Service Layer)

**Purpose**: Add the `BrowseAsync` method to `CourseCatalogService` so the database objects are callable from C# code. No user story can work without this.

**CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T004 Add `record BrowseResult(IEnumerable<CourseItemDto> Items, int TotalCount, int PageNumber, int PageSize)` type in `src/Modules/Catalog/Application/CourseCatalogService.cs` to carry paginated results from the stored procedure

- [ ] T005 Add `BrowseAsync(string? searchTerm, string? category, int pageNumber, int pageSize)` method to `CourseCatalogService` in `src/Modules/Catalog/Application/CourseCatalogService.cs` that calls `BrowseCourses` via `context.Database.GetDbConnection()` + `SqlCommand` with `SqlDataReader` to read both result sets (courses + total count), maps rows to `CourseItemDto` records, and returns `BrowseResult`

**Checkpoint**: Foundation ready — `BrowseAsync` can be called from the Razor Page model. User story implementation can now begin.

---

## Phase 3: User Story 1 + 2 — Search and Category Filter (Priority: P1) 🎯 MVP

**Goal**: Fix the broken search box and category filter so both work correctly via HTMX, backed by the T-SQL stored procedure with Full-Text Search.

**Independent Test**: Type in the search box — only matching courses appear. Select a category — only courses in that category appear. Combined — only courses matching both appear.

### Implementation for US1 + US2

- [ ] T006 [US1] Add `[BindProperty(SupportsGet = true)] public string? Search { get; set; }` and `[BindProperty(SupportsGet = true)] public string? Category { get; set; }` properties (already exist, verify) and call `CourseCatalogService.BrowseAsync(Search, Category, 1, 12)` in `OnGetAsync()` in `src/Host/Pages/Courses/Index.cshtml.cs`; map returned `BrowseResult.Items` to `CourseItem` records for the view; derive `Categories` list from the filtered results

- [ ] T007 [US1] Update `OnGetCourseListAsync` handler in `src/Host/Pages/Courses/Index.cshtml.cs` to accept `string? search`, `string? category` parameters and call `BrowseAsync(search, category, 1, 12)` — replacing the current `GetCoursesAndEnrollments` call — and return the `_CourseList` partial with paginated results

- [ ] T008 [US1] Wrap search input and category select in a single `<form class="filters" id="filter-form">` in `src/Host/Pages/Courses/Index.cshtml`; add `hx-include="#filter-form"` to BOTH the search `<input>` and category `<select>` so each HTMX request sends all filter parameters; set search input `hx-trigger="keyup changed delay:300ms"` and category select `hx-trigger="change"`; both target `hx-target="#course-list"`

- [ ] T009 [P] [US2] Verify the Clear button in `src/Host/Pages/Courses/Index.cshtml` navigates to `/Courses/Index` (no query params) to reset all filters

- [ ] T010 [P] [US2] Update `_CourseList` partial in `src/Host/Pages/Shared/_CourseList.cshtml` to correctly handle empty model (show empty state) and non-empty model (render course cards) — existing logic should suffice, verify it renders `BrowseResult.Items` correctly

**Checkpoint**: At this point, search by title and filter by category both work via HTMX with FTS-backed T-SQL queries. This is the MVP — the Browse Courses page is functional for discovery.

---

## Phase 4: User Story 3 — Pagination (Priority: P2)

**Goal**: Add Previous/Next pagination controls so users can navigate through large course catalogs without scrolling through hundreds of results.

**Independent Test**: With more than 12 courses, only 12 show per page. Clicking Next shows the next 12. Previous/Next buttons are disabled at boundaries.

### Implementation for US3

- [ ] T011 [US3] Add `[BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1` and `public int PageSize { get; set; } = 12` properties and `public int TotalCount { get; set; } = 0` to `CourseIndexModel` in `src/Host/Pages/Courses/Index.cshtml.cs`; pass `PageNumber` and `PageSize` to `BrowseAsync` in both `OnGetAsync` and `OnGetCourseListAsync`

- [ ] T012 [US3] Add `public int PageNumber { get; set; } = 1` parameter to `OnGetCourseListAsync` in `src/Host/Pages/Courses/Index.cshtml.cs` and pass it to `BrowseAsync`; store `BrowseResult.TotalCount` on the model; return both the course list and pagination state to the partial

- [ ] T013 [P] [US3] Create `_Pagination.cshtml` partial in `src/Host/Pages/Shared/_Pagination.cshtml` that accepts `int pageNumber`, `int totalCount`, `int pageSize`, `string? search`, `string? category` as model; computes `totalPages = Math.Ceiling((double)totalCount / pageSize)`; renders Previous button (disabled when `pageNumber == 1`), page indicator text ("Page X of Y"), and Next button (disabled when `pageNumber >= totalPages`); buttons use `hx-get="/Courses/Index?handler=CourseList"` with `hx-include="#filter-form"` and include `page` as a hidden input or query parameter via `hx-vals`

- [ ] T014 [US3] Update `OnGetCourseListAsync` in `src/Host/Pages/Courses/Index.cshtml.cs` to return a combined `PartialViewResult` that renders both `_CourseList` and `_Pagination` together, so the `#course-list` div swap includes pagination controls

- [ ] T015 [US3] Update `_CourseList.cshtml` in `src/Host/Pages/Shared/_CourseList.cshtml` to render the course card grid; pagination controls are now rendered alongside it by the handler (T014)

- [ ] T016 [US3] Update the search input in `src/Host/Pages/Courses/Index.cshtml` to include a hidden `<input type="hidden" name="page" value="1" id="page-reset" class="search-page-reset">` that is included in search/filter HTMX requests (resets to page 1), but NOT included in pagination requests. Use `hx-include="#filter-form #page-reset"` on search and category, and `hx-include="#filter-form"` (without page-reset) on pagination buttons

**Checkpoint**: Pagination works independently. Users can page through courses with Previous/Next controls.

---

## Phase 5: User Story 4 — Combined Search, Filter, and Pagination (Priority: P2)

**Goal**: Ensure search, category filtering, and pagination work together seamlessly — filters persist across page navigation, and filter changes reset to page 1.

**Independent Test**: Apply search + category, navigate to page 2, verify filters persist. Change search term, verify reset to page 1 with new results.

### Implementation for US4

- [ ] T017 [US4] Verify `hx-include="#filter-form"` on pagination buttons in `src/Host/Pages/Shared/_Pagination.cshtml` sends `search` and `category` values along with `page` — so filters persist when navigating pages

- [ ] T018 [US4] Verify that search input and category dropdown requests (with `#page-reset` included) always send `page=1` — so filter changes reset pagination — in `src/Host/Pages/Courses/Index.cshtml`

- [ ] T019 [US4] Update `OnGetCourseListAsync` in `src/Host/Pages/Courses/Index.cshtml.cs` to accept all three parameters (`search`, `category`, `page`) and apply the page-reset logic: if `search` changed or `category` changed, ignore the `page` parameter and use 1; if only `page` changed, use the provided page number

- [ ] T020 [P] [US4] Add edge case handling in `OnGetCourseListAsync` in `src/Host/Pages/Courses/Index.cshtml.cs`: trim whitespace from search term (treat empty-after-trim as null); cap `pageNumber` to `Math.Max(1, Math.Min(pageNumber, totalPages))` to handle invalid page numbers after filter changes

**Checkpoint**: All four user stories are functional. Search, filter, and pagination work together as an integrated system.

---

## Phase 6: Performance Tests

**Purpose**: Validate that the T-SQL stored procedure with Full-Text Search meets the performance requirements (NFR-001 through NFR-003) and scales to large catalogs.

- [ ] T021 [P] Create `PerformanceTests.cs` in `tests/Catalog.Tests/` with xUnit test `Stored_procedure_returns_page_within_200ms_with_100_courses` that seeds 100 courses, calls `BrowseCourses` stored procedure via `SqlConnection` + `SqlCommand` directly (bypassing EF Core for pure DB measurement), measures `Stopwatch.ElapsedMilliseconds`, and asserts result time < 200ms and correct row count (12 per page)

- [ ] T022 [P] Create `PerformanceTests.cs` in `tests/Catalog.Tests/` with xUnit test `Stored_procedure_returns_page_within_500ms_with_1000_courses` that seeds 1,000 courses with varied titles, calls `BrowseCourses` with a search term and category filter, measures execution time, and asserts result time < 500ms and correct total count in Result Set 2

- [ ] T023 [P] Create `PerformanceTests.cs` in `tests/Catalog.Tests/` with xUnit test `Stored_procedure_returns_page_within_1000ms_with_10000_courses` that seeds 10,000 courses, calls `BrowseCourses` with search + category + pagination (page 5 of 12), measures execution time, and asserts result time < 1000ms and exactly 12 rows in Result Set 1

- [ ] T024 Create `PerformanceTests.cs` in `tests/Catalog.Tests/` with xUnit test `Full_text_search_outperforms_like_search` that runs the same search query twice — once with `CONTAINS` (FTS) and once with `LIKE '%term%'` — on a dataset of 1,000+ courses, and asserts the FTS query is measurably faster (or at least not slower) with `Assert.True(ftsTime <= likeTime * 1.5)` (allowing 50% margin for FTS index overhead on small datasets)

- [ ] T025 Create `PerformanceTests.cs` in `tests/Catalog.Tests/` with xUnit test `BrowseAsync_service_method_within_500ms` that calls `CourseCatalogService.BrowseAsync("test", null, 1, 12)` through the DI-resolved service (full stack: C# + EF Core + SQL), measures end-to-end time, and asserts < 500ms to validate the application layer adds acceptable overhead

- [ ] T026 [P] Create `PerformanceTests.cs` in `tests/Catalog.Tests/` with xUnit test `FTS_index_exists_after_migration` that connects to the database and queries `sys.fulltext_indexes` to verify a Full-Text Index exists on the `Courses` table, and queries `sys.fulltext_catalogs` to verify the catalog exists

- [ ] T027 [P] Create `PerformanceTests.cs` in `tests/Catalog.Tests/` with xUnit test `Stored_procedure_fallback_to_like_when_fts_unavailable` that temporarily drops the FTS index, runs `BrowseCourses` with a search term, verifies results are still returned (using LIKE fallback), then recreates the FTS index in the test cleanup

---

## Phase 7: Integration Tests

**Purpose**: Verify the BrowseAsync service method and HTMX endpoint produce correct results for all acceptance scenarios.

- [ ] T028 [P] Create `CourseCatalogSearchTests.cs` in `tests/Catalog.Tests/` with xUnit test `BrowseAsync_returns_matching_courses_by_title` that calls `BrowseAsync("python", null, 1, 12)` and asserts all returned courses have titles containing "python" (case-insensitive)

- [ ] T029 [P] Create `CourseCatalogSearchTests.cs` in `tests/Catalog.Tests/` with xUnit test `BrowseAsync_filters_by_category` that calls `BrowseAsync(null, "Programming", 1, 12)` and asserts all returned courses have Category == "Programming"

- [ ] T030 [P] Create `CourseCatalogSearchTests.cs` in `tests/Catalog.Tests/` with xUnit test `BrowseAsync_combines_search_and_category` that calls `BrowseAsync("data", "Programming", 1, 12)` and asserts all results match both criteria

- [ ] T031 Create `CourseCatalogSearchTests.cs` in `tests/Catalog.Tests/` with xUnit test `BrowseAsync_returns_correct_page` that calls `BrowseAsync` with page 1 and page 2 and asserts different, non-overlapping course sets with correct ordering

- [ ] T032 Create `CourseCatalogSearchTests.cs` in `tests/Catalog.Tests/` with xUnit test `BrowseAsync_total_count_across_pages_matches` that paginates through all pages and asserts the sum of all page row counts equals `TotalCount` from Result Set 2

- [ ] T033 [P] Create `CourseCatalogSearchTests.cs` in `tests/Catalog.Tests/` with xUnit test `BrowseAsync_empty_result_for_no_match` that calls `BrowseAsync("xyznonexistent", null, 1, 12)` and asserts zero items and TotalCount == 0

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup: Migration + SP + FTS)
    └── Phase 2 (Foundational: BrowseAsync service method)
            ├── Phase 3 (US1+US2: Search + Category Filter)  → MVP
            └── Phase 4 (US3: Pagination)
                    └── Phase 5 (US4: Combined workflow)
Phase 6 (Performance Tests) — can start after Phase 1
Phase 7 (Integration Tests) — can start after Phase 2
```

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1+US2 (Phase 3)**: Depends on Phase 2 — MVP deliverable
- **US3 (Phase 4)**: Depends on Phase 2 — can start in parallel with Phase 3
- **US4 (Phase 5)**: Depends on Phases 3 AND 4
- **Performance Tests (Phase 6)**: Depends on Phase 1 (SP must exist) — can run in parallel with user story phases
- **Integration Tests (Phase 7)**: Depends on Phase 2 (BrowseAsync must exist) — can run in parallel with user story phases

### Parallel Opportunities

- **Phase 1**: T001, T002, T003 are sequential (migration file structure) — no parallelism
- **Phase 2**: T004 → T005 sequential (record type needed by method) — no parallelism
- **Phase 3**: T006 → T007 → T008 → T009, T010 sequential flow; T009 and T010 are [P] with each other
- **Phase 4**: T011 → T012; T013 is [P] with T012 (different files); T014 depends on T012+T013; T015, T016 after T014
- **Phase 5**: T017-T020 are verification tasks — can be [P] since they touch different aspects
- **Phase 6**: T021-T023, T026-T027 are [P] with each other (same file but independent test methods); T024-T025 are [P] with the group
- **Phase 7**: T028-T030 are [P]; T031-T032 depend on each other (pagination sequence); T033 is [P]

### Parallel Execution Example: Performance Tests

```
# All Phase 6 tasks can dispatch simultaneously:
Task: "PerformanceTests.cs — SP 200ms with 100 courses"
Task: "PerformanceTests.cs — SP 500ms with 1000 courses"
Task: "PerformanceTests.cs — SP 1000ms with 10000 courses"
Task: "PerformanceTests.cs — FTS vs LIKE comparison"
Task: "PerformanceTests.cs — BrowseAsync end-to-end 500ms"
Task: "PerformanceTests.cs — FTS index existence check"
Task: "PerformanceTests.cs — LIKE fallback when FTS unavailable"
```

### Parallel Execution Example: US1+US2

```
# After T008 completes:
Task: "Verify Clear button (T009)"
Task: "Verify _CourseList partial (T010)"
```

---

## Implementation Strategy

### MVP First (Phases 1-3 Only)

1. Phase 1: Database migration + stored procedure + FTS index
2. Phase 2: `BrowseAsync` service method
3. Phase 3: Fix search + category filter HTMX, wire to `BrowseAsync`
4. **STOP AND VALIDATE**: Browse Courses page works with search and filter
5. Deploy/demo if ready

### Incremental Delivery

1. Phases 1-2: Foundation
2. Phase 3: MVP — Search + Filter work ✅
3. Phase 4: Pagination added ✅
4. Phase 5: Combined workflow polished ✅
5. Phase 6: Performance validated ✅
6. Phase 7: Integration tested ✅

### Parallel Team Strategy

With subagent parallelism:

1. Parent completes Phases 1-2 (shared infrastructure)
2. Dispatch in parallel:
   - Subagent A: Phase 3 (US1+US2 — search/filter)
   - Subagent B: Phase 6 (Performance Tests — only needs Phase 1)
   - Subagent C: Phase 7 (Integration Tests — only needs Phase 2)
3. After Phase 3 completes: Subagent D: Phase 4 (Pagination)
4. After Phases 3+4 complete: Phase 5 (Combined workflow verification)

---

## Notes

- [P] tasks = different files or independent test methods — safe for parallel execution
- [Story] label maps task to specific user story for traceability
- Performance tests use direct `SqlConnection` + `SqlCommand` for pure DB measurement (T021-T024, T026-T027) and DI-resolved service for full-stack measurement (T025)
- All tests target `tests/Catalog.Tests/` — the existing xUnit project
- Seed data for performance tests should use parameterized INSERT loops in the test's `[SetUp]` or inline in the test method
- The stored procedure's FTS fallback (LIKE) is tested explicitly (T027)
- Commit after each task or logical group; stop at checkpoints to validate independently
