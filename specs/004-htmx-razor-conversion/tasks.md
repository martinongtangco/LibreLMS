# Tasks: HTMX + Razor Modern UI

**Input**: Design documents from `/specs/004-htmx-razor-conversion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/partial-views.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add HTMX to the project and create shared presentation assets used by all user stories.

- [ ] T001 Add HTMX CDN script tag (`https://cdn.jsdelivr.net/npm/htmx.org@2.0.4/dist/htmx.min.js`) to bottom of `<body>` in `src/Host/Pages/Shared/_Layout.cshtml`

- [ ] T002 [P] Define `CourseItem` record in `src/Host/Pages/Courses/Index.cshtml.cs` with properties: `Id (Guid)`, `Title (string)`, `ShortDescription (string)`, `Category (string)`, `Duration (string)`, `IsEnrolled (bool)`

- [ ] T003 [P] Define `CourseDetailItem` record in `src/Host/Pages/Courses/Detail.cshtml.cs` with properties: `Id`, `Title`, `ShortDescription`, `FullDescription`, `Category`, `Duration`, `IsEnrolled`, `IsScorm`, `ScormPackageId`

- [ ] T004 [P] Define `EnrollmentRow` record in `src/Host/Pages/MyCourses/Index.cshtml.cs` with properties: `EnrollmentId`, `CourseId`, `CourseTitle`, `EnrolledAt`, `LatestStatus (string?)`, `LatestScore (double?)`

- [ ] T005 [P] Define `EnrollmentResult` record in `src/Host/Pages/Courses/Detail.cshtml.cs` with properties: `Success`, `Message`, `MessageType ("success"|"warning"|"error")`, `CourseId`, `IsScorm (bool?)`

- [ ] T006 [P] Create `src/Host/Pages/Shared/_CourseCard.cshtml` partial — strongly-typed `@model CourseItem`, renders a single `<div class="card">` with course title link, short description, category/duration/enrolled badges, and "View Details" button

- [ ] T007 [P] Create `src/Host/Pages/Shared/_EnrollmentResult.cshtml` partial — strongly-typed `@model EnrollmentResult`, renders success badge + launch button (if SCORM), or warning/error message with retry option based on `MessageType`

- [ ] T008 [P] Create `src/Host/Pages/Shared/_ErrorPartial.cshtml` partial — strongly-typed `@model string` (error message text), renders a `<div class="error-message" style="...">` with the message

- [ ] T009 [P] Create `src/Host/Pages/Shared/_MyCourseRow.cshtml` partial — strongly-typed `@model EnrollmentRow`, renders a single enrollment row with course title link, enrollment date, status badge (color-coded), and score if available

**Checkpoint**: HTMX is loaded on all pages. View models and shared partials exist and compile.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Refactor page models to use direct service injection (instead of HttpClient → API) so HTMX handler methods can return PartialViewResult. **Must complete before any user story.**

- [ ] T010 Refactor `src/Host/Pages/Courses/Index.cshtml.cs` — inject `CourseCatalogService` directly instead of using `HttpClient` to call `/api/courses`; update `OnGetAsync` to use the service; remove `CourseListResponse` JSON deserialization code; keep `CourseItem` record

- [ ] T011 Refactor `src/Host/Pages/Courses/Detail.cshtml.cs` — inject `CourseCatalogService` and `ScormPackageService` directly instead of using `HttpClient`; update `OnGetAsync` to use services; map result to `CourseDetailItem` record; remove JSON deserialization code

- [ ] T012 Refactor `src/Host/Pages/MyCourses/Index.cshtml.cs` — inject `EnrollmentLookup` and `ScormAttemptService` directly instead of using `HttpClient`; update `OnGetAsync` to use services; map results to `EnrollmentRow` records; remove JSON deserialization code

- [ ] T013 Add enrollment helper method to `src/Host/Pages/Courses/Detail.cshtml.cs` — inject `EnrollmentService`; add private `async Task<EnrollmentResult> TryEnrollAsync(Guid courseId)` that calls `EnrollmentService.EnrollAsync`, handles the student ID extraction (reuse `ScormHelpers.GetStudentId` pattern), and returns `EnrollmentResult` with appropriate `Success`/`Message`/`MessageType`

**Checkpoint**: All page models use direct service injection. No `HttpClient` calls remain in the Razor Pages layer. Architecture tests still pass.

---

## Phase 3: User Story 1 - Browse and Filter Courses Without Page Reloads (Priority: P1) 🎯 MVP

