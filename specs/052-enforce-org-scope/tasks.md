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

- [ ] T001 Create branch `story/052-enforce-org-scope` from `master` (Constitution VIII)
- [ ] T002 [US5] RED-VERIFY (E2E, pre-fix): write the `08-rbac.spec.ts` subtree block (setup: SuperUser creates child org + child OrgAdmin + learner via the API; asserts AC 1–4; teardown deletes them) and run it against the current build — the cross-org assertions must FAIL (out-of-subtree reads 200 / foreign rows in lists). Paste evidence. (Spec file is added on the branch; the fix comes later — the file may fail to build the app? No: it's TS, app untouched.)

## Phase 2: Foundation (US1 — users surface)

**Goal**: `OrgScope` + subtree helper exist; the users surface (API +
Learners page + learner SP) is scoped.

**Independent Test**: Management.Tests user-scope tests green (subtree list,
out-of-scope 403s, SuperUser all); `GET /api/users` as a child OrgAdmin
returns only the subtree.

- [ ] T003 [US1] `src/Modules/Management.Contracts/OrgScope.cs`: the scope record (SuperUser / ForOrgAdmin, fail-closed constructor) + `AuthHelpers.FromClaims(ClaimsPrincipal)` in the Host
- [ ] T004 [P] [US1] `src/Modules/Management/Application/OrgSubtree.cs`: shared BFS descendant helper (move out of DashboardService, which now calls it — behavior unchanged)
- [ ] T005 [US1] New `tests/Management.Tests` project (house pattern: real MSSQL via `ConnectionStrings__Sql`, random-GUID marker rows cleaned up) + add to `LibreLms.slnx`; user-scope tests: (a) OrgAdmin sees only subtree users in ListAll/Paged; (b) out-of-subtree GetById/Update/Delete → forbidden; (c) Create with out-of-subtree org → forbidden; (d) SuperUser system-wide
- [ ] T006 [US1] `UserService`: add the required `OrgScope` param to ListAllAsync, ListAllPagedAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync (+ `IUserProvisioning.ListPagedAsync` gains `Guid? rootOrgId`); NO checks yet — params plumbed, ignored. (Red-verify state for the unit tests.)
- [ ] T007 [US1] RED-VERIFY (unit): run T005's tests → the out-of-scope tests FAIL (params ignored). Paste evidence.
- [ ] T008 [US1] Implement the checks in `UserService` (list filter via subtree; single-target checks; `@RootOrgId` on the paged call) — unit tests green
- [ ] T009 [US1] Migration: re-create `AdminListLearners` with `@RootOrgId UNIQUEIDENTIFIER = NULL` (+ `#Subtree` temp table, recursive CTE, `IsDeleted` excluded; row + count SELECTs filtered) — keep the 9 columns; correct `AdminListLearnersTests` to assert 9 (settles the documented pre-existing failure); update `UserProvisioningService.ListPagedAsync` to pass it
- [ ] T010 [US1] Host: `GET/POST/PUT/DELETE /api/users*` endpoints pass `OrgScope.FromClaims` + map forbidden → JSON 403; `Pages/Admin/Learners/{Index,Create,Edit}.cshtml.cs` pass the scope; org dropdown = subtree for OrgAdmin (all for SuperUser)

## Phase 3: US2 — organizations surface (P1)

**Goal**: OrgAdmin sees/modifies only their subtree of orgs.

- [ ] T011 [P] [US2] Management.Tests org-scope tests: subtree list (ListAll/ListByParent/picker/chart), out-of-subtree GetById/Update/Delete/Disable/Enable → forbidden, Create with out-of-subtree parentId (or root-level by OrgAdmin) → forbidden, SuperUser all
- [ ] T012 [US2] `OrganizationService`: `OrgScope` param on CreateAsync, GetByIdAsync, ListByParentAsync, GetSubtreeAsync, UpdateAsync, DeleteAsync, CanDeleteAsync, ListAllAsync, GetChartTreeAsync, DisableAsync, EnableAsync, GetByIdWithStatusAsync — params first (red), then checks (green) — red-verify evidence in notes
- [ ] T013 [US2] Host: `/api/organizations*` endpoints pass scope + 403 mapping; `Pages/Admin/Organizations/{Index,Create,Edit,Chart}.cshtml.cs` pass scope; dropdowns/tree root = subtree for OrgAdmin

## Phase 4: US3 — course visibility + delete surface (P1)

**Goal**: course-visibility writes and course deletes are subtree-checked.

- [ ] T014 [P] [US3] Management.Tests course-scope tests: GetAllCourses subtree filter, SetVisibilityOverride with out-of-subtree org → forbidden, GetVisibleCourses/GetOverrides org-checked, DeleteCourse of out-of-subtree course → forbidden, SuperUser all
- [ ] T015 [US3] `CourseVisibilityService`: `OrgScope` param on GetVisibleCoursesAsync, SetVisibilityOverrideAsync, GetOverridesAsync, GetAllCoursesAsync, DeleteCourseAsync — params first (red), checks second (green) — red-verify evidence in notes
- [ ] T016 [US3] Host: `/api/admin/courses*` endpoints pass scope + 403 mapping; `Pages/Admin/Courses/{Index,Edit,Create}.cshtml.cs` pass scope (course org dropdown = subtree for OrgAdmin)

## Phase 5: US4 — enrollments surface (P2)

**Goal**: enrollment reads/writes are scoped by the enrolled student's org.

- [ ] T017 [P] [US4] Management.Tests enrollment-scope tests: list/paged subtree filter, EnrollAsync/BulkEnrollAsync with out-of-subtree student → clean rejection (403-shaped), CancelEnrollmentAsync out-of-subtree → forbidden, SuperUser all
- [ ] T018 [US4] `AdminEnrollmentService`: `OrgScope` param on ListAllEnrollmentsAsync, ListAllEnrollmentsPagedAsync, EnrollAsync, BulkEnrollAsync, CancelEnrollmentAsync (+ `IEnrollmentAdmin.ListAsync`/`ListPagedAsync`/`CountEnrollmentsByOrgsAsync` callers as needed) — params first (red), checks second (green) — red-verify evidence in notes
- [ ] T019 [US4] Migration: re-create `AdminListEnrollments` with `@RootOrgId` (same `#Subtree` pattern, filter on the student's `s.OrganizationId`); update `EnrollmentAdminService.ListPagedAsync`
- [ ] T020 [US4] Host: `/api/admin/enrollments*` endpoints pass scope + 403 mapping; `Pages/Admin/Enrollments/{Index,BulkEnroll}.cshtml.cs` pass scope

## Phase 6: US5 — E2E contract (P2)

**Goal**: the cross-org denial is pinned over real HTTP.

- [ ] T021 [US5] Run the T002 08-rbac block against the FIXED build → all subtree assertions PASS (setup/teardown clean; no seed-data change)

## Phase 7: US6 — regression sweep (P3)

**Goal**: SuperUser/Learner behavior unchanged; nothing else broke.

- [ ] T022 [P] [US6] `DashboardService`: point at the shared `OrgSubtree`; verify `GetOrgMetricsAsync`/`GetRecentActivityAsync` stay subtree-correct (unit-verified); `Pages/Admin/Dashboard` + `Upload` pages get the scope plumbing (upload = course create → org check)
- [ ] T023 [P] [US6] `Pages/Courses/Index.cshtml.cs` (learner-facing): verify it uses only public-catalog paths (no scope-sensitive admin service); if it touches one, plumb a SuperUser-shaped scope with a comment
- [ ] T024 Gate 1: `dotnet build LibreLms.slnx` (0 errors) + app restart in the devcontainer — paste evidence
- [ ] T025 Gate 2: ArchitectureTests, full `dotnet test LibreLms.slnx` (AdminListLearnersTests should now PASS — 9 columns), filler-clean AFTER the last unit run, full Playwright serial — paste evidence
- [ ] T026 Independent verification (Constitution XVI): fresh subagent re-runs build + Playwright from a clean worktree checkout of `story/052-enforce-org-scope` (its instructions MUST include the filler-clean step) and reports independently; merge to master only after GREEN (`git merge --no-ff`)
- [ ] T027 Gate 3 (post-merge, on master): rebuild, restart, re-run gate 2 — paste evidence; mark all tasks `[X]`, set spec Status to Complete, commit F

## Verification Notes

(filled during T002, T007, T012, T015, T018, T024, T025, T026, T027)
