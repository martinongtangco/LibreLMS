# Tasks: Course Catalog & Enrollment

**Input**: Design documents from `/specs/001-course-catalog-enrollment/`

**Prerequisites**: plan.md (tech stack, structure), spec.md (4 user stories), data-model.md (3 entities), contracts/api.md (4 endpoints + 1 cross-module contract), research.md (5 decisions)

**Tests**: Not explicitly requested in the feature specification. Test tasks are excluded.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add EF Core dependencies and configure database connection shared by all modules.

- [X] T001 Add EF Core packages (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`) to `src/Modules/Catalog/Catalog.csproj`
- [X] T002 [P] Add EF Core packages (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`) to `src/Modules/Enrollment/Enrollment.csproj`
- [X] T003 [P] Add `Microsoft.AspNetCore.Authentication.JwtBearer` and `Microsoft.AspNetCore.Authentication.Cookies` to `src/Host/Host.csproj` for authentication
- [X] T004 [P] Add connection string key to `src/Host/appsettings.Development.json` for MSSQL (`ConnectionStrings:DefaultConnection`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types, contracts, and DI wiring that MUST exist before any user story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Define `CourseSummary` record in `src/Modules/Catalog.Contracts/CourseSummary.cs` (fields: `Guid Id`, `string Title`)
- [X] T006 Define `ICourseLookup` interface in `src/Modules/Catalog.Contracts/ICourseLookup.cs` (`Task<CourseSummary?> GetCourseAsync(Guid courseId)`)
- [X] T007 [P] Add project reference from `src/Modules/Enrollment/Enrollment.csproj` to `src/Modules/Catalog.Contracts/Catalog.Contracts.csproj`
- [X] T008 Create `CatalogDbContext` in `src/Modules/Catalog/Infrastructure/CatalogDbContext.cs` (extends `DbContext`, owns `Courses` table)
- [X] T009 [P] Create `EnrollmentDbContext` in `src/Modules/Enrollment/Infrastructure/EnrollmentDbContext.cs` (extends `DbContext`, owns `Students` and `Enrollments` tables)
- [X] T010 Create module registration extension `CatalogModuleExtensions` in `src/Modules/Catalog/Endpoints/CatalogModuleExtensions.cs` (`IEndpointRouteBuilder.MapCatalogEndpoints()`)
- [X] T011 [P] Create module registration extension `EnrollmentModuleExtensions` in `src/Modules/Enrollment/Endpoints/EnrollmentModuleExtensions.cs` (`IEndpointRouteBuilder.MapEnrollmentEndpoints()`)
- [X] T012 Update `src/Host/Program.cs` to: configure EF Core with MSSQL connection string, register both `DbContext` types, call `MapCatalogEndpoints()` and `MapEnrollmentEndpoints()`, add authentication middleware
- [X] T013 Create simple seed data method for test students in `src/Modules/Enrollment/Infrastructure/EnrollmentSeeder.cs` (seeds 2-3 students with known credentials)

**Checkpoint**: Foundation ready — database contexts configured, contracts defined, DI wired. User story implementation can now begin.

---

## Phase 3: User Story 1 - Browse Available Courses (Priority: P1) 🎯 MVP

**Goal**: Students can browse a list of available courses with title, description, category, and duration. They can filter by course name or category.

**Independent Test**: Navigate to `/api/courses` and verify a JSON list of courses is returned. Navigate to `/` (Razor Pages) and verify the catalog page renders.

### Implementation for User Story 1

- [X] T014 [US1] Create `Course` entity in `src/Modules/Catalog/Domain/Course.cs` (extends `Entity<Guid>`, fields: `Title`, `ShortDescription`, `FullDescription`, `Category`, `Duration`, `CreatedAt`)
- [X] T015 [P] [US1] Create `CourseDto` record in `src/Modules/Catalog/Endpoints/CourseDto.cs` (DTO for catalog listing: `Id`, `Title`, `ShortDescription`, `Category`, `Duration`)
- [X] T016 [US1] Implement `CourseCatalogService` in `src/Modules/Catalog/Application/CourseCatalogService.cs` (methods: `ListAsync(search, category)`, `GetByIdAsync(id)`) using `CatalogDbContext`
- [X] T017 [US1] Implement `GET /api/courses` endpoint in `src/Modules/Catalog/Endpoints/CatalogEndpoints.cs` (with optional `search` and `category` query parameters, returns 200 with course list)
- [X] T018 [US1] Implement `GET /api/courses/{id}` endpoint in `src/Modules/Catalog/Endpoints/CatalogEndpoints.cs` (returns 200 with full course details or 404)
- [X] T019 [US1] Implement `CatalogSeeder` in `src/Modules/Catalog/Infrastructure/CatalogSeeder.cs` (seeds 10-15 sample courses across 3-4 categories)
- [X] T020 [US1] Wire seeder into `src/Host/Program.cs` (run on first startup if no courses exist, using `DbEnsureCreated` or migration + seed pattern)
- [X] T021 [US1] Create catalog index Razor Page in `src/Host/Pages/Courses/Index.cshtml` and `Index.cshtml.cs` (displays course list with filter inputs, links to course detail)
- [X] T022 [US1] Wire catalog page routes in `src/Host/Program.cs` (`/` and `/courses` map to catalog index)

**Checkpoint**: At this point, students can browse and filter the course catalog. API and web portal both functional.

---

## Phase 4: User Story 2 - Enroll in a Course (Priority: P1) 🎯 MVP

**Goal**: An authenticated student views a course detail page and enrolls. Enrollment is confirmed immediately. Duplicate enrollment is prevented (409).

**Independent Test**: POST to `/api/enrollments` with a valid course ID and auth token, verify 201 Created. POST again, verify 409 Conflict.

### Implementation for User Story 2

- [X] T023 [US2] Create `Student` entity in `src/Modules/Enrollment/Domain/Student.cs` (extends `Entity<Guid>`, fields: `Name`, `Email`, `CreatedAt`)
- [X] T024 [P] [US2] Create `Enrollment` entity in `src/Modules/Enrollment/Domain/Enrollment.cs` (extends `Entity<Guid>`, fields: `StudentId`, `CourseId`, `EnrolledAt`)
- [X] T025 [US2] Add unique index constraint on `(StudentId, CourseId)` in `EnrollmentDbContext.OnModelCreating` to enforce FR-005 at the database level
- [X] T026 [US2] Implement `ICourseLookup` in `src/Modules/Catalog/Application/CourseLookup.cs` (uses `CatalogDbContext` to fetch `CourseSummary` by ID)
- [X] T027 [US2] Register `ICourseLookup` in `src/Modules/Catalog/Endpoints/CatalogModuleExtensions.cs` DI registration
- [X] T028 [US2] Implement `EnrollmentService` in `src/Modules/Enrollment/Application/EnrollmentService.cs` (method: `EnrollAsync(studentId, courseId)` — validates course exists via `ICourseLookup`, checks for duplicate, creates `Enrollment` entity, saves via `EnrollmentDbContext`)
- [X] T029 [P] [US2] Create `EnrollRequest` DTO in `src/Modules/Enrollment/Endpoints/EnrollRequest.cs` (field: `Guid CourseId`)
- [X] T030 [P] [US2] Create `EnrollmentDto` record in `src/Modules/Enrollment/Endpoints/EnrollmentDto.cs` (fields: `Id`, `StudentId`, `CourseId`, `EnrolledAt`)
- [X] T031 [US2] Implement `POST /api/enrollments` endpoint in `src/Modules/Enrollment/Endpoints/EnrollmentEndpoints.cs` (requires authentication, calls `EnrollmentService.EnrollAsync`, returns 201 on success, 409 on duplicate, 400 on invalid course ID)
- [X] T032 [US2] Update course detail Razor Page to show "Enroll" button in `src/Host/Pages/Courses/Detail.cshtml` and `Detail.cshtml.cs` (calls POST `/api/enrollments` on click, shows confirmation message)
- [X] T033 [US2] Create course detail Razor Page at `src/Host/Pages/Courses/Detail.cshtml` and `Detail.cshtml.cs` (shows full course info + enroll button / enrolled status)

**Checkpoint**: At this point, students can browse courses and enroll. Both API and web portal support the full enroll flow.

---

## Phase 5: User Story 3 - View Enrolled Courses (Priority: P2)

**Goal**: A student sees a list of courses they are enrolled in, with course title and enrollment date. Empty state shown when no enrollments exist.

**Independent Test**: After enrolling in a course, GET `/api/enrollments/my` with auth token returns the enrollment. Verify Razor Pages "My Courses" page renders correctly.

### Implementation for User Story 3

- [X] T034 [US3] Extend `EnrollmentService` in `src/Modules/Enrollment/Application/EnrollmentService.cs` with method `GetMyEnrollmentsAsync(studentId)` — returns list of enrollments with course titles (via `ICourseLookup` to resolve course names)
- [X] T035 [US3] Create `MyEnrollmentDto` record in `src/Modules/Enrollment/Endpoints/MyEnrollmentDto.cs` (fields: `Id`, `CourseId`, `CourseTitle`, `EnrolledAt`)
- [X] T036 [US3] Implement `GET /api/enrollments/my` endpoint in `src/Modules/Enrollment/Endpoints/EnrollmentEndpoints.cs` (requires authentication, returns 200 with enrollment list including course titles)
- [X] T037 [US3] Create "My Courses" Razor Page at `src/Host/Pages/MyCourses/Index.cshtml` and `Index.cshtml.cs` (displays enrolled courses list with links to course details, empty state message when no enrollments)
- [X] T038 [US3] Add navigation link to "My Courses" in the shared layout at `src/Host/Pages/Shared/_Layout.cshtml`

**Checkpoint**: Students can now view their enrolled courses via both API and web portal.

---

## Phase 6: User Story 4 - View Course Details (Priority: P2)

**Goal**: A student views full course information with enrollment status indicator. This task refines the course detail page already partially created in US2.

**Independent Test**: Navigate to course detail page for an enrolled course — verify "Enrolled" status shown. Navigate to detail for a non-enrolled course — verify "Enroll" button shown.

### Implementation for User Story 4

- [X] T039 [US4] Extend `CourseCatalogService` in `src/Modules/Catalog/Application/CourseCatalogService.cs` with method `GetCourseWithEnrollmentAsync(courseId, studentId)` — returns course details plus enrollment status
- [X] T040 [US4] Create `CourseDetailDto` record in `src/Modules/Catalog/Endpoints/CourseDetailDto.cs` (fields: all course fields + `bool IsEnrolled` for the current student)
- [X] T041 [US4] Update `GET /api/courses/{id}` endpoint to accept optional `studentId` query parameter and include `isEnrolled` field in response
- [X] T042 [US4] Update course detail Razor Page (`src/Host/Pages/Courses/Detail.cshtml.cs`) to fetch enrollment status and render "Enrolled" badge or "Enroll" button accordingly
- [X] T043 [US4] Update catalog listing Razor Page (`src/Host/Pages/Courses/Index.cshtml`) to show enrollment status indicator on each course card for authenticated users

**Checkpoint**: All four user stories are now independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories.

- [X] T044 [P] Update `src/Host/Pages/Shared/_Layout.cshtml` with navigation, branding, and responsive layout
- [X] T045 Add empty state handling to catalog page when no courses exist (FR-008)
- [X] T046 [P] Add error handling page at `src/Host/Pages/Error.cshtml` for 404 and 500 errors
- [X] T047 Add `dotnet ef migrations add` migration files for both `CatalogDbContext` and `EnrollmentDbContext`
- [X] T048 Update `tests/ArchitectureTests/ModuleBoundaryTests.cs` if any new types require boundary verification
- [X] T049 [P] Update `tests/Catalog.Tests/` and `tests/Enrollment.Tests/` placeholder tests to verify module assemblies load with new types
- [X] T050 Run full `dotnet build` and `dotnet test` to verify everything compiles and tests pass
- [X] T051 Validate against `quickstart.md` scenarios (all 8 validation checks)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - US1 and US2 are P1 and should be completed before US3 and US4
  - US3 depends on US2 (enrollment must exist before listing)
  - US4 depends on US1 (course detail refines the catalog listing)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories
- **US2 (P1)**: Can start after Foundational (Phase 2) — depends on `ICourseLookup` from US1's contract (defined in Phase 2)
- **US3 (P2)**: Depends on US2 completion (enrollment data must exist)
- **US4 (P2)**: Depends on US1 completion (course detail refines catalog)

### Within Each User Story

- Models before services
- Services before endpoints
- API endpoints before Razor Pages
- Core implementation before UI polish

### Parallel Opportunities

- T001 and T002 and T003 and T004: All setup tasks are independent
- T005, T006, T008, T009, T010, T011: Foundational tasks touching different files
- T014 and T015: Course entity and DTO can be created in parallel
- T023 and T024: Student and Enrollment entities can be created in parallel
- T044, T045, T046: Polish tasks are independent

---

## Parallel Example: User Story 1

```
# Launch in parallel (different files):
T014: Create Course entity in src/Modules/Catalog/Domain/Course.cs
T015: Create CourseDto in src/Modules/Catalog/Endpoints/CourseDto.cs

# After both complete:
T016: Implement CourseCatalogService (depends on Course entity + DbContext)
T017: Implement GET /api/courses endpoint (depends on CourseCatalogService + CourseDto)
T018: Implement GET /api/courses/{id} endpoint (depends on CourseCatalogService)

# After API is functional:
T021: Create catalog Razor Page (consumes API endpoints)
```

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US1 — Browse Available Courses
4. Complete Phase 4: US2 — Enroll in a Course
5. **STOP and VALIDATE**: Run quickstart.md scenarios 1-6
6. Demo: Student can browse catalog, view details, and enroll

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Browse catalog → Validate
3. Add US2 → Enroll → Validate (MVP!)
4. Add US3 → View enrolled courses → Validate
5. Add US4 → Course detail with enrollment status → Validate
6. Polish → Layout, error handling, migrations
7. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies on other incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Architecture tests must pass at every checkpoint (Constitution Principle III)
