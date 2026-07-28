# Tasks: SCORM Launch & Completion

**Input**: Design documents from `/specs/002-scorm-launch-completion/`

**Prerequisites**: plan.md (tech stack, structure), spec.md (5 user stories), data-model.md (3 entities + 1 contract), contracts/api.md (8 endpoints + 1 cross-module contract), research.md (8 decisions)

**Tests**: Not explicitly requested in the feature specification. Test tasks are excluded.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add EF Core, StackExchange.Redis, and project references needed by the Scorm module.

- [X] T001 Add EF Core packages (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`) to `src/Modules/Scorm/Scorm.csproj`
- [X] T002 [P] Add `StackExchange.Redis` package to `src/Modules/Scorm/Scorm.csproj` for Valkey session storage
- [X] T003 [P] Add project reference from `src/Modules/Scorm/Scorm.csproj` to `src/Modules/Catalog.Contracts/Catalog.Contracts.csproj`
- [X] T004 Add Valkey connection string key to `src/Host/appsettings.Development.json` (`ConnectionStrings:Valkey`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types, contracts, DI wiring, and infrastructure that MUST exist before any user story can be implemented. Includes the new `IEnrollmentLookup` cross-module contract.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Define `IEnrollmentLookup` interface in `src/Modules/Enrollment.Contracts/IEnrollmentLookup.cs` (`Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId)`)
- [X] T006 Implement `EnrollmentLookup` in `src/Modules/Enrollment/Application/EnrollmentLookup.cs` (queries `EnrollmentDbContext.Enrollments`)
- [X] T007 Register `IEnrollmentLookup` in `src/Modules/Enrollment/Endpoints/EnrollmentModuleExtensions.cs` DI registration
- [X] T008 Add project reference from `src/Modules/Scorm/Scorm.csproj` to `src/Modules/Enrollment.Contracts/Enrollment.Contracts.csproj`
- [X] T009 Create `ScormPackage` entity in `src/Modules/Scorm/Domain/ScormPackage.cs` (extends `Entity<Guid>`, fields: `CourseId`, `ManifestTitle`, `LaunchPath`, `ContentDirectory`, `CreatedAt`)
- [X] T010 [P] Create `CourseAttempt` entity in `src/Modules/Scorm/Domain/CourseAttempt.cs` (extends `Entity<Guid>`, fields: `StudentId`, `CourseId`, `AttemptNumber`, `Status`, `ScoreRaw`, `SessionTime`, `SuspendData`, `StartedAt`, `CompletedAt`, `LastCommitAt`)
- [X] T011 Create `ScormDbContext` in `src/Modules/Scorm/Infrastructure/ScormDbContext.cs` (extends `DbContext`, owns `ScormPackages` and `CourseAttempts` tables, with unique index on `(StudentId, CourseId, AttemptNumber)`)
- [X] T012 [P] Create `ScormSessionStore` in `src/Modules/Scorm/Infrastructure/ScormSessionStore.cs` (Valkey-backed session state using `StackExchange.Redis` — methods: `CreateSessionAsync`, `SetValueAsync`, `GetValueAsync`, `CommitAsync`, `DeleteSessionAsync`, with 30-minute TTL)
- [X] T013 [P] Create `ManifestParser` in `src/Modules/Scorm/Infrastructure/ManifestParser.cs` (parse `imsmanifest.xml` via `XDocument`, extract launch SCO path and manifest title)
- [X] T014 Create module registration extension `ScormModuleExtensions` in `src/Modules/Scorm/Endpoints/ScormModuleExtensions.cs` (`IEndpointRouteBuilder.MapScormEndpoints()`)
- [X] T015 Update `src/Host/Program.cs` to: register `ScormDbContext` with MSSQL, register `IConnectionMultiplexer` for Valkey, register `ScormModule`, add `MapScormEndpoints()`
- [X] T016 Create `ScormSeeder` in `src/Modules/Scorm/Infrastructure/ScormSeeder.cs` (seeds a minimal sample SCORM package with a dummy manifest and content for demo)

**Checkpoint**: Foundation ready — Scorm module has entities, DbContext, session store, manifest parser, DI wiring, and cross-module contracts. User story implementation can now begin.

---

## Phase 3: User Story 1 - Launch a SCORM Course (Priority: P1) 🎯 MVP

**Goal**: An enrolled student can launch a SCORM course, seeing the course content with a live session initialized.

**Independent Test**: Navigate to `/scorm/launch/{courseId}` for an enrolled SCORM course — verify content renders and session is initialized in Valkey.

### Implementation for User Story 1

- [X] T017 [US1] Implement `ScormPackageService` in `src/Modules/Scorm/Application/ScormPackageService.cs` (methods: `GetPackageByCourseIdAsync(courseId)`, `FindLaunchPath(courseId)`) using `ScormDbContext`
- [X] T018 [US1] Implement `ScormSessionService` in `src/Modules/Scorm/Application/ScormSessionService.cs` (method: `LaunchAsync(studentId, courseId)` — validates enrollment via `IEnrollmentLookup`, checks for active session, creates session in Valkey with default CMI values, creates `CourseAttempt` record in MSSQL)
- [X] T019 [US1] Implement `POST /api/scorm/{courseId}/launch` endpoint in `src/Modules/Scorm/Endpoints/ScormEndpoints.cs` (requires authentication, returns sessionId, contentUrl, apiUrl, entry mode)
- [X] T020 [US1] Create SCORM wrapper Razor Page at `src/Host/Pages/Scorm/Launch.cshtml` and `Launch.cshtml.cs` (minimal layout, injects SCORM API script, shows course content via iframe or embedded HTML)
- [X] T021 [US1] Configure static file serving for `wwwroot/scorm-content/` in `src/Host/Program.cs`
- [X] T022 [US1] Wire seeder into `src/Host/Program.cs` (seed sample SCORM package on first startup if no packages exist)
- [X] T023 [US1] Update course detail page (`src/Host/Pages/Courses/Detail.cshtml`) to show "Launch" button for SCORM courses (check if course has a ScormPackage)
- [X] T024 [US1] Update `GET /api/courses/{id}` to include `isScorm` and `scormPackageId` fields in the response for course detail UI

**Checkpoint**: At this point, enrolled students can launch a seeded SCORM course and see the content with an active session.

---

## Phase 4: User Story 2 - Track Course Progress During Session (Priority: P1) 🎯 MVP

**Goal**: During a live SCORM session, the system captures `LMSSetValue`/`LMSGetValue` calls and maintains session state in Valkey. Score validation rejects out-of-range values.

**Independent Test**: During an active session, POST setValue for `cmi.core.lesson_status` and verify getValue returns the same value. Test score boundary rejection (105 → error).

### Implementation for User Story 2

- [X] T025 [US2] Extend `ScormSessionService` with `SetValueAsync(sessionId, element, value)` — validates CMI fields (score 0-100, valid status values), stores in Valkey via `ScormSessionStore`
- [X] T026 [US2] Extend `ScormSessionService` with `GetValueAsync(sessionId, element)` — reads from Valkey via `ScormSessionStore`
- [X] T027 [US2] Implement `POST /api/scorm/session/{sessionId}/setValue` endpoint in `src/Modules/Scorm/Endpoints/ScormSessionEndpoints.cs` (returns success/error with SCORM error code)
- [X] T028 [P] [US2] Implement `GET /api/scorm/session/{sessionId}/getValue` endpoint in `src/Modules/Scorm/Endpoints/ScormSessionEndpoints.cs` (returns value string)
- [X] T029 [US2] Create SCORM API JavaScript shim in `src/Modules/Scorm/Endpoints/ScormApiScriptEndpoint.cs` — serves `api.js` that defines `window.API` with `LMSInitialize`, `LMSFinish`, `LMSGetValue`, `LMSSetValue`, `LMSCommit` calling server endpoints via `fetch()`
- [X] T030 [US2] Add `beforeunload` handler to SCORM wrapper page (`src/Host/Pages/Scorm/Launch.cshtml`) that calls `/api/scorm/session/{sessionId}/commit` on tab close

**Checkpoint**: At this point, SCORM content can communicate with the system via the API shim — set and get values, with score validation.

---

## Phase 5: User Story 3 - Commit Completion and View Results (Priority: P2)

**Goal**: When a student finishes or commits a SCORM session, the system saves durable records (status, score, time) to MSSQL. The student can view their attempts and results.

**Independent Test**: Complete a SCORM session (set score, call finish), reload page, verify completion record persists and appears in "My Courses".

### Implementation for User Story 3

- [X] T031 [US3] Extend `ScormSessionService` with `CommitAsync(sessionId)` — reads full CMI bag from Valkey, updates/creates `CourseAttempt` in MSSQL with status, score, session time, suspend data
- [X] T032 [US3] Extend `ScormSessionService` with `FinishAsync(sessionId, exitReason)` — commits data, sets `CompletedAt`, deletes Valkey session (cleanup)
- [X] T033 [US3] Implement `POST /api/scorm/session/{sessionId}/commit` endpoint in `src/Modules/Scorm/Endpoints/ScormSessionEndpoints.cs`
- [X] T034 [P] [US3] Implement `POST /api/scorm/session/{sessionId}/finish` endpoint in `src/Modules/Scorm/Endpoints/ScormSessionEndpoints.cs`
- [X] T035 [US3] Implement `ScormAttemptService` in `src/Modules/Scorm/Application/ScormAttemptService.cs` (method: `GetMyAttemptsAsync(studentId)` — returns attempts with course titles via `ICourseLookup`)
- [X] T036 [US3] Implement `GET /api/scorm/attempts/my` endpoint in `src/Modules/Scorm/Endpoints/ScormEndpoints.cs` (requires authentication, returns attempts with course titles)
- [X] T037 [US3] Update "My Courses" Razor Page (`src/Host/Pages/MyCourses/Index.cshtml`) to display SCORM attempt status and score alongside enrollment info

**Checkpoint**: Students can complete SCORM courses and view their results. Data persists across page reloads.

---

## Phase 6: User Story 4 - Resume a Course from Checkpoint (Priority: P3)

**Goal**: A student who previously started but did not complete a SCORM course can relaunch and resume from their last committed checkpoint, with suspend data restored.

**Independent Test**: Start a course, set suspend data, commit (not finish), relaunch — verify `LMSGetValue("cmi.suspend_data")` returns the saved data.

### Implementation for User Story 4

- [X] T038 [US4] Extend `ScormSessionService.LaunchAsync` to check for existing incomplete attempt — if found, set `entry="resume"`, restore `suspendData` and `sessionTime` into the new Valkey session from the last `CourseAttempt` record
- [X] T039 [US4] Extend `ScormSessionService.LaunchAsync` to auto-increment `AttemptNumber` for new attempts (query max attempt number for this student/course)
- [X] T040 [US4] Update SCORM wrapper page (`src/Host/Pages/Scorm/Launch.cshtml.cs`) to pass `entry` mode (initial/resume/retake) to the session initialization and show appropriate UI message for completed courses

**Checkpoint**: Students can resume courses from their last checkpoint and track multiple attempts.

---

## Phase 7: User Story 5 - Upload a SCORM Package (Priority: P3)

**Goal**: An admin can upload a SCORM 1.2 ZIP package, which is validated, extracted, and made available as a launchable course in the catalog.

**Independent Test**: Upload a valid SCORM ZIP — verify course appears in catalog and is launchable. Upload invalid ZIP — verify rejection with error message.

### Implementation for User Story 5

- [X] T041 [US5] Extend `ScormPackageService` with `UploadAsync(file, title)` — validates ZIP contains `imsmanifest.xml`, parses manifest via `ManifestParser`, extracts ZIP to `wwwroot/scorm-content/{packageId}/`, creates `ScormPackage` entity, creates/links `Course` in Catalog via DbContext
- [X] T042 [US5] Implement `POST /api/scorm/upload` endpoint in `src/Modules/Scorm/Endpoints/ScormEndpoints.cs` (requires admin authorization, accepts multipart form-data with `package` field, returns package details)
- [X] T043 [US5] Create admin upload Razor Page at `src/Host/Pages/Admin/Upload.cshtml` and `Upload.cshtml.cs` (file picker for ZIP, upload button, error/success messages)
- [X] T044 [US5] Add admin authorization check — update Program.cs or endpoint to require `[Authorize(Roles = "Admin")]` for upload endpoint

**Checkpoint**: Admins can upload SCORM packages that become available in the catalog.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories.

- [X] T045 [P] Add `dotnet ef migrations add` migration files for `ScormDbContext` (ScormPackages + CourseAttempts tables)
- [X] T046 Update `tests/ArchitectureTests/ModuleBoundaryTests.cs` to verify Scorm module boundaries (Scorm → Catalog.Contracts, Scorm → Enrollment.Contracts only; no direct module references)
- [X] T047 [P] Update `.gitignore` to exclude `wwwroot/scorm-content/` (runtime-extracted content)
- [X] T048 [P] Add session timeout handling — server-side check in ScormSessionStore for expired Valkey keys (TTL exceeded), returning 404 for stale session IDs
- [X] T049 [P] Update shared layout (`src/Host/Pages/Shared/_Layout.cshtml`) with admin upload link in navigation
- [X] T050 Run full `dotnet build` and `dotnet test` to verify everything compiles and tests pass
- [X] T051 Validate against `quickstart.md` scenarios (all 8 validation checks)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3-7)**: All depend on Foundational phase completion
  - US1 and US2 are P1 and should be completed before US3 and US4
  - US3 depends on US2 (session state must exist before committing)
  - US4 depends on US3 (commit data must exist before resuming)
  - US5 is independent of US1-US4 (upload doesn't require session tracking)
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories
- **US2 (P1)**: Can start after Foundational (Phase 2) — builds on US1's session initialization
- **US3 (P2)**: Depends on US2 completion (session data must exist before committing)
- **US4 (P3)**: Depends on US3 completion (commit data must exist before resuming)
- **US5 (P3)**: Can start after Foundational — independent of US1-US4 (upload is a separate flow)

### Within Each User Story

- Models before services
- Services before endpoints
- API endpoints before Razor Pages
- Core implementation before UI polish

### Parallel Opportunities

- T001, T002, T003, T004: All setup tasks are independent
- T009, T010: ScormPackage and CourseAttempt entities can be created in parallel
- T012, T013: ScormSessionStore and ManifestParser are independent
- T027, T028: setValue and getValue endpoints are independent
- T033, T034: commit and finish endpoints are independent
- US5 can proceed in parallel with US1-US4 after Foundational

---

## Parallel Example: User Story 1

```
# Launch in parallel (different files):
T009: Create ScormPackage entity in src/Modules/Scorm/Domain/ScormPackage.cs
T010: Create CourseAttempt entity in src/Modules/Scorm/Domain/CourseAttempt.cs

# After both complete:
T011: Create ScormDbContext (depends on both entities)

# After Foundational phase:
T017: Implement ScormPackageService (depends on ScormDbContext)
T018: Implement ScormSessionService (depends on ScormDbContext, ScormSessionStore)

# After services:
T019: Implement POST /api/scorm/{courseId}/launch endpoint
T020: Create SCORM wrapper Razor Page
```

---

## Implementation Strategy

### MVP First (US1 + US2 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US1 — Launch a SCORM Course
4. Complete Phase 4: US2 — Track Course Progress
5. **STOP and VALIDATE**: Run quickstart.md scenarios 1-4
6. Demo: Student can launch a SCORM course and the system tracks progress

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Launch courses → Validate
3. Add US2 → Track progress → Validate (MVP!)
4. Add US3 → Commit & view results → Validate
5. Add US4 → Resume from checkpoint → Validate
6. Add US5 → Upload packages → Validate
7. Polish → Migrations, architecture tests, cleanup
8. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies on other incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Architecture tests must pass at every checkpoint (Constitution Principle III)
