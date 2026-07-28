# Tasks: Fix Critical Gaps

**Input**: Design documents from `/specs/003-fix-critical-gaps/`

**Prerequisites**: spec.md (5 user stories with priorities), existing codebase analysis

**Tests**: Not explicitly requested in the feature specification. Test tasks are excluded.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No new infrastructure needed — this is a bug-fix slice against an existing working codebase. All projects, packages, and DI wiring already exist.

*(No setup tasks — proceed directly to Foundational phase.)*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Fixes that must be resolved before any user story can be independently tested. The navigation tag-helpers fix is foundational because every page depends on working navigation.

- [ ] T001 Create `src/Host/Pages/_ViewImports.cshtml` with `@using LearningLms.Host`, `@using LearningLms.Host.Pages.Courses`, `@using LearningLms.Host.Pages.MyCourses`, `@using LearningLms.Host.Pages.Admin`, `@using LearningLms.Host.Pages.Scorm`, `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`

**Checkpoint**: All Razor pages render tag helpers correctly — navigation links resolve to proper URLs.

---

## Phase 3: User Story 1 - SCORM Course Launches and Displays Content (Priority: P1) 🎯 MVP

**Goal**: An enrolled student can launch a SCORM course and see the course content rendered in the browser with an active session.

**Independent Test**: Start the app, seed data runs — one seeded course has a SCORM package. Enroll the seeded student in that course. Click "Launch" — verify the SCORM `index.html` renders inside the iframe and the session is created in Valkey.

### Implementation for User Story 1

- [ ] T002 [US1] Extend `ScormSessionService.LaunchAsync` in `src/Modules/Scorm/Application/ScormSessionService.cs` to accept `ScormPackageService` (or look up the package internally) and compute `contentUrl` from `ScormPackage.ContentDirectory + "/" + ScormPackage.LaunchPath`
- [ ] T003 [US1] Update `LaunchResult` record in `src/Modules/Scorm/Application/ScormSessionService.cs` to include a `string? ContentUrl` property; update `CreateSuccess()` factory to accept and set it
- [ ] T004 [US1] Update `POST /api/scorm/{courseId}/launch` endpoint in `src/Host/Program.cs` to inject `ScormPackageService`, resolve the package, compute `contentUrl`, and include it in the JSON response (`{ sessionId, contentUrl, entry, attemptNumber }`)
- [ ] T005 [US1] Update `LaunchResponse` record in `src/Host/Pages/Scorm/Launch.cshtml.cs` to match the API response (ensure `ContentUrl` is deserialized correctly)
- [ ] T006 [US1] Update `ScormSeeder.SeedAsync` in `src/Modules/Scorm/Infrastructure/ScormSeeder.cs` to use a real seeded course ID (e.g., `11111111-1111-1111-1111-111111111111` from `CatalogSeeder`) instead of `Guid.Zero`
- [ ] T007 [US1] Update `EnrollmentSeeder.Seed` in `src/Modules/Enrollment/Infrastructure/EnrollmentSeeder.cs` to create an enrollment linking the seeded student (`550e8400-e29b-41d4-a716-446655440001`) to the course that has the SCORM package, so the demo flow works out of the box
- [ ] T008 [US1] Ensure `src/Host/Program.cs` startup seeding logic runs `ScormSeeder.SeedAsync` before `EnrollmentSeeder.Seed` (or that enrollment seeding references the correct course ID after SCORM seeding), so the demo data is self-consistent

**Checkpoint**: At this point, the seeded demo course is a launchable SCORM course. The launch endpoint returns `contentUrl`. The iframe renders course content.

---

## Phase 4: User Story 2 - Navigation Links Work Across All Pages (Priority: P1)

**Goal**: All navigation links in the shared layout resolve to correct URLs and navigate between pages properly.

**Independent Test**: Start the app, click each of the 3 navigation links — verify each destination page loads with its expected content.

### Implementation for User Story 2

