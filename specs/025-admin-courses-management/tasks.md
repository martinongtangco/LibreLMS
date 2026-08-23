---

description: "Task list for Admin Courses Management Overhaul with SCORM Integration"

---

# Tasks: Admin Courses Management Overhaul with SCORM Integration

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

- [x] T001 Create git branch `bug/025-admin-courses-management` from `main`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Database migration, service-layer changes, and new DTOs that user story pages depend on.

**CRITICAL**: T002-T009 must complete before dependent story phases begin.

### Database Migration

- [x] T002 [P] Generate EF Core migration `AddScormPackageNullableCourseId` in `src/Host/Migrations/Scorm/` that: (a) drops existing unique index on `ScormPackages.CourseId`, (b) alters `CourseId` column from `Guid` to nullable `Guid?`, (c) creates filtered unique index `WHERE [CourseId] IS NOT NULL` using `HasFilter("[CourseId] IS NOT NULL")`

### Scorm Module Changes

- [x] T003 [P] Modify `src/Modules/Scorm/Domain/ScormPackage.cs`: change `CourseId` property from `Guid` to `Guid?` (nullable)

- [x] T004 [P] Modify `src/Modules/Scorm/Infrastructure/ScormDbContext.cs`: update `OnModelCreating` for `ScormPackage` — change `entity.HasIndex(e => e.CourseId).IsUnique()` to `entity.HasIndex(e => e.CourseId).IsUnique().HasFilter("[CourseId] IS NOT NULL")`

- [x] T005 Modify `src/Modules/Scorm/Application/ScormPackageService.cs`: add `ListAvailableAsync()` method returning `Task<IEnumerable<ScormPackage>>` that queries `ScormPackages` where `CourseId == null`; add `AssociateWithCourseAsync(Guid packageId, Guid courseId)` method that finds a package by `packageId`, validates `CourseId` is null, sets `CourseId = courseId`, and saves; add `ReplacePackageAsync(Guid courseId, Stream zipStream)` method that finds existing package for `courseId`, deletes its content directory from filesystem, removes the entity, then calls existing upload logic to create new package with the given `courseId`

### Catalog Module Changes

- [x] T006 [P] Create `src/Modules/Catalog/Endpoints/UpdateCourseRequest.cs` with record containing: Title (string), ShortDescription (string), FullDescription (string), Category (string), Duration (string)

- [x] T007 [P] Modify `src/Modules/Catalog/Endpoints/CreateCourseRequest.cs`: add optional `Guid? ScormPackageId` parameter to the record for SCORM association during course creation

- [x] T008 Modify `src/Modules/Catalog/Application/CourseCatalogService.cs`: add `UpdateAsync(Guid courseId, UpdateCourseRequest request)` method that finds course by ID, updates Title/ShortDescription/FullDescription/Category/Duration, calls `SaveChangesAsync`, and returns updated Course (throws `KeyNotFoundException` if not found)

### Management Module Changes

- [x] T009 Modify `src/Modules/Management/Application/CourseVisibilityService.cs`: fix `GetAllCoursesAsync` to properly resolve organization names using `orgLookup.GetOrganizationAsync` with a cache (same pattern as `GetVisibleCoursesAsync`); fix `DeleteCourseAsync` to also delete the associated ScormPackage (via injected ScormDbContext or ScormPackageService) and its content directory from the filesystem before deleting the course

**Checkpoint**: Service layer ready — migration applied, new methods exist, all DTOs in place. Page implementation can begin.

---

## Phase 3: User Story 1 - View and Manage Courses with Filtering, Sorting, and Pagination (Priority: P1) 🎯 MVP

**Goal**: Admin can search, filter by category, sort by column, and paginate through courses on the Admin/Courses listing page. SCORM status column shows whether a course has SCORM content.

**Independent Test**: Navigate to `/Admin/Courses`, enter a search term, select a category, click column headers to sort, and navigate pages. Results update correctly with each action. SCORM status column shows indicators.

**Dependencies**: T009 (GetAllCoursesAsync fix)

### Implementation for User Story 1