**Goal**: Course catalog search and category filter update the course list via partial page swap without full page reload.

**Independent Test**: Navigate to `/Courses`, type in search box, select a category — course list updates without navbar/footer changing.

- [ ] T014 [US1] Create `src/Host/Pages/Shared/_CourseList.cshtml` partial — strongly-typed `@model IEnumerable<CourseItem>`, renders the full course card grid using `@foreach` + `@Html.Partial("_CourseCard", course)` for each item, with empty state `<div class="card empty-state">` when collection is empty

- [ ] T015 [US1] Add `OnGetCourseListAsync(string? search, string? category)` handler to `src/Host/Pages/Courses/Index.cshtml.cs` — calls `CourseCatalogService.ListAsync(search, category)`, fetches enrolled course IDs, maps to `IEnumerable<CourseItem>`, returns `Partial("_CourseList", model)`

- [ ] T016 [US1] Update `src/Host/Pages/Courses/Index.cshtml` — wrap existing course rendering in `<div id="course-list">` target region; replace inline course card rendering with `@await Html.PartialAsync("_CourseList", Model.Courses)`; add `id="search-input"` to search text input; add `hx-get="/Courses/Index?handler=CourseList"` and `hx-trigger="keyup changed delay:300ms"` and `hx-target="#course-list"` and `hx-indicator=".htmx-indicator"` to search input; add `hx-get="/Courses/Index?handler=CourseList"` and `hx-trigger="change"` and `hx-target="#course-list"` to category select; add `hx-get="/Courses/Index?handler=CourseList"` and `hx-target="#course-list"` to clear link; update filter form to NOT have `method="get"` (HTMX handles the request); keep `name="search"` and `name="category"` attributes on inputs so HTMX includes them

- [ ] T017 [US1] Add HTMX loading indicator CSS to `src/Host/Pages/Shared/_Layout.cshtml` — add `.htmx-indicator { display: none; }` and `.htmx-request .htmx-indicator { display: inline; }` styles; add a small spinner element `<div class="htmx-indicator">⏳ Loading...</div>` near the course list area in `Index.cshtml`

**Checkpoint**: Course catalog filtering works entirely via HTMX partial swaps. No full page reloads during search or category filter. Graceful degradation: removing HTMX script restores full-page form submission.

---

## Phase 4: User Story 2 - Enroll in a Course with Inline Feedback (Priority: P1)

**Goal**: Enrollment from course detail page provides inline feedback (success/warning/error) without full page reload.

**Independent Test**: View a course detail, click "Enroll" — button replaced with enrolled badge or error message inline.

- [ ] T018 [US2] Add `OnPostEnrollAsync(Guid courseId)` handler to `src/Host/Pages/Courses/Detail.cshtml.cs` — calls `TryEnrollAsync(courseId)`, returns `Partial("_EnrollmentResult", result)` on success; catches exceptions and returns `Partial("_ErrorPartial", ex.Message)` on failure

- [ ] T019 [US2] Update `src/Host/Pages/Courses/Detail.cshtml` — replace the entire enrollment `<script>` block and `<button onclick="enrollInCourse(...)">` with: a `<div id="enroll-region">` containing a `<form hx-post="/Courses/Detail?handler=Enroll" hx-vals='{"courseId": "@Model.Course.Id"}' hx-swap="outerHTML" hx-target="#enroll-region">` with a `<button type="submit" class="btn btn-primary">Enroll in This Course</button>`; when `IsEnrolled` is true, render the enrolled badge + launch button directly (no form needed)

- [ ] T020 [US2] Update `src/Host/Pages/Courses/Detail.cshtml.cs` — update `OnGetAsync` to also check enrollment status and set `IsEnrolled` on the `CourseDetailItem` so the initial page render shows the correct state (enrolled vs not enrolled)

**Checkpoint**: Enrollment works via HTMX with inline feedback. No `location.reload()`, no inline JavaScript. Success shows enrolled badge; 409 shows "already enrolled" warning; errors show inline error message with retry.

---

## Phase 5: User Story 3 - Navigate to Course Details Within the Page (Priority: P2)

**Goal**: Clicking a course card loads course details in the main content area without a full page reload.

**Independent Test**: From catalog, click a course title — detail loads in main area, navbar/footer stable. Click "Back to Catalog" — list restores.

