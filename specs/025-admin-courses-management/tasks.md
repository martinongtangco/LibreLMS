---

description: "Task list for Admin Courses Management Overhaul"

---

# Tasks: Admin Courses Management Overhaul

**Input**: Design documents from `/specs/025-admin-courses-management/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Not included as standalone tasks. E2E validation via Playwright is part of the Polish phase (Constitution Principle XIII).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Branch creation and environment preparation

- [ ] T001 Create git branch `bug/025-admin-courses-management` from `main`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Service-layer changes and new DTOs that user story pages depend on.

**CRITICAL**: T002-T004 must complete before dependent story phases begin.

- [ ] T002 [P] [US3] Create `UpdateCourseRequest` record in `src/Modules/Catalog/Endpoints/UpdateCourseRequest.cs` with fields: Title (string), ShortDescription (string), FullDescription (string), Category (string), Duration (string)
- [ ] T003 [P] [US3] Add `UpdateAsync(Guid courseId, UpdateCourseRequest request)` method to `src/Modules/Catalog/Application/CourseCatalogService.cs` that fetches the course by ID, updates Title/ShortDescription/FullDescription/Category/Duration, calls SaveChangesAsync, and returns the updated Course (throws KeyNotFoundException if not found)
- [ ] T004 [P] [US4] Fix `GetAllCoursesAsync` in `src/Modules/Management/Application/CourseVisibilityService.cs` to resolve organization names using `orgLookup.GetOrganizationAsync` instead of hardcoding "Unknown", following the same pattern used in `GetVisibleCoursesAsync`

**Checkpoint**: Service layer ready — all new methods and fixes are in place. Page implementation can begin.

---

## Phase 3: User Story 1 - View and Manage Courses with Filtering, Sorting, and Pagination (Priority: P1) 🎯 MVP

**Goal**: Admin can search, filter by category, sort by column, and paginate through courses on the Admin/Courses listing page.

**Independent Test**: Navigate to `/Admin/Courses`, enter a search term, select a category, click column headers to sort, and navigate pages. Results update correctly with each action.

**Note**: This phase includes the Create button, Edit link, and Delete action in the Index redesign to avoid modifying `Index.cshtml` multiple times across stories. All CRUD actions are wired here; their back-end pages (Create, Edit) are completed in later stories.

### Implementation for User Story 1

- [ ] T005 [P] [US1] Rewrite `src/Host/Pages/Admin/Courses/Index.cshtml.cs`: add `[BindProperty(SupportsGet = true)]` params for Search (string?), Category (string?), SortBy (string, default "title"), SortDirection (string, default "asc"), PageNumber (int, default 1), PageSize (int, default 15); add `Categories` property (List<string>); in `OnGetAsync`, call `CourseCatalogService.BrowseAsync(search, category, pageNumber, pageSize)` or fall back to `ListAsync` with LINQ filtering and in-memory `.OrderBy()` + `.Skip().Take()` for sorting and pagination; populate Categories from distinct course categories using `CourseCatalogService.ListAsync()`
- [ ] T006 [P] [US1] Rewrite `src/Host/Pages/Admin/Courses/Index.cshtml`: add "Create Course" button linking to `/Admin/Courses/Create`; add search input and category dropdown filter above the table; make column headers clickable links with sort params (e.g., `?sortBy=title&sortDirection=desc`); wrap table in a `<div class="card">` for contrast against page background; add "Edit" link (`asp-page="Edit" asp-route-courseId="@course.CourseId"`) and keep existing Delete form in Actions column; add pagination controls below the table with Previous/Next buttons and page info; add empty state message when no courses match filters; update `CourseDisplay` record if needed to include additional fields for edit/delete actions

**Checkpoint**: At this point, the Admin/Courses listing page supports search, category filter, column sorting, pagination, and displays Create/Edit/Delete actions. Create and Edit links point to pages that will be completed in later stories.

---

## Phase 4: User Story 2 - Create New Courses (Priority: P1)

**Goal**: Admin can create new courses from the Admin/Courses page via the "Create Course" button.

**Independent Test**: Navigate to `/Admin/Courses`, click "Create Course", fill in all fields, submit. Success message appears and the new course is visible in the listing.

### Implementation for User Story 2

- [ ] T007 [P] [US2] Rewrite `src/Host/Pages/Admin/Courses/Create.cshtml.cs`: replace `IHttpClientFactory` injection with `CourseCatalogService` injection; in `OnPostAsync`, call `CourseCatalogService.CreateAsync` directly with the form fields instead of making an HTTP POST; after successful creation, redirect to `/Admin/Courses` with a success query parameter (e.g., `?success=true`)
- [ ] T008 [P] [US2] Update `src/Host/Pages/Admin/Courses/Create.cshtml` redirect link from `asp-page="/Courses/Index"` to `asp-page="/Admin/Courses"` in the success message section

**Checkpoint**: Course creation works end-to-end: button click → form → submit → success → listing shows new course.

---

## Phase 5: User Story 3 - Edit Existing Course Details (Priority: P2)

**Goal**: Admin can edit course details (title, description, category, duration) from the Admin/Courses page.

**Independent Test**: Navigate to `/Admin/Courses`, click "Edit" on a course, modify a field, save. Success message appears and the updated data is visible in the listing.

**Dependencies**: T002 (UpdateCourseRequest) and T003 (UpdateAsync) must be complete.

### Implementation for User Story 3

- [ ] T009 [US3] Create `src/Host/Pages/Admin/Courses/Edit.cshtml.cs`: `[Authorize(Roles = "SuperUser,OrgAdmin")]` page model injecting `CourseCatalogService`; `[BindProperty]` properties for Title, ShortDescription, FullDescription, Category, Duration; `OnGetAsync(courseId)` loads the course by ID and populates fields (redirects to Index with error if not found); `OnPostAsync(courseId)` calls `CourseCatalogService.UpdateAsync(courseId, new UpdateCourseRequest(...))` and redirects to Index with success
- [ ] T010 [US3] Create `src/Host/Pages/Admin/Courses/Edit.cshtml`: page with `<h1>Edit Course</h1>`, form with inputs for Title, ShortDescription, FullDescription (textarea), Category, Duration matching the Create form layout using existing CSS classes (`card`, `form-group`, `form-label`, `form-control`, `form-textarea`, `btn-primary`, `btn-secondary`); Save Changes and Cancel buttons; Cancel links to `/Admin/Courses`

**Checkpoint**: Course editing works end-to-end: Edit link → pre-populated form → save → success → listing shows updated data.

---

## Phase 6: User Story 4 - Delete Courses Reliably (Priority: P2)

**Goal**: Admin can delete courses with confirmation and feedback.

**Independent Test**: Navigate to `/Admin/Courses`, click "Delete" on a course, confirm. Course is removed and success message is shown.

**Dependencies**: T004 (GetAllCoursesAsync fix) must be complete.

### Implementation for User Story 4

- [ ] T011 [US4] Verify delete flow in `src/Host/Pages/Admin/Courses/Index.cshtml.cs`: confirm that `OnPostDeleteAsync` calls `CourseVisibilityService.DeleteCourseAsync`, handles `KeyNotFoundException` with an error message, and refreshes the course list via `OnGetAsync`; ensure the delete form in `Index.cshtml` correctly passes `courseId` via `asp-route-courseId`

**Checkpoint**: Course deletion works: Delete click → confirm dialog → course removed → success message → listing updated.

---

## Phase 7: User Story 5 - Improved Table Readability and Visual Design (Priority: P3)

**Goal**: Table has clear visual distinction between rows, headers, and page background.

**Independent Test**: Visual inspection of `/Admin/Courses` confirms the table card has white background distinct from the beige page, headers are clearly visible, and alternating rows aid scanning.

### Implementation for User Story 5

- [ ] T012 [P] [US5] Add CSS rules to `src/Host/wwwroot/css/site.css`: add `.data-table tr:nth-child(even) { background: var(--color-bg); }` for alternating row colors; add `.data-table tbody tr:hover { background: var(--color-border-light, #f5f0ea); }` for hover highlight; ensure the table card wrapper (added in T006) renders with `--color-surface` (#ffffff) background providing clear contrast against `--page-bg` (#f5ead8)

**Checkpoint**: Table is visually distinct with card surface, clear headers, alternating rows, and hover states.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Verification, cleanup, and E2E validation.

- [ ] T013 Run `dotnet build src/Host` and confirm zero build errors
- [ ] T014 Run `dotnet test tests/ArchitectureTests` and confirm module boundary tests pass
- [ ] T015 Run the application (`dotnet run --project src/Host`) and validate all 7 scenarios from `quickstart.md` manually
- [ ] T016 [P] Verify responsive layout on mobile viewport (≤480px): filters stack vertically, table scrolls horizontally, pagination buttons remain tappable
- [ ] T017 Verify empty state displays when no courses match search/filter, with actionable guidance message

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — T002/T003 block US3; T004 blocks US4
- **US1 (Phase 3)**: Depends on Setup — can start immediately; redesigns Index pages
- **US2 (Phase 4)**: Depends on Setup — independent files (Create.cshtml.cs, Create.cshtml)
- **US3 (Phase 5)**: Depends on Foundational (T002, T003) AND US1 (T006 adds Edit link)
- **US4 (Phase 6)**: Depends on Foundational (T004 fix) — verify existing delete handler works
- **US5 (Phase 7)**: Depends on US1 (T006 adds card wrapper) — CSS applies to T006's markup
- **Polish (Phase 8)**: Depends on all stories complete

### User Story Dependencies

```
Phase 1 (Setup)
    ↓
Phase 2 (Foundational: T002, T003, T004) ──→ US3 (Edit) & US4 (Delete)
    ↓
US1 (Listing redesign: T005, T006) ────────→ US3 needs Edit link from T006
US2 (Create fix: T007, T008) ──────────────→ Independent
US4 (Delete verify: T011) ─────────────────→ Independent (after T004)
US5 (CSS: T012) ───────────────────────────→ After US1 (card wrapper exists)
    ↓
Phase 8 (Polish)
```

### Parallel Opportunities

Within each phase, tasks marked [P] can run concurrently:

```bash
# Phase 2: All foundational tasks in parallel (3 files, no dependencies)
Task: "Create UpdateCourseRequest record" (T002)
Task: "Add UpdateAsync to CourseCatalogService" (T003)
Task: "Fix GetAllCoursesAsync org names" (T004)

# US1 + US2 can start together (all different files):
Task: "Rewrite Index.cshtml.cs with BrowseAsync" (T005)
Task: "Rewrite Index.cshtml with filters/pagination" (T006)
Task: "Rewrite Create.cshtml.cs with direct DI" (T007)
Task: "Fix Create.cshtml redirect" (T008)

# After Foundational completes, US3 tasks can start:
Task: "Create Edit.cshtml.cs" (T009)
Task: "Create Edit.cshtml" (T010)

# US5 (CSS) can run anytime after US1 adds card wrapper:
Task: "Add table contrast CSS rules" (T012)
```

### Critical Path

```
T001 → T005/T006 → T009/T010 → T013 (Polish)
```

The critical path is ~4 sequential steps. All other tasks branch off in parallel.

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1: Setup (branch creation)
2. Complete Phase 2: Foundational (T002, T003, T004)
3. Complete Phase 3: US1 — listing with search, filter, sort, pagination
4. Complete Phase 4: US2 — create course flow fixed
5. **STOP AND VALIDATE**: Admin can browse (with filters/pagination), create, and delete courses
6. Deploy/demo if ready

### Incremental Delivery

1. MVP (US1 + US2) → Browse + Create work
2. Add US3 → Edit works
3. Add US4 → Delete verified working
4. Add US5 → Table readability improved
5. Each increment adds value without breaking previous features

### Parallel Team Strategy

With subagent parallelism (Constitution Principle XI):

1. Complete Phase 1 + Phase 2 together (3 parallel tasks)
2. Launch US1 (T005 + T006) and US2 (T007 + T008) as parallel runs
3. Once Foundational completes, launch US3 (T009 + T010)
4. US4 (T011) and US5 (T012) can run in parallel with US3
5. Parent session runs Polish (T013-T017) after all stories complete

---

## Notes

- [P] tasks = different files, no dependencies on incomplete work
- [Story] label maps task to specific user story for traceability
- Index.cshtml and Index.cshtml.cs are comprehensive rewrites in US1 to avoid multi-story file conflicts
- Create/Edit links are added in US1 but only become functional after US2/US3 complete
- Constitution Principle XIII requires build + E2E test + post-merge regression before claiming completion