- [ ] T009 [P] [US2] Verify `src/Host/Pages/Shared/_Layout.cshtml` navigation links render correctly after `_ViewImports.cshtml` (T001) is in place; test each link (`/Courses/Index`, `/MyCourses/Index`, `/Admin/Upload`) resolves to a working page

**Checkpoint**: All 3 navigation links work. Pages render with correct content.

---

## Phase 5: User Story 3 - Admin Creates a New Course (Priority: P2)

**Goal**: An admin can create a new course through the web UI. The course is persisted and immediately visible in the catalog.

**Independent Test**: Navigate to an admin course creation page, fill in title/description/category/duration, submit — verify the course appears in the catalog listing and has a detail page.

### Implementation for User Story 3

- [ ] T010 [P] [US3] Create `CreateCourseRequest` record in `src/Modules/Catalog/Endpoints/CreateCourseRequest.cs` (fields: `string Title`, `string ShortDescription`, `string FullDescription`, `string Category`, `string Duration`)
- [ ] T011 [US3] Add `CreateAsync(CreateCourseRequest request)` method to `CourseCatalogService` in `src/Modules/Catalog/Application/CourseCatalogService.cs` — creates a `Course` entity with `Guid.NewGuid()`, saves via `CatalogDbContext`
- [ ] T012 [US3] Add `POST /api/courses` endpoint in `src/Host/Program.cs` — requires `[Authorize(Roles = "Admin")]`, accepts JSON body `CreateCourseRequest`, calls `CourseCatalogService.CreateAsync`, returns `201 Created` with the new course's `CourseDto`
- [ ] T013 [P] [US3] Create `src/Host/Pages/Admin/Courses/Create.cshtml.cs` — a Razor Page model with `BindProperty` fields for `Title`, `ShortDescription`, `FullDescription`, `Category`, `Duration`, and an `OnPostAsync` that calls `POST /api/courses` via `IHttpClientFactory` and redirects on success
- [ ] T014 [P] [US3] Create `src/Host/Pages/Admin/Courses/Create.cshtml` — a form with inputs for course fields (title, short description, full description textarea, category, duration), validation messages, and a submit button
- [ ] T015 [US3] Add a "Create Course" link in the navigation bar at `src/Host/Pages/Shared/_Layout.cshtml` (visible alongside or near the existing admin upload link)

**Checkpoint**: Admins can create courses through the web UI. New courses appear in the catalog.

---

## Phase 6: User Story 4 - Admin Uploads SCORM Package for a Course (Priority: P2)

**Goal**: An admin uploads a SCORM ZIP and selects a course from a dropdown. The upload page works in Docker (no hardcoded localhost). Success/error messages are clear.

**Independent Test**: Create a course (or use a seeded one), navigate to Upload SCORM, select the course from a dropdown, upload a SCORM ZIP — verify success. Upload an invalid ZIP — verify error message.

### Implementation for User Story 4

- [ ] T016 [US4] Update `ScormUploadModel` in `src/Host/Pages/Admin/Upload.cshtml.cs` to inject `IHttpClientFactory` instead of creating a raw `HttpClient` with hardcoded `http://localhost:5000`; use `httpClientFactory.CreateClient()` with relative URLs
- [ ] T017 [P] [US4] Add a `List<CourseSummary> Courses` property to `ScormUploadModel` in `src/Host/Pages/Admin/Upload.cshtml.cs`; populate it in `OnGetAsync` by calling `GET /api/courses` via the injected `HttpClient`
- [ ] T018 [P] [US4] Update `src/Host/Pages/Admin/Upload.cshtml` to replace the manual course GUID text input with a `<select>` dropdown bound to the `Courses` list, displaying `Title` and posting the `Id` as `courseId`
- [ ] T019 [US4] Ensure the upload `OnPostAsync` in `src/Host/Pages/Admin/Upload.cshtml.cs` uses `IHttpClientFactory.CreateClient()` for the `POST /api/scorm/upload` call (relative URL, no hardcoded host)

**Checkpoint**: Upload page shows a course dropdown, uses relative URLs, and works in Docker environments.

---