- [ ] T021 [US3] Create `src/Host/Pages/Shared/_CourseDetail.cshtml` partial — strongly-typed `@model CourseDetailItem`, renders the full course detail card (title, badges, descriptions, enrollment section) matching the current `Detail.cshtml` layout but without the `<nav>` wrapper. Include the HTMX enrollment form from US2 when `!IsEnrolled`. Include a "← Back to Catalog" link with `hx-get="/Courses/Index?handler=CourseList" hx-target="#main-content"`.

- [ ] T022 [US3] Add `OnGetDetailAsync(Guid id)` handler to `src/Host/Pages/Courses/Detail.cshtml.cs` — calls services to fetch course + enrollment status, maps to `CourseDetailItem`, returns `Partial("_CourseDetail", model)`; returns `Partial("_ErrorPartial", "Course not found")` if course doesn't exist

- [ ] T023 [US3] Update `src/Host/Pages/Courses/Index.cshtml` — wrap the main content area (heading through course list) in `<div id="main-content">`; update the `_CourseCard.cshtml` partial to add `hx-get="/Courses/Detail?id=@Model.Id&handler=Detail" hx-target="#main-content" hx-push-url="true"` to the course title link and "View Details" button

- [ ] T024 [US3] Update `src/Host/Pages/Courses/Index.cshtml` — update the "← Back to Catalog" in the partial to restore the course list via `hx-get="/Courses/Index?handler=CourseList" hx-target="#main-content"`

**Checkpoint**: Course detail loads inline via HTMX. Browser URL updates via `hx-push-url`. Back navigation restores the list. Full page refresh lands on the correct page.

---

## Phase 6: User Story 4 - View My Courses with Live Status (Priority: P2)

**Goal**: "My Courses" page displays enrollment status and SCORM data that can refresh via partial swap.

**Independent Test**: Navigate to `/MyCourses` — enrollments shown with status badges. Click refresh — data updates without full reload.

- [ ] T025 [US4] Create `src/Host/Pages/Shared/_EnrollmentList.cshtml` partial — strongly-typed `@model IEnumerable<EnrollmentRow>`, renders the enrollment table using `@foreach` + `@Html.Partial("_MyCourseRow", row)` for each enrollment, with empty state when collection is empty

- [ ] T026 [US4] Add `OnGetEnrollmentsAsync()` handler to `src/Host/Pages/MyCourses/Index.cshtml.cs` — calls `EnrollmentLookup.GetMyEnrollmentsAsync` and `ScormAttemptService.GetMyAttemptsAsync`, joins enrollment data with latest SCORM attempt per course, maps to `IEnumerable<EnrollmentRow>`, returns `Partial("_EnrollmentList", model)`

- [ ] T027 [US4] Update `src/Host/Pages/MyCourses/Index.cshtml` — wrap enrollment rendering in `<div id="enrollment-list" hx-get="/MyCourses?handler=Enrollments" hx-trigger="from:[data-refresh-enrollments]">`; replace inline enrollment rendering with `@await Html.PartialAsync("_EnrollmentList", Model.EnrollmentRows)`; add a refresh button `<button type="button" data-refresh-enrollments class="btn btn-secondary" hx-get="/MyCourses?handler=Enrollments" hx-target="#enrollment-list">↻ Refresh</button>`

**Checkpoint**: "My Courses" displays enrollments with status badges. Refresh button updates data via HTMX partial swap.

---

## Phase 7: User Story 5 - Upload SCORM Packages with Progress Feedback (Priority: P3)

**Goal**: SCORM upload shows progress and result feedback inline without full page reload.

**Independent Test**: Admin navigates to upload page, selects ZIP, uploads — success/error message displays inline.

- [ ] T028 [US5] Read current `src/Host/Pages/Admin/Upload.cshtml` and `Upload.cshtml.cs` to understand the existing upload flow and identify the form action and result handling

- [ ] T029 [US5] Update `src/Host/Pages/Admin/Upload.cshtml` — wrap upload form with `hx-post` pointing to the existing upload handler; add `hx-encoding="multipart/form-data"` for file uploads; add `hx-target="#upload-result"` for result display; add `hx-indicator=".htmx-indicator"` for loading state; add `<div id="upload-result"></div>` below the form for inline feedback; keep standard `method="post" enctype="multipart/form-data" action="..."` as graceful degradation fallback

**Checkpoint**: SCORM upload works via HTMX with inline result feedback. File uploads use `multipart/form-data` encoding.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final touches, error handling consistency, and validation.