- [x] T010 [P] [US1] Modify `src/Host/Pages/Admin/Courses/Index.cshtml.cs`: inject `ScormPackageService` alongside existing services; add `HasScorm` boolean to `CourseDisplay` record; in `OnGetAsync`, after building `Courses` list, check each course against `ScormPackageService.GetPackageByCourseIdAsync` to populate `HasScorm`; ensure `OnPostDeleteAsync` includes SCORM-aware confirmation data (expose `HasScorm` per course in the model)

- [x] T011 [P] [US1] Modify `src/Host/Pages/Admin/Courses/Index.cshtml`: add "SCORM" column to the table showing a badge/indicator for courses with SCORM content; ensure "Create Course" button, Edit link (`asp-page="Edit" asp-route-courseId="@course.CourseId"`), and Delete form are all present in the Actions column; add `data-has-scorm="true"` attribute to delete buttons for courses with SCORM content (used by confirmation JS in US4)

**Checkpoint**: At this point, the Admin/Courses listing page displays courses with search, filter, sort, pagination, and SCORM status indicators. Create/Edit/Delete actions are wired.

---

## Phase 4: User Story 2 - Create New Courses (Priority: P1)

**Goal**: Admin can create new courses from the Admin/Courses page via the "Create Course" button, with the option to upload SCORM or associate existing SCORM.

**Independent Test**: Navigate to `/Admin/Courses`, click "Create Course", fill in all fields, optionally upload SCORM or associate existing SCORM, submit. Success message appears and the new course is visible in the listing.

**Dependencies**: T003-T008 (Scorm domain/DB changes, ScormPackageService methods, Catalog CreateCourseRequest change)

### Implementation for User Story 2

- [x] T012 [P] [US2] Rewrite `src/Host/Pages/Admin/Courses/Create.cshtml.cs`: inject `CourseCatalogService`, `ScormPackageService`, and `IWebHostEnvironment`; add `[BindProperty]` properties: `ScormMode` (string: "none"/"upload"/"associate"), `ScormFile` (IFormFile?), `ScormPackageId` (Guid?); in `OnPostAsync`: (a) create course via `CourseCatalogService.CreateAsync`, (b) if ScormMode="upload", call `ScormPackageService.UploadAsync(file.OpenReadStream(), courseId)`, (c) if ScormMode="associate", call `ScormPackageService.AssociateWithCourseAsync(ScormPackageId.Value, courseId)`, (d) redirect to `/Admin/Courses` with success/error params

- [x] T013 [P] [US2] Rewrite `src/Host/Pages/Admin/Courses/Create.cshtml`: add SCORM section after Duration field with radio button group: (1) "No SCORM content" (default), (2) "Upload new SCORM package", (3) "Associate existing SCORM"; add `<input type="file" accept=".zip" name="ScormFile" id="scormFileInput" />` hidden by default; add dropdown for existing SCORM packages hidden by default, populated from `Model.AvailableScormPackages`; add minimal JavaScript to toggle visibility of file input and dropdown based on radio selection; load available SCORM packages in page model `OnGet` via `ScormPackageService.ListAvailableAsync()`

**Checkpoint**: Course creation works end-to-end: button click → form with SCORM options → submit → success → listing shows new course with optional SCORM.

---

## Phase 5: User Story 3 - Edit Existing Course Details (Priority: P2)

**Goal**: Admin can edit course details (title, description, category, duration) and manage SCORM (view current, add new, replace) from the Admin/Courses page.

**Independent Test**: Navigate to `/Admin/Courses`, click "Edit" on a course, modify fields and/or upload SCORM, save. Success message appears and updated data is visible.

**Dependencies**: T006, T008 (UpdateCourseRequest, UpdateAsync); T005 (ScormPackageService methods)

### Implementation for User Story 3