## Phase 7: User Story 5 - Student Authentication Works (Priority: P2)

**Goal**: Students log in with seeded credentials. Their identity is persisted via cookies. Authenticated endpoints use the real student ID from claims instead of a hardcoded fallback.

**Independent Test**: Access `/api/enrollments` (POST) without login — redirected to `/Account/Login`. Log in as "Alice" — verify subsequent requests identify her correctly (not another student).

### Implementation for User Story 5

- [ ] T020 [P] [US5] Add `PasswordHash` field to `Student` entity in `src/Modules/Enrollment/Domain/Student.cs` — a simple string field for storing a hashed password
- [ ] T021 [P] [US5] Add `PasswordHash` column to `EnrollmentDbContext.OnModelCreating` in `src/Modules/Enrollment/Infrastructure/EnrollmentDbContext.cs` (max length 256)
- [ ] T022 [US5] Update `EnrollmentSeeder.Seed` in `src/Modules/Enrollment/Infrastructure/EnrollmentSeeder.cs` to set `PasswordHash` for each seeded student — use `BCrypt.Net-Next` or a simple `SHA256` hash of a known password (e.g., "password123") so seeded credentials are known
- [ ] T023 [P] [US5] Add `BCrypt.Net-Next` (or `System.Security.Cryptography` for SHA256) package reference to `src/Host/Host.csproj` if not already present
- [ ] T024 [US5] Create `src/Host/Pages/Account/Login.cshtml.cs` — a Razor Page model with `Email` and `Password` bind properties, `OnPostAsync` that queries `EnrollmentDbContext.Students` by email, verifies the password hash, and signs in with `HttpContext.SignInAsync` using `ClaimsPrincipal` with `ClaimTypes.NameIdentifier` set to the student's `Id`
- [ ] T025 [P] [US5] Create `src/Host/Pages/Account/Login.cshtml` — a login form with email and password inputs, an error message display, and a submit button; styled to match the existing layout
- [ ] T026 [US5] Update `GetStudentId` in `src/Host/Program.cs` to remove the hardcoded demo fallback — if no valid claim is found, return a new `Guid()` or throw (relying on the `[Authorize]` attribute to enforce login)
- [ ] T027 [US5] Add a "Logout" link in `src/Host/Pages/Shared/_Layout.cshtml` that posts to a logout endpoint (or use an `asp-page="/Account/Logout"` link if a logout page is created)
- [ ] T028 [P] [US5] Create `src/Host/Pages/Account/Logout.cshtml.cs` — signs out the user with `HttpContext.SignOutAsync()` and redirects to `/Account/Login`
- [ ] T029 [US5] Add "Admin" seeded student in `EnrollmentSeeder.Seed` with a known admin password, and add `Roles = "Admin"` claim during sign-in (check `Student.Email` or add a `Roles` field to `Student`) so the upload endpoint's `[Authorize(Roles = "Admin")]` can be satisfied

**Checkpoint**: Students can log in, their identity persists across requests, admin login grants upload access, and the hardcoded student ID fallback is removed.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and overall code quality.

- [ ] T030 [P] Remove unused `@using` directives from individual `.cshtml` files now that `_ViewImports.cshtml` handles them
- [ ] T031 [P] Add `wwwroot/scorm-content/` to `.gitignore` to exclude runtime-extracted content
- [ ] T032 Update `src/Host/Migrations/Catalog/` and `src/Host/Migrations/Enrollment/` with new migration for `Student.PasswordHash` column (run `dotnet ef migrations add AddPasswordHashToStudent` for `EnrollmentDbContext`)
- [ ] T033 [P] Update `src/Host/Pages/Shared/_Layout.cshtml` navigation to conditionally show "Create Course" and "Logout" links based on auth state (e.g., show Login/Logout appropriately)
- [ ] T034 Run full `dotnet build` and `dotnet test` to verify everything compiles and tests pass
- [ ] T035 Validate end-to-end demo flow: seed data runs on startup, seeded course has SCORM package, seeded student is enrolled, login works, launch shows content

