# Tasks: Organization Scope Enforcement

**Input**: Design documents from `/specs/052-enforce-org-scope/` (ADR 0010 is
the governing design — read it first).

**Prerequisites**: plan.md, spec.md, research.md, quickstart.md

**Organization**: foundation first (US1), then one surface cluster per story
(US2–US4), then the E2E contract (US5) and the regression sweep (US6). The
E2E red-verify runs BEFORE any fix lands; unit red-verify runs after the
`OrgScope` params exist but before the checks.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to

## Phase 1: Setup

- [X] T001 Create branch `story/052-enforce-org-scope` from `master` (Constitution VIII)
- [X] T002 [US5] RED-VERIFY (E2E, pre-fix): write the `08-rbac.spec.ts` subtree block (setup: SuperUser creates child org + child OrgAdmin + learner via the API; asserts AC 1–4; teardown deletes them) and run it against the current build — the cross-org assertions must FAIL (out-of-subtree reads 200 / foreign rows in lists). Paste evidence. (Spec file is added on the branch; the fix comes later — the file may fail to build the app? No: it's TS, app untouched.)

## Phase 2: Foundation (US1 — users surface)

**Goal**: `OrgScope` + subtree helper exist; the users surface (API +
Learners page + learner SP) is scoped.

**Independent Test**: Management.Tests user-scope tests green (subtree list,
out-of-scope 403s, SuperUser all); `GET /api/users` as a child OrgAdmin
returns only the subtree.

- [X] T003 [US1] `src/Modules/Management.Contracts/OrgScope.cs`: the scope record (SuperUser / ForOrgAdmin, fail-closed constructor) + `AuthHelpers.GetScope(ClaimsPrincipal)` in the Host (named `GetScope`, not `FromClaims`)
- [X] T004 [P] [US1] `src/Modules/Management/Application/OrgSubtree.cs`: shared subtree helper (`GetSubtreeOrgIdsAsync` BFS + `IsOrgInScopeAsync` ancestor walk, both over `IOrganizationLookup` — which gained `GetChildOrgIdsAsync`). DashboardService's private BFS was pointed at it in T022 (behavior unchanged)
- [X] T005 [US1] New `tests/Management.Tests` project + added to `LibreLms.slnx`; user-scope tests: (a) OrgAdmin sees only subtree users in ListAll; (b) out-of-subtree GetById/Update/Delete → forbidden; (c) Create with out-of-subtree org → forbidden; (d) SuperUser system-wide. **Deviation**: hand-rolled fakes (house style — same as Scorm.Tests/Enrollment.Tests fakes) instead of real MSSQL marker rows; the SP-level subtree filter is covered by Enrollment.Tests + E2E instead
- [X] T006 [US1] `UserService`: add the required `OrgScope` param to ListAllAsync, ListAllPagedAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync, ListByOrgScopeAsync (+ `IUserProvisioning.ListPagedAsync` gains `Guid? rootOrgId`)
- [X] T007 [US1] RED-VERIFY (unit): run T005's tests against the pre-fix service → compile-level red, all scoped call sites fail (`No overload for method 'GetByIdAsync' takes 2 arguments` etc.)
- [X] T008 [US1] Implement the checks in `UserService` (list filter via subtree; single-target checks via `OrgSubtree.IsOrgInScopeAsync`; `@RootOrgId` derived from the scope on the paged call) — unit tests green
- [X] T009 [US1] Migration: re-create `AdminListLearners` with `@RootOrgId UNIQUEIDENTIFIER = NULL` (+ `#Subtree` temp table, always created so the predicate compiles in both modes, recursive CTE excluding `IsDeleted`; row + count SELECTs filtered) — 9 columns kept; `AdminListLearnersTests` corrected to assert 9 (settles the documented pre-existing failure — Enrollment.Tests now 42/42); `UserProvisioningService.ListPagedAsync` passes it
- [X] T010 [US1] Host: `GET/POST/PUT/DELETE /api/users*` endpoints pass `AuthHelpers.GetScope` + map `ForbiddenAccessException` → JSON 403; `Pages/Admin/Learners/{Index,Create,Edit}.cshtml.cs` pass the scope; org dropdown = subtree for OrgAdmin (all for SuperUser)

## Phase 3: US2 — organizations surface (P1)

**Goal**: OrgAdmin sees/modifies only their subtree of orgs.

- [X] T011 [P] [US2] Management.Tests org-scope tests (`OrganizationScopeTests`, real `OrganizationLookup` over InMemory `ManagementDbContext`): ListAll subtree, out-of-subtree GetById/GetByIdWithStatus/GetSubtree/Update/Delete/CanDelete/Disable/Enable → forbidden, Create under foreign parent / root-level by OrgAdmin → forbidden, chart pinned to own subtree, SuperUser all
- [X] T012 [US2] `OrganizationService`: `OrgScope` param on CreateAsync, GetByIdAsync, ListByParentAsync, GetSubtreeAsync, UpdateAsync, DeleteAsync, CanDeleteAsync, ListAllAsync, GetChartTreeAsync (OrgAdmin pinned to own subtree — a passed root can never widen it), DisableAsync, EnableAsync, GetByIdWithStatusAsync; `IOrganizationLookup` injected; all checks via `OrgSubtree`. Red-verify: covered by the T007 stash-red (service compiled against the new contract) + E2E red; unit suite green after checks
- [X] T013 [US2] Host: `/api/organizations*` endpoints pass scope + 403 mapping; `Pages/Admin/Organizations/{Index,Create,Edit,Chart}.cshtml.cs` pass scope; dropdowns/tree root = subtree for OrgAdmin

## Phase 4: US3 — course visibility + delete surface (P1)

**Goal**: course-visibility writes and course deletes are subtree-checked.

- [X] T014 [P] [US3] Management.Tests course-scope tests (`CourseVisibilityScopeTests`): GetAllCourses subtree filter, SetVisibilityOverride/GetVisibleCourses/GetOverrides with out-of-subtree org → forbidden, DeleteCourse of out-of-subtree course → forbidden, SuperUser all
- [X] T015 [US3] `CourseVisibilityService`: `OrgScope` param on GetVisibleCoursesAsync, SetVisibilityOverrideAsync, GetOverridesAsync, GetAllCoursesAsync, DeleteCourseAsync — all checks via `OrgSubtree`; unit suite green
- [X] T016 [US3] Host: `/api/admin/courses*` endpoints pass scope + 403 mapping; `Pages/Admin/Courses/Index.cshtml.cs` (delete) passes scope; `Pages/Admin/Dashboard/Index.cshtml.cs` (course listing) passes scope. **Note**: Admin/Courses/{Edit,Create} pages use the Catalog service directly (course content, not the Management visibility surface) — no Management scope-sensitive call to plumb

## Phase 5: US4 — enrollments surface (P2)

**Goal**: enrollment reads/writes are scoped by the enrolled student's org.

- [X] T017 [P] [US4] Management.Tests enrollment-scope tests (`EnrollmentScopeTests`): list subtree filter, paged call passes the subtree root to the SP / SuperUser passes null, EnrollAsync out-of-subtree → forbidden (no side effect), BulkEnrollAsync out-of-subtree students skipped with message, CancelEnrollmentAsync out-of-subtree → forbidden, SuperUser all
- [X] T018 [US4] `AdminEnrollmentService`: `OrgScope` param on ListAllEnrollmentsAsync, ListAllEnrollmentsPagedAsync, EnrollAsync, BulkEnrollAsync, CancelEnrollmentAsync (+ `IEnrollmentAdmin` gains `GetEnrollmentStudentIdAsync`; `ListPagedAsync` gains `Guid? rootOrgId`); fail-closed `StudentOrgInScopeAsync` helper; unit suite green
- [X] T019 [US4] Migration: re-create `AdminListEnrollments` with `@RootOrgId` (same `#Subtree` pattern, filter on the student's `s.OrganizationId`); `EnrollmentAdminService.ListPagedAsync` passes it
- [X] T020 [US4] Host: `/api/admin/enrollments*` endpoints pass scope + 403 mapping; `Pages/Admin/Enrollments/{Index,BulkEnroll}.cshtml.cs` pass scope (student dropdown subtree-filtered)

## Phase 6: US5 — E2E contract (P2)

**Goal**: the cross-org denial is pinned over real HTTP.

- [X] T021 [US5] Run the T002 08-rbac block against the FIXED build → all 5 subtree tests PASS; full Playwright suite 177 passed + 1 documented skip (setup/teardown clean; no seed-data change)

## Phase 7: US6 — regression sweep (P3)

**Goal**: SuperUser/Learner behavior unchanged; nothing else broke.

- [X] T022 [P] [US6] `DashboardService`: private BFS removed, `GetOrgMetricsAsync` now uses the shared `OrgSubtree` (identical set: org + live descendants; `DashboardServiceBulkCountsTests` still 9/9 green with the real `OrganizationLookup`). **Adjacent finding (future-spec candidate, not fixed)**: `GET /api/dashboard/activity` (`GetRecentActivityAsync`) is system-wide for OrgAdmins too — the dashboard activity feed is not one of the four 052 surfaces. **Note**: `Pages/Admin/Upload` calls the Scorm module's package service, not a Management course-create — no Management scope plumbing applicable
- [X] T023 [P] [US6] `Pages/Courses/Index.cshtml.cs` (learner-facing): it calls `GetVisibleCoursesAsync(ownOrg)` (claim-derived org) — now passes `OrgScope.ForOrgAdmin(ownOrg)` with a comment (the check always passes; no behavior change)
- [X] T024 Gate 1: `dotnet build LibreLms.slnx` (0 errors) + app restart in the devcontainer — paste evidence
- [X] T025 Gate 2: ArchitectureTests, full `dotnet test LibreLms.slnx` (AdminListLearnersTests should now PASS — 9 columns), filler-clean AFTER the last unit run, full Playwright serial — paste evidence
- [X] T026 Independent verification (Constitution XVI): fresh subagent re-runs build + Playwright from a clean worktree checkout of `story/052-enforce-org-scope` (its instructions MUST include the filler-clean step) and reports independently; merge to master only after GREEN (`git merge --no-ff`)
- [ ] T027 Gate 3 (post-merge, on master): rebuild, restart, re-run gate 2 — paste evidence; mark all tasks `[X]`, set spec Status to Complete, commit F

## Verification Notes

- **T002 (E2E red, pre-fix)**: `08-rbac.spec.ts` subtree block vs. pre-fix build →
  `1 failed, 4 did not run (serial abort), 15 passed`. The users test failed on the
  cross-org assertion: `root-org learner must NOT be visible to a child OrgAdmin —
  Expected value: not "alice@example.com"; Received array: [... ~350 users incl.
  alice@example.com, scope-admin-<ts>@example.com, scope-learner-<ts>@example.com]`.
  First attempt also exposed a setup bug (org soft-delete keeps the (Name, ParentId)
  unique row → duplicate key on retry) — fixed with per-run timestamped
  names/emails + prefix-based stale-user cleanup.
- **T007 (unit red)**: with `UserService.cs` stashed to the pre-fix version, the new
  suite fails to compile — `error CS1501: No overload for method 'GetByIdAsync' takes 2
  arguments` / `CreateAsync takes 6 arguments` / `UpdateAsync takes 5 arguments` (all
  scoped call sites). Against the fixed service: `Passed! Failed: 0, Passed: 16`.
- **T012/T015/T018 (unit green after checks)**: full `tests/Management.Tests` run
  `Passed! Failed: 0, Passed: 55` (16 users + 18 orgs + 10 visibility + 11 enrollments).
  E2E red (T002) pins the pre-fix behavior for all four surfaces over real HTTP.
- **Migration discovery gotcha (T009/T019)**: hand-written SP migrations need the
  `.Designer.cs` partial carrying the `[Migration("...")]` + `[DbContext(...)]`
  attributes — without it EF silently skips the migration (the class exists in the
  assembly but `dotnet ef migrations list` omits it and `Migrate()` never applies it).
  Both new migrations include Designer copies (model unchanged → identical
  BuildTargetModel).
- **T024 (Gate 1)**: `dotnet build LibreLms.slnx` → `Build succeeded. 0 Error(s)`;
  in-container app restart → `Now listening on: http://localhost:5000`, HTTP 302
  probe; both SPs confirmed in MSSQL with the 5th `@RootOrgId uniqueidentifier` param.
- **T025 (Gate 2)**: full `dotnet test LibreLms.slnx` (with `ConnectionStrings__Sql`
  env) → Management 55, Arch 14, Host 9, Catalog 32, Enrollment 42, Scorm 18 = **170/170**
  (Enrollment 42/42 includes the settled `never_exposes_credential_columns` — 9 cols).
  One transient Catalog flake observed on two of three parallel slnx runs (green in
  isolation and on re-run — pre-existing shared-DB sensitivity of the perf seed, same
  as the pre-052 baseline). Filler-clean after the last unit run (11,668 filler rows →
  10 seeded courses), then full Playwright serial: **177 passed + 1 documented skip**
  (verify-email), incl. the 5 new `08-rbac` subtree tests.
- **T026 (independent verification)**: fresh no-context subagent, detached clean
  worktree at 1755c77: build 0 errors; units 170/170 (the single Catalog
  parallel-flake re-ran 32/32 in isolation); in-container app on the branch commit
  (302 probe); filler-clean to 10 courses; Playwright **177 passed + 1 skip, 0
  failed**. Verdict GREEN → merge authorized.

## Verification Notes

(filled during T002, T007, T012, T015, T018, T024, T025, T026, T027)
