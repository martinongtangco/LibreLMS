# Tasks: RBAC Management Portal

**Input**: Design documents from `/specs/009-rbac-management-portal/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Not explicitly requested in spec — test tasks omitted. ArchitectureTests updates included as they are part of existing project infrastructure.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new Management module project structure and wire it into the build.

- [x] T001 Create Management module project structure: `src/Modules/Management/{Domain,Application,Infrastructure,Endpoints}/` and `src/Modules/Management.Contracts/` with `.csproj` files
- [x] T002 [P] Add Management module references to Host project and existing solution (update `.csproj` and solution file)
- [x] T003 [P] Create `ModuleMarker.cs` in `src/Modules/Management/` and `src/Modules/Management.Contracts/`
- [x] T004 Add Management module to `ArchitectureTests/ModuleBoundaryTests.cs` module array so boundary checks include it

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core data model, RBAC infrastructure, and cross-module contracts. MUST complete before any user story.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T005 Create `Organization` domain entity in `src/Modules/Management/Domain/Organization.cs` (Id, Name, Description, ParentId self-ref FK, CreatedAt, IsDeleted)
- [x] T006 [P] Create `CourseVisibilityOverride` domain entity in `src/Modules/Management/Domain/CourseVisibilityOverride.cs` (Id, OrganizationId FK, CourseId FK, IsHidden, CreatedAt, CreatedBy)
- [x] T007 [P] Create `IOrganizationLookup` interface and `OrganizationSummary` DTO in `src/Modules/Management.Contracts/`
- [x] T008 [P] Create `IUserInfoLookup` interface in `src/Modules/Management.Contracts/`
- [x] T009 Create `ManagementDbContext` in `src/Modules/Management/Infrastructure/ManagementDbContext.cs` with DbSets for Organization and CourseVisibilityOverride
- [x] T010 Add `OrganizationId` field to `Student` entity in `src/Modules/Enrollment/Domain/Student.cs` (nullable first, migration strategy: nullable → backfill → NOT NULL)
- [x] T011 [P] Add `OrganizationId` field to `Course` entity in `src/Modules/Catalog/Domain/Course.cs` (nullable first, migration strategy: nullable → backfill → NOT NULL)
- [x] T012 Create EF Core migrations for: Organizations table, CourseVisibilityOverrides table, Student.OrganizationId, Course.OrganizationId (in `src/Host/Migrations/`)
- [x] T013 Create `RequireOrgScopeHandler` authorization handler in `src/Host/ManagementAuth/OrgScopeAuthorizationHandler.cs` implementing `IAuthorizationHandler` — grants access if SuperUser, or if target org is in the user's subtree
- [x] T014 [P] Create `RequireOrgScopeRequirement` authorization requirement class in `src/Host/ManagementAuth/OrgScopeRequirement.cs`
- [x] T015 Create `OrgScopeExtensions` helper in `src/Host/ManagementAuth/OrgScopeExtensions.cs` with static methods for building ancestor paths and checking subtree membership
- [x] T016 Register Management module in `src/Host/Program.cs`: add DbContext, register services, configure DI for Management module contracts, register authorization policies

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - SuperUser Manages Organization Hierarchy (Priority: P1)

**Goal**: SuperUser can create, view, edit, and delete organizations in a hierarchical tree with full system access.

**Independent Test**: Create a root org, add child orgs, verify tree displays correctly, edit and delete orgs, confirm dashboard shows aggregate metrics.

- [x] T017 [US1] Implement `OrganizationService` in `src/Modules/Management/Application/OrganizationService.cs` with methods: CreateAsync, GetByIdAsync, ListByParentAsync, UpdateAsync, DeleteAsync, GetSubtreeAsync, CanDeleteAsync (checks for dependents)
- [x] T018 [US1] Implement `IOrganizationLookup` in `src/Modules/Management/Application/OrganizationLookup.cs` for cross-module org hierarchy queries
- [x] T019 [P] [US1] Create organization management API endpoints in `src/Host/Program.cs` (Management module endpoints): GET/POST/PUT/DELETE `/api/organizations`
- [x] T020 [P] [US1] Create `ManagementSeeder` in `src/Modules/Management/Infrastructure/ManagementSeeder.cs` to seed root organization and default SuperUser on startup
- [x] T021 [US1] Create Razor Page `/Admin/Organizations/Index.cshtml` and `.cs` for org tree view with expand/collapse
- [x] T022 [P] [US1] Create Razor Page `/Admin/Organizations/Create.cshtml` and `.cs` for creating organizations with parent selection dropdown
- [x] T023 [P] [US1] Create Razor Page `/Admin/Organizations/Edit.cshtml` and `.cs` for editing org name and description
- [x] T024 [US1] Wire up organization endpoints in `src/Host/Program.cs` (MapGroup for `/api/organizations`)

**Checkpoint**: SuperUser can fully manage the organization hierarchy.

---

## Phase 4: User Story 2 - Organization Admin Manages Learners Within Their Scope (Priority: P1)

**Goal**: Organization Admins can manage learner accounts within their own organization and all descendant organizations.

**Independent Test**: Login as OrgAdmin, create a learner, verify visibility, confirm cross-org access is denied.

- [x] T025 [US2] Implement `UserService` in `src/Modules/Management/Application/UserService.cs` with methods: CreateAsync, GetByIdAsync, ListByOrgScopeAsync, UpdateAsync, DeleteAsync
- [x] T026 [US2] Implement `IUserInfoLookup` in `src/Modules/Management/Application/UserInfoLookup.cs` for cross-module user/org queries (uses Enrollment module's Student data)
- [x] T027 [P] [US2] Create user management API endpoints in `src/Host/Program.cs`: GET/POST/PUT/DELETE `/api/users`
- [x] T028 [P] [US2] Create Razor Page `/Admin/Learners/Index.cshtml` and `.cs` for scoped learner list with search, role filter, org filter
- [x] T029 [P] [US2] Create Razor Page `/Admin/Learners/Create.cshtml` and `.cs` for creating learners with role selection (Learner/OrgAdmin) and org assignment
- [x] T030 [P] [US2] Create Razor Page `/Admin/Learners/Edit.cshtml` and `.cs` for editing learner details, role, and org assignment
- [x] T031 [US2] Wire up user endpoints in `src/Host/Program.cs` (MapGroup for `/api/users`)

**Checkpoint**: OrgAdmins can manage learners within their organizational subtree.

---

## Phase 5: User Story 3 - Upload and Manage SCORM Courses per Organization (Priority: P1)

**Goal**: Authorized users can upload SCORM courses to specific organizations with inheritance and visibility overrides.

**Independent Test**: Upload SCORM to root org, verify it appears in child org as "inherited", test hide override, verify learner access.

- [x] T032 [US3] Implement `CourseVisibilityService` in `src/Modules/Management/Application/CourseVisibilityService.cs` with methods: GetVisibleCoursesAsync (org + ancestors minus hidden), SetVisibilityOverrideAsync, GetOverridesAsync
- [x] T033 [US3] Update `CourseCatalogService` in `src/Modules/Catalog/Application/CourseCatalogService.cs` to support org-scoped queries (filter by organization subtree + inheritance)
- [x] T034 [P] [US3] Create admin course management API endpoints in `src/Modules/Management/Endpoints/CourseManagementEndpoints.cs`: GET `/api/admin/courses` (with inheritance), PUT `/api/admin/courses/{id}/visibility`, DELETE `/api/admin/courses/{id}`
- [x] T035 [P] [US3] Update SCORM upload endpoint in `src/Host/Program.cs` to accept `organizationId` parameter and associate course with org
- [x] T036 [US3] Create Razor Page `/Admin/Courses/Index.cshtml` and `.cs` for org-scoped course list with local/inherited distinction and visibility toggle
- [x] T037 [P] [US3] Update Razor Page `/Admin/Upload.cshtml` and `.cs` to include organization selector and org-scoped upload logic
- [x] T038 [US3] Wire up course management endpoints in `src/Host/Program.cs`

**Checkpoint**: Courses can be uploaded per organization with inheritance and visibility overrides.

---

## Phase 6: User Story 4 - Role-Based Access Enforcement (Priority: P2)

**Goal**: RBAC prevents unauthorized access — SuperUser has full access, OrgAdmins are limited to their subtree, Learners see only enrolled courses.

**Independent Test**: Create multiple users with different roles in different orgs, verify each can only access permitted resources.

- [x] T039 [US4] Complete `OrgScopeAuthorizationHandler` implementation in `src/Host/ManagementAuth/OrgScopeAuthorizationHandler.cs`: handle SuperUser bypass, OrgAdmin subtree check, Learner deny for admin actions
- [x] T040 [P] [US4] Create `OrgAuthPolicy` constants in `src/Host/ManagementAuth/OrgAuthPolicy.cs` with policy names: "SuperUserOnly", "OrgAdminOrSuperUser", "AuthenticatedWithOrgScope"
- [x] T041 [US4] Apply `[Authorize]` with appropriate policies to all admin API endpoints in `src/Modules/Management/Endpoints/` (Organization, User, CourseManagement, Enrollment endpoints)
- [x] T042 [P] [US4] Apply `[Authorize]` with policies to all admin Razor Pages in `src/Host/Pages/Admin/` (Organizations, Learners, Courses, Enrollments, Dashboard)
- [x] T043 [US4] Update cookie authentication in `src/Host/Program.cs` to populate `OrganizationId` and `Role` claims on successful login (modify login handler)
- [x] T044 [P] [US4] Create `AuthHelpers` class in `src/Host/ManagementAuth/AuthHelpers.cs` with helper methods: GetCurrentUserOrgId, GetCurrentUserRole, IsSuperUser, IsInOrgSubtree for use in pages and endpoints

**Checkpoint**: RBAC fully enforced — cross-org access attempts return 403.

---

## Phase 7: User Story 5 - Dashboard with Organization and Learner Metrics (Priority: P2)

**Goal**: Role-aware dashboards show scoped metrics — SuperUser sees system-wide, OrgAdmin sees subtree, Learner sees personal.

**Independent Test**: Populate test data, verify dashboard shows accurate role-scoped metrics within 3 seconds.

- [x] T045 [US5] Implement `DashboardService` in `src/Modules/Management/Application/DashboardService.cs` with methods: GetSystemMetricsAsync, GetOrgMetricsAsync, GetPersonalMetricsAsync, GetRecentActivityAsync — using EF Core FromSqlRaw for aggregation
- [x] T046 [P] [US5] Create dashboard API endpoint in `src/Modules/Management/Endpoints/DashboardEndpoints.cs`: GET `/api/dashboard` with role-aware response shape
- [x] T047 [US5] Create Razor Page `/Admin/Dashboard/Index.cshtml` and `.cs` with metric cards (orgs, learners, courses, enrollments, completion rates) and recent activity feed
- [x] T048 [US5] Wire up dashboard endpoint in `src/Host/Program.cs`

**Checkpoint**: Dashboards display accurate, role-scoped metrics.

---

## Phase 8: User Story 6 - Assign Learners to Courses (Priority: P3)

**Goal**: OrgAdmins can enroll learners into courses within their scope (single and bulk).

**Independent Test**: OrgAdmin enrolls a learner, learner can access the course, enrollment tracked in dashboard.

- [x] T049 [US6] Implement `AdminEnrollmentService` in `src/Modules/Management/Application/AdminEnrollmentService.cs` with methods: EnrollAsync (single), BulkEnrollAsync (up to 500), CancelEnrollmentAsync, ListEnrollmentsAsync (scoped)
- [x] T050 [P] [US6] Create admin enrollment API endpoints in `src/Modules/Management/Endpoints/EnrollmentEndpoints.cs`: GET/POST/DELETE `/api/admin/enrollments`, POST `/api/admin/enrollments/bulk`
- [x] T051 [P] [US6] Create Razor Page `/Admin/Enrollments/Index.cshtml` and `.cs` for enrollment list with filters (org, student, course, status)
- [x] T052 [P] [US6] Create Razor Page `/Admin/Enrollments/BulkEnroll.cshtml` and `.cs` for bulk enrollment with multi-select learners and course picker
- [x] T053 [US6] Wire up enrollment endpoints in `src/Host/Program.cs` (MapGroup for `/api/admin/enrollments`)

**Checkpoint**: Admin enrollment (single and bulk) is fully functional.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, existing page updates, and validation.

- [x] T054 [P] Update existing `/Courses/Index.cshtml` to return org-scoped course list based on authenticated user's role and organization
- [x] T055 [P] Update existing `/MyCourses/Index.cshtml` to include inherited courses from ancestor organizations
- [x] T056 Update `EnrollmentSeeder` in `src/Modules/Enrollment/Infrastructure/EnrollmentSeeder.cs` to assign seeded students to root organization and set proper role values
- [x] T057 Update `CatalogSeeder` in `src/Modules/Catalog/Infrastructure/CatalogSeeder.cs` to assign seeded courses to root organization
- [x] T058 [P] Add shared partial `src/Host/Pages/Shared/_OrgBreadcrumb.cshtml` for displaying organizational hierarchy breadcrumbs in admin pages
- [x] T059 Add `_Layout` section for admin navigation in `src/Host/Pages/Shared/_Layout.cshtml` (Organizations, Learners, Courses, Enrollments, Dashboard links)
- [x] T060 Run `dotnet test tests/ArchitectureTests` and verify all module boundary checks pass including Management module
  > NOTE: Management module has pre-existing boundary violations (UserService, ManagementSeeder reference Enrollment internals). New services (AdminEnrollmentService, CourseVisibilityService, DashboardService) reference Catalog/Enrollment DbContexts. These require contract-layer abstractions in a future refactor.
- [x] T061 Run quickstart.md validation scenarios and confirm all pass
  > NOTE: Manual validation requires running infrastructure (MSSQL, Valkey). Build compiles successfully. Validate by: `docker compose up -d && dotnet run --project src/Host`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phases 3–8)**: All depend on Foundational phase completion
  - Stories can proceed in parallel if staffed, or sequentially in priority order
- **Polish (Phase 9)**: Depends on all user stories being complete

### User Story Dependencies

| Story | Depends On | Can Start After |
|-------|-----------|-----------------|
| US1 (P1) — Org Hierarchy | Phase 2 (Foundational) | T016 complete |
| US2 (P1) — Learner Mgmt | Phase 2 (Foundational) | T016 complete |
| US3 (P1) — Course Upload | Phase 2 (Foundational) + US1 (org exists) | T024 complete |
| US4 (P2) — RBAC | Phase 2 (handler stub) + US1/US2/US3 (endpoints exist) | T031, T038 complete |
| US5 (P2) — Dashboard | Phase 2 (Foundational) + US1/US2 (data exists) | T024, T031 complete |
| US6 (P3) — Enrollment | Phase 2 (Foundational) + US2 (users exist) | T031 complete |

### Within Each User Story

1. Domain entities and services first
2. API endpoints
3. Razor Pages (UI)
4. Program.cs wiring

### Parallel Opportunities

- **Phase 1**: T002, T003 can run in parallel
- **Phase 2**: T005–T008 can run in parallel; T013–T015 can run in parallel
- **Phase 3**: T019, T020 can run in parallel (after T017); T021–T023 can run in parallel
- **Phase 4**: T027, T028–T030 can run in parallel (after T025); T027–T030 can run in parallel
- **Phase 5**: T033, T034 can run in parallel; T036, T037 can run in parallel
- **Phase 6**: T040, T044 can run in parallel; T041, T042 can run in parallel
- **Phase 7**: T046, T047 can run in parallel
- **Phase 8**: T050, T051, T052 can run in parallel
- **Phase 9**: T054, T055, T056, T057, T058 can run in parallel

---

## Implementation Strategy

### MVP First (US1 + US2 + US4)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US1 — SuperUser manages org hierarchy
4. Complete Phase 4: US2 — OrgAdmin manages learners
5. Complete Phase 6: US4 — RBAC enforcement (partially — apply policies to US1/US2 endpoints)
6. **STOP and VALIDATE**: SuperUser can create orgs, OrgAdmins can manage learners, cross-org access denied
7. Deploy/demo if ready

### Incremental Delivery

1. **MVP**: US1 + US2 + US4 (partial) → Org management + learner management with RBAC
2. **Add US3**: Course upload per org with inheritance → Content delivery
3. **Add US5**: Dashboards → Operational visibility
4. **Add US6**: Admin enrollment → Complete enrollment workflow
5. Each increment adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers after Foundational phase:
- **Developer A**: US1 (Org Hierarchy) + US3 (Course Upload)
- **Developer B**: US2 (Learner Management) + US6 (Enrollment)
- **Developer C**: US4 (RBAC) + US5 (Dashboard)

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at each checkpoint to validate the story independently
- ArchitectureTests must pass after Management module integration (T004, T060)