- [x] T014 [P] [US3] Create `src/Host/Pages/Admin/Courses/Edit.cshtml.cs`: `[Authorize(Roles = "SuperUser,OrgAdmin")]` page model injecting `CourseCatalogService`, `ScormPackageService`; `[BindProperty]` properties for Title, ShortDescription, FullDescription, Category, Duration, ScormFile (IFormFile?), ScormMode (string?); `CurrentScormPackage` property for display; `OnGetAsync(Guid courseId)` loads course by ID and populates fields, loads current SCORM via `ScormPackageService.GetPackageByCourseIdAsync(courseId)` (redirects to Index with error if not found); `OnPostAsync(Guid courseId)` calls `CourseCatalogService.UpdateAsync(courseId, new UpdateCourseRequest(...))`, then if ScormFile is provided calls `ScormPackageService.ReplacePackageAsync(courseId, ScormFile.OpenReadStream())` if course has SCORM or `ScormPackageService.UploadAsync(ScormFile.OpenReadStream(), courseId)` if not, then redirects to Index with success

- [x] T015 [P] [US3] Create `src/Host/Pages/Admin/Courses/Edit.cshtml`: page with `<h1>Edit Course</h1>`; form with inputs for Title, ShortDescription, FullDescription (textarea), Category, Duration matching the Create form layout; SCORM section showing: if `CurrentScormPackage` exists, display its ManifestTitle and CreatedAt with "Upload new SCORM to replace" file input; if no SCORM, show "Upload SCORM package" file input; Save Changes and Cancel buttons; Cancel links to `/Admin/Courses`

**Checkpoint**: Course editing works end-to-end: Edit link → pre-populated form → save → success → listing shows updated data. SCORM can be added or replaced.

---

## Phase 6: User Story 4 - Delete Courses Reliably (Priority: P2)

**Goal**: Admin can delete courses with confirmation and feedback. Courses with SCORM show a warning about SCORM deletion.

**Independent Test**: Navigate to `/Admin/Courses`, click "Delete" on a course, confirm. Course is removed and success message is shown.

**Dependencies**: T009 (DeleteCourseAsync fix with SCORM cleanup)

### Implementation for User Story 4

- [x] T016 [P] [US4] Modify `src/Host/Pages/Admin/Courses/Index.cshtml.cs`: in `OnPostDeleteAsync`, check if course has SCORM before deleting (via `ScormPackageService.GetPackageByCourseIdAsync`); expose `HasScorm` per course in the `CourseDisplay` record or as a separate `Dictionary<Guid, bool>` for the view

- [x] T017 [P] [US4] Modify `src/Host/Pages/Admin/Courses/Index.cshtml`: for delete buttons/forms, add JavaScript confirmation that checks `data-has-scorm` attribute — if true, show message "This course has SCORM content. Deleting this course will also permanently delete the SCORM package and its extracted files. Are you sure?" — if false, show standard "Are you sure you want to delete this course?"; verify the delete form correctly passes `courseId` via `asp-route-courseId`

**Checkpoint**: Course deletion works: Delete click → confirm dialog (with SCORM warning if applicable) → course removed → success message → listing updated.

---

## Phase 7: User Story 5 - Improved Table Readability and Visual Design (Priority: P3)

**Goal**: Table has clear visual distinction between rows, headers, and page background. SCORM status badges are visually clear.

**Independent Test**: Visual inspection of `/Admin/Courses` confirms the table card has white background distinct from the beige page, headers are clearly visible, alternating rows aid scanning, and SCORM badges are legible.

**Dependencies**: T011 (card wrapper markup exists from US1)

### Implementation for User Story 5