**Checkpoint**: All fixes verified. Build passes. Demo flow works from a clean start.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 2)**: T001 — must complete first (tag helpers are needed by all pages)
- **User Stories (Phase 3-7)**: All depend on Phase 2 completion
  - **US1 (P1)** and **US2 (P1)** can run in parallel after Phase 2
  - **US3 (P2)** and **US4 (P2)** are independent of US1/US2 but US4 benefits from US3 (course dropdown is more useful when you can create courses)
  - **US5 (P2)** is independent of US1-US4 but should complete before final validation (T035)
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (SCORM Launch)**: Depends on Phase 2 only. T008 requires knowledge of seeded course IDs from both `CatalogSeeder` and `ScormSeeder`.
- **US2 (Navigation)**: Depends on Phase 2 only (T001 IS the main fix; T009 is verification).
- **US3 (Course Creation)**: Depends on Phase 2 only. T015 modifies `_Layout.cshtml` — may conflict with T033 (coordinate).
- **US4 (Upload Improvements)**: Depends on Phase 2 only. Benefits from US3 (course dropdown) but does not require it.
- **US5 (Authentication)**: Depends on Phase 2 only. T029 (admin student) should complete before final validation.

### Within Each User Story

- Model/entity changes before service changes
- Service changes before endpoint changes
- API endpoint changes before Razor Page changes
- Core fixes before UI polish

### Parallel Opportunities

- T001 (Phase 2) is the sole foundational task — complete first
- T002-T004 (US1 service + API) touch different files but share `ScormSessionService.cs` — T002 and T003 are in the same file, so they must be sequential; T004 is in `Program.cs` and can parallel with T006-T008
- T006 (ScormSeeder) and T007 (EnrollmentSeeder) are independent — can run in parallel
- T010-T011 (US3 models + service) are in different files — can run in parallel
- T013 and T014 (US3 Razor Page .cs + .cshtml) are independent — can run in parallel
- T016 and T017 (US4 upload model changes) are in the same file — sequential; T018 (UI) is independent
- T020-T022 (US5 model + migration + seeder) are independent — can run in parallel
- T024 and T025 (US5 login page .cs + .cshtml) are independent — can run in parallel

---

## Parallel Example: User Story 1

```
# After Phase 2 (T001), launch in parallel:
T006: Update ScormSeeder to use real course ID (src/Modules/Scorm/Infrastructure/ScormSeeder.cs)
T007: Update EnrollmentSeeder to enroll student in SCORM course (src/Modules/Enrollment/Infrastructure/EnrollmentSeeder.cs)

# These are independent — different files, different modules
# Then complete (sequential within same file):
T002: Extend ScormSessionService.LaunchAsync (src/Modules/Scorm/Application/ScormSessionService.cs)
T003: Update LaunchResult record (same file as T002)

# After service is ready:
T004: Update launch endpoint (src/Host/Program.cs)
T005: Update LaunchResponse in page model (src/Host/Pages/Scorm/Launch.cshtml.cs)
T008: Ensure seed ordering in Program.cs (src/Host/Program.cs)
```

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 2: T001 (tag helpers)
2. Complete Phase 3: US1 — SCORM launch with ContentUrl, linked seed data
3. Complete Phase 4: US2 — Navigation verification
4. **STOP and VALIDATE**: Seeded course has SCORM package, student is enrolled, launch shows content, navigation works
5. Demo: Student can launch the seeded SCORM course and see content

### Incremental Delivery

1. Phase 2 → Navigation works
2. Add US1 → SCORM launch works with seed data
3. Add US3 → Admin can create courses
4. Add US4 → Upload page works with dropdown and no hardcoded URLs
5. Add US5 → Login/logout works, real auth instead of hardcoded fallback
6. Polish → Migrations, cleanup, end-to-end validation
7. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies on other incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Architecture tests must pass at every checkpoint (Constitution Principle III)
- This is a bug-fix slice — no new modules or Contracts projects are needed
- All changes stay within existing module boundaries