- [ ] T030 Add HTMX error handling to all handler methods in `src/Host/Pages/Courses/Index.cshtml.cs`, `Detail.cshtml.cs`, and `MyCourses/Index.cshtml.cs` — wrap service calls in try/catch; on exception, check `Request.Headers["HX-Request"]` and return `Partial("_ErrorPartial", "Unable to load data. Please refresh.")` for HTMX requests

- [ ] T031 [P] Add `hx-on::error` attributes to HTMX targets for client-side error feedback — e.g., `hx-on::htmx:after-request="if(event.detail.failed) alert('Request failed. Please try again.')"`

- [ ] T032 Run `dotnet test tests/ArchitectureTests` to verify no module boundary violations from the new service injections in page models

- [ ] T033 Validate all 8 quickstart scenarios from `quickstart.md` — catalog filter, category filter, enroll inline, already-enrolled feedback, detail inline navigation, my courses refresh, graceful degradation (JS disabled), architecture tests

- [ ] T034 Commit all changes and push to `master` branch

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup)
    ↓
Phase 2 (Foundational) — BLOCKS all user stories
    ↓
Phase 3 (US1) ──→ Phase 4 (US2) ──→ Phase 5 (US3) ──→ Phase 6 (US4) ──→ Phase 7 (US5)
    ↓
Phase 8 (Polish) — depends on all stories
```

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (service injection is required for handler methods)
- **User Stories (Phases 3–7)**: Depend on Foundational. Sequential recommended (US1→US2→US3→US4→US5) due to shared partials and page model dependencies, but US3–US5 have no inter-story dependencies and could run in parallel after US1+US2
- **Polish (Phase 8)**: Depends on all user stories

### User Story Dependencies

| Story | Depends On | Can Parallel With |
|-------|-----------|-------------------|
| US1 (Browse/Filter) | Phase 2 only | US2 (after both start) |
| US2 (Enroll) | Phase 2 only | US1 (after both start) |
| US3 (Detail Navigation) | US1 (uses `_CourseCard` HTMX attrs) | US4, US5 |
| US4 (My Courses) | Phase 2 only | US3, US5 |
| US5 (SCORM Upload) | Phase 1 (HTMX loaded) | US3, US4 |

### Within Each User Story

1. Handler methods (C# code-behind) before view updates (cshtml)
2. Partial views created before pages that reference them
3. Core implementation before error handling

### Parallel Opportunities

- **Phase 1**: T002–T009 are all `[P]` — view models and partial views are independent files
- **Phase 2**: T010–T012 are independent refactors (different page models) — can parallelize if careful about merge conflicts
- **Phase 5 + 6**: US3 and US4 touch different pages (`Courses/Detail` vs `MyCourses/Index`) — no file conflicts
- **Phase 6 + 7**: US4 and US5 touch different pages — no file conflicts

---

## Parallel Example: Phase 1 (Setup)

```
# All Phase 1 tasks after T001 can run in parallel:
Task: "Define CourseItem record in Index.cshtml.cs"          (T002)
Task: "Define CourseDetailItem record in Detail.cshtml.cs"    (T003)
Task: "Define EnrollmentRow record in MyCourses/Index.cshtml.cs" (T004)
Task: "Define EnrollmentResult record in Detail.cshtml.cs"    (T005)
Task: "Create _CourseCard.cshtml"                             (T006)
Task: "Create _EnrollmentResult.cshtml"                       (T007)
Task: "Create _ErrorPartial.cshtml"                           (T008)
Task: "Create _MyCourseRow.cshtml"                            (T009)
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1: Setup (HTMX CDN + shared partials)
2. Complete Phase 2: Foundational (service injection refactor)
3. Complete Phase 3: US1 — Browse and Filter Without Reloads
4. **STOP and VALIDATE**: Course catalog filtering works via HTMX
5. This is the minimum viable product — users get the biggest UX win

### Incremental Delivery

1. MVP: US1 (catalog filtering) → validate
2. Add US2 (enrollment) → validate
3. Add US3 (detail navigation) → validate
4. Add US4 (my courses) → validate
5. Add US5 (SCORM upload) → validate
6. Phase 8: Polish → push to master

### Final Step

After all phases complete and validation passes: commit and push to `master` branch.

---

## Notes

- [P] tasks = different files, no dependencies on each other
- [USn] label maps task to specific user story for traceability
- No test tasks — spec does not request automated tests; validation is via quickstart.md scenarios
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at each checkpoint to validate independently
- T034 is the final push to master branch