- [x] T018 [P] [US5] Add CSS rules to `src/Host/wwwroot/css/site.css`: add `.data-table tr:nth-child(even) { background: var(--color-bg); }` for alternating row colors; add `.data-table tbody tr:hover { background: var(--color-border-light, #f5f0ea); }` for hover highlight; add `.badge-scorm` or similar class for SCORM status indicators with appropriate colors; ensure the table card wrapper renders with `--color-surface` (#ffffff) background providing clear contrast against `--page-bg` (#f5ead8)

**Checkpoint**: Table is visually distinct with card surface, clear headers, alternating rows, hover states, and SCORM status badges.

---

## Phase 8: User Story 6 - SCORM Pool Management (Admin/Upload Page) (Priority: P1)

**Goal**: Admin can upload SCORM packages to the available pool (without a course) and manage (list/delete) orphaned packages.

**Independent Test**: Navigate to `/Admin/Upload`, upload a SCORM ZIP. It appears in the available pool list. Delete it. It is removed.

**Dependencies**: T003-T005 (Scorm nullable CourseId, service methods)

### Implementation for User Story 6

- [x] T019 [P] [US6] Rewrite `src/Host/Pages/Admin/Upload.cshtml.cs`: remove course dropdown and `CourseCatalogService`/`CourseVisibilityService` dependencies; inject `ScormPackageService` directly; add `AvailablePackages` property (list of ScormPackage with ManifestTitle, Id, CreatedAt); in `OnGetAsync`, call `ScormPackageService.ListAvailableAsync()` to populate packages; in `OnPostAsync`, handle file upload without courseId — use direct call to `ScormPackageService` with `null` courseId (may need to add overload or modify existing `UploadAsync` to accept `Guid? courseId`); add `OnPostDeleteAsync(Guid packageId)` that deletes the ScormPackage entity and its content directory from the filesystem

- [x] T020 [P] [US6] Rewrite `src/Host/Pages/Admin/Upload.cshtml`: remove course selection dropdown; add upload form with single file input for SCORM ZIP; add section below titled "Available SCORM Packages" listing unassociated packages with ManifestTitle, CreatedAt, and Delete button for each; add SCORM pool count display; add 50MB upload size note

- [x] T021 Modify `src/Modules/Scorm/Application/ScormPackageService.cs`: add `UploadAsync(Stream zipStream, Guid? courseId)` overload that accepts nullable courseId — when null, creates ScormPackage with `CourseId = null`; add `DeleteAsync(Guid packageId)` method that finds the package, deletes its content directory from filesystem, removes the entity, and saves

**Checkpoint**: SCORM pool management works: upload to pool → package listed → delete from pool → package removed. Course creation can associate with pool packages.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Verification, cleanup, and E2E validation.

- [x] T022 Run `dotnet build src/Host` and confirm zero build errors

- [x] T023 Run EF Core migration: `dotnet ef database update --project src/Host --context ScormDbContext` and confirm `ScormPackages.CourseId` is nullable with filtered unique index

- [x] T024 Run `dotnet test tests/ArchitectureTests` and confirm module boundary tests pass (no cross-module references introduced)

- [x] T025 Run the application (`dotnet run --project src/Host`) and validate all 14 scenarios from `quickstart.md` manually

- [x] T026 [P] Verify responsive layout on mobile viewport (≤480px): filters stack vertically, table scrolls horizontally, pagination buttons remain tappable, SCORM radio section is usable

- [x] T027 Verify empty state displays when no courses match search/filter, with actionable guidance message

- [x] T028 Verify SCORM upload size limit (50MB): test with a file exceeding 50MB and confirm rejection

- [x] T029 Verify that the SCORM upload endpoint in `src/Modules/Scorm/Endpoints/` accepts nullable courseId (if the endpoint is used by the Upload page; otherwise confirm direct service injection is used instead)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — T002/T003/T004 block US6 (SCORM pool); T005 blocks US2/US3/US6; T006/T008 block US3; T009 blocks US1/US4
- **US1 (Phase 3)**: Depends on T009 (GetAllCoursesAsync fix) — redesigns Index pages
- **US2 (Phase 4)**: Depends on T003-T008 (Scorm changes + Catalog changes) — Create page with SCORM
- **US3 (Phase 5)**: Depends on T005, T006, T008 — Edit page with SCORM management
- **US4 (Phase 6)**: Depends on T009 (DeleteCourseAsync fix) and T010/T011 (SCORM data in Index)
- **US5 (Phase 7)**: Depends on T011 (card wrapper markup exists) — CSS only
- **US6/Pool (Phase 8)**: Depends on T003-T005 — Upload page repurpose
- **Polish (Phase 9)**: Depends on all stories complete

### User Story Dependencies

```
Phase 1 (Setup)
    ↓
Phase 2 (Foundational: T002-T009)
    ↓
    ├── US1 (Listing: T010, T011) ────────────────→ US4 (Delete: T016, T017)
    ├── US2 (Create with SCORM: T012, T013) ──────→ Independent
    ├── US3 (Edit with SCORM: T014, T015) ────────→ Independent (after T005, T006, T008)
    ├── US6 (SCORM Pool: T019, T020, T021) ───────→ Independent (after T003-T005)
    └── US5 (CSS: T018) ──────────────────────────→ After US1 (card wrapper exists)
    ↓
Phase 9 (Polish: T022-T029)
```

### Parallel Opportunities

Within each phase, tasks marked [P] can run concurrently:

```bash
# Phase 2: Foundational tasks in parallel (different modules, no dependencies)
Task: "Generate EF migration" (T002)
Task: "Make ScormPackage.CourseId nullable" (T003)
Task: "Update ScormDbContext filtered index" (T004)
Task: "Create UpdateCourseRequest DTO" (T006)
Task: "Add ScormPackageId to CreateCourseRequest" (T007)

# After T002-T005 complete, parallel story work:
Task: "Rewrite Index.cshtml.cs with SCORM status" (T010) [US1]
Task: "Rewrite Index.cshtml with SCORM column" (T011) [US1]
Task: "Rewrite Create.cshtml.cs with SCORM" (T012) [US2]
Task: "Rewrite Create.cshtml with SCORM section" (T013) [US2]
Task: "Create Edit.cshtml.cs" (T014) [US3]
Task: "Create Edit.cshtml" (T015) [US3]
Task: "Rewrite Upload.cshtml.cs for pool" (T019) [US6]
Task: "Rewrite Upload.cshtml for pool" (T020) [US6]

# US4 (delete) after US1 completes:
Task: "Add SCORM-aware delete to Index.cshtml.cs" (T016)
Task: "Add SCORM delete confirmation JS to Index.cshtml" (T017)

# US5 (CSS) anytime after US1 adds card wrapper:
Task: "Add table contrast CSS rules" (T018)
```

### Critical Path

```
T001 → T002/T003/T004 → T005 → T012/T013 → T022 (Polish)
```

The critical path is ~4 sequential steps. All other tasks branch off in parallel.

---

## Implementation Strategy

### MVP First (US1 + US2 + US6 Only)

1. Complete Phase 1: Setup (branch creation)
2. Complete Phase 2: Foundational (T002-T009)
3. Complete Phase 3: US1 — listing with search, filter, sort, pagination, SCORM column
4. Complete Phase 4: US2 — create course with SCORM upload/association
5. Complete Phase 8: US6 — SCORM pool management on Upload page
6. **STOP AND VALIDATE**: Admin can browse (with filters/pagination/SCORM status), create courses (with SCORM), and manage SCORM pool
7. Deploy/demo if ready

### Incremental Delivery

1. MVP (US1 + US2 + US6) → Browse + Create with SCORM + Pool management work
2. Add US3 → Edit with SCORM management works
3. Add US4 → Delete with SCORM warning works
4. Add US5 → Table readability improved
5. Each increment adds value without breaking previous features

### Parallel Team Strategy

With subagent parallelism (Constitution Principle XI):

1. Complete Phase 1 + Phase 2 together (parallel foundational tasks)
2. Launch US1 (T010 + T011), US2 (T012 + T013), US3 (T014 + T015), and US6 (T019 + T020 + T021) as parallel runs (all different files)
3. Once US1 completes, launch US4 (T016 + T017)
4. US5 (T018) can run anytime after US1 adds card wrapper
5. Parent session runs Polish (T022-T029) after all stories complete

---

## Notes

- [P] tasks = different files, no dependencies on incomplete work
- [Story] label maps task to specific user story for traceability
- Index.cshtml and Index.cshtml.cs are comprehensive rewrites in US1 to avoid multi-story file conflicts
- Create/Edit links are added in US1 but only become functional after US2/US3 complete
- Constitution Principle XIII requires build + E2E test + post-merge regression before claiming completion
- SCORM upload uses direct service injection (no HTTP client) — consistent with research decisions
- File upload size limit (50MB) must be configured in Program.cs or the page handler via `[DisableRequestSizeLimit]` or `MaxRequestSize`
