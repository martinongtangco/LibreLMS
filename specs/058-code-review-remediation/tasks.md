# Tasks: Code Review Remediation (2026-10-05 pass) — spec 058

**Branch**: `story/058-code-review-remediation` | **Plan**: [plan.md](plan.md)

**Prerequisites**: spec.md, plan.md, research.md, data-model.md, quickstart.md

**Organization**: four milestones in spec priority order (P1→P4), each an independently
committable increment with its own gate sequence (XIII/XVII) and run-log entry. Task IDs are
global and sequential. `[P]` = parallelizable (Principle XI — dispatch as parallel subagent
runs; parent session remains sole writer).

## Format: `[ID] [P?] [US?] Description with file path`

## Phase 1: Setup

- [X] T001 Author spec/plan/research/tasks on `master` (Principle IX) — spec 3b421c3
      (fast-forwarded onto master per HANDOFF deviation), plan 02db451, tasks (this commit)
- [ ] T002 Stand up local environment, host-side mode declared (CLAUDE.md §5 open conflict —
      stated in every gate report, not adjudicated): Docker Desktop up, `docker compose up -d`
      from the main repo root (mssql :1433, valkey :6380), export
      `ConnectionStrings__Sql` / `ConnectionStrings__Valkey=localhost:6380` /
      `ASPNETCORE_ENVIRONMENT=Development` in the test/app shell
- [ ] T003 Mandatory `verify-test-baseline` (before_implement hook): run the sequential unit
      runner to completion, record per-project pass/fail/skip + cause of every known-red, and
      the repo's actual commit convention from `git log` (output as a "Verified Baseline" block
      into the run log's Item 10 notes)

## Phase 2: Milestone M1 — P1/US1: dashboard stats from real SCORM attempt data

**Independent test (spec US1)**: seed a learner with a completed, scored SCORM attempt; load
System, Org, and Personal dashboards; confirm the completion rate and score reflect that attempt
instead of 0. (Covered by T008's E2E + T006's unit red→green.)

- [ ] T004 [US1] Add `src/Modules/Scorm.Contracts/IScormAttemptStats.cs`:
      `IScormAttemptStats` (`GetSystemStatsAsync`, `GetStatsForStudentsAsync(IEnumerable<Guid>)`
      — empty input → zero summary, no DB hit) + `AttemptStatsSummary` record
      (`TotalAttempts`, `CompletedAttempts`, `ScoredCompletedAttempts`, `CompletedScoreSum`,
      `DistinctCompletedCourses`) per data-model.md; terminal completion set T =
      {completed, passed, failed} case-insensitive (research R1)
- [ ] T005 [US1] Add `src/Modules/Scorm/Application/ScormAttemptStatsService.cs` (one aggregate
      EF query per call, `AsNoTracking()`, case-insensitive status compare) + register
      `AddScoped<IScormAttemptStats, ScormAttemptStatsService>()` in
      `src/Modules/Scorm/Endpoints/ScormModuleExtensions.cs`
- [ ] T006 [US1] Add `tests/Management.Tests/DashboardServiceTests.cs` (EF InMemory
      `ManagementDbContext` + fakes: `FakeScormAttemptStats`, `FakeProvisioning`,
      `FakeUserLookup`, `FakeEnrollmentAdmin`, `FakeCourseLookup`, `FakeOrgLookup`; unique
      DB name per class): org-metrics subtree aggregate with hand-computed expectation (US2-1
      shape, e.g. 2-of-5 completed → 0.4), system metrics incl. zero-attempts → 0.0, personal
      (one completed scored attempt → count 1 + exact score; no attempts → 0/0; attempts-none-
      completed → 0/0, no divide-by-zero). **Run against the still-hardcoded service first and
      record the red output (expected 0.4 / actual 0.0)** — US2's "fails on old behavior" proof
- [ ] T007 [US1] Wire `src/Modules/Management/Application/DashboardService.cs`: ctor gains
      `IScormAttemptStats` + `IUserProvisioning`; `GetSystemMetricsAsync`/`GetOrgMetricsAsync`
      compute `AverageCompletionRate = completed/total (0.0 if none)`; `GetPersonalMetricsAsync`
      computes `CompletedCourseCount = DistinctCompletedCourses` and
      `AverageScore = CompletedScoreSum/ScoredCompletedAttempts (0.0 if none)`; org view resolves
      subtree student ids via `IUserProvisioning.ListByOrgAsync(orgId)` per subtree org (union).
      DTO shapes unchanged. Re-run T006 → green
- [ ] T008 [US1] Add `tests/Playwright.Tests/tests/21-dashboard-real-stats.spec.ts` (own data per
      run, XVII.1): unique learner `dash058-<ts>@example.com` in root org (admin API), enroll in
      seeded SCORM course `11111111-1111-1111-1111-111111111111`, complete a SCORM session
      (`lesson_status=completed`, `score.raw=87.5`); assert Learner `GET /api/dashboard`
      (`completedCourseCount >= 1`, `averageScore == 87.5`), OrgAdmin + SuperUser
      `averageCompletionRate > 0`; US1-2: fresh empty child org + its OrgAdmin →
      `averageCompletionRate == 0`; lower-bound assertions + best-effort teardown (house
      idempotency pattern); launch-retry with Valkey flush from `20-scorm-session-authz`
- [ ] T009 [US1] Gates + commits: build 0 errors → app restart (`/` → 302) → sequential unit
      runner green → Playwright (new spec + full suite) green [local, host-side, supporting] →
      `git push` → branch CI SUCCESS (record run ID) [authoritative, XVII] → commit
      `feat(058): wire dashboard completion/score metrics to real SCORM attempt data (spec 058)`
      + `docs(058): run log — M1/P1 evidence (host-side local + CI <run id>)`

## Phase 3: Milestone M2 — P2/US2: Management unit test coverage

**Independent test (spec US2)**: `dotnet test tests/Management.Tests/Management.Tests.csproj`
before and after — new tests pass (and the T006 set already proved red-on-old-behavior).

- [ ] T010 [P] [US2] Add `tests/Management.Tests/OrganizationLookupTests.cs` (EF InMemory, seeded
      org tree): found/missing/soft-deleted `GetOrganizationAsync`; `GetChildOrgIdsAsync`
      excludes deleted; `GetAncestorOrgIdsAsync` includes self, stops at root and at a deleted
      parent
- [ ] T011 [P] [US2] Add `tests/Management.Tests/UserInfoLookupTests.cs` (fake Enrollment
      `IUserLookup`): maps `UserScopeInfo` across the boundary; null pass-through
- [ ] T012 [P] [US2] Add `tests/Management.Tests/OrgSubtreeTests.cs` (fake `IOrganizationLookup`,
      3-level tree): SuperUser → null; OrgAdmin → org + all descendants; None → empty;
      `IsOrgInScopeAsync` matrix (self/descendant/sibling/ancestor/None)
- [ ] T013 [P] [US2] Add `tests/Management.Tests/TreeLayoutServiceTests.cs`: non-trivial shape
      (root + 2 children, one child with 2 children) → depths 0/1/2, Y = depth × 130
      (NodeHeight 50 + LevelGap 80), distinct X with parent centered over children, soft-deleted
      child excluded (US2-2)
- [ ] T014 [US2] Gates + commits: Management.Tests all green with count growth ≥ 4 new tests
      (SC-002) → sequential runner + Playwright green [local, host-side] → push → CI SUCCESS →
      commit `test(058): unit tests for Management lookup/subtree/layout services (spec 058)`
      + `docs(058): run log — M2/P2 evidence (host-side local + CI <run id>)`

## Phase 4: Milestone M3 — P3/US3: ADR 0014 + catch-block collapse

**Independent test (spec US3)**: ADR exists in `docs/adr/` picking the convention; the four
cited catch-blocks (Program.cs:462-543, the `users` handlers) are refactored with unchanged
status codes / response bodies (verified by T016's mapping tests + the existing Playwright/auth
suite).

- [ ] T015 [US3] Write `docs/adr/0014-management-error-handling-convention.md` (next number
      after 0013): context (two coexisting conventions; the 4-block × ~10 catch duplication;
      what `Result<T>` can/cannot carry) → decision (typed exceptions for Management Application
      expected business failures + single endpoint translation point; result records stay on the
      Scorm session/registration surfaces where endpoints need multi-field outcomes) →
      consequences (per-handler catch type sets must stay identical; `adminEnrollments` keeps
      explicit catches — its 404-with-body/409 diverge from the mapper; follow-up candidate:
      converting the other three Management services if this pattern proves itself) per research R4
- [ ] T016 [US3] Add `src/Host/ManagementErrors.cs` (`Translate(Exception)`:
      `ForbiddenAccessException` → 403 JSON `{error}`; BCL `KeyNotFoundException` → 404 no
      body; `InvalidOperationException`/`ArgumentException` → 400 JSON `{error}`; anything else
      → throw) + `tests/Host.Tests/ManagementErrorsTests.cs` pinning all four rows (status +
      body shape)
- [ ] T017 [US3] Refactor `src/Host/Program.cs`: the 4 cited `users` handlers (GET `/{id}`,
      POST `/`, PUT `/{id}`, DELETE `/{id}`) → `catch (<identical type set as today> ex)
      { return ManagementErrors.Translate(ex); }`; extend to the `orgs` (5) and `adminCourses`
      (3) handlers (same uniform shape, verified per-handler against source); `adminEnrollments`
      untouched (comment pointing at ADR 0014 where its shape diverges)
- [ ] T018 [US3] Gates + commits: build 0 errors → Host.Tests green (incl. new mapping tests) →
      `08-rbac.spec.ts` + full Playwright green (no observable change, SC-003) [local,
      host-side] → push → CI SUCCESS → commits `docs(058): ADR 0014 — Management
      error-handling convention (spec 058)` + `fix(058): collapse repeated Management catch
      blocks via ManagementErrors per ADR 0014 (spec 058)` + `docs(058): run log — M3/P3
      evidence (host-side local + CI <run id>)`

## Phase 5: Milestone M4 — P4/US4: housekeeping

**Independent test (spec US4)**: build succeeds; ArchitectureTests gains 2 passing assertions
(each manually violated once during development, then reverted); every endpoint under
`adminEnrollments`/`users`/`orgs`/`adminCourses` keeps its 401/403 behavior (existing
Playwright/auth tests).

- [ ] T019 [US4] `src/Host/Program.cs` startup: delete the duplicate
      `enrollmentCtx/scormCtx/managementCtx` `Database.Migrate()` calls (Program.cs:161-167) —
      exactly one per context (research R10: idempotent today; fresh-DB CI proves one call
      suffices)
- [ ] T020 [P] [US4] `tests/ArchitectureTests/ModuleBoundaryTests.cs`: add (a) per-module
      "Contracts assembly must not depend on its own module internals" and (b) "SharedKernel
      must not depend on any `LibreLms.Modules.*` or `LibreLms.Contracts.*` namespace"
      (table-driven over the existing `Modules` array + `LibreLms.Contracts.<M>.ModuleMarker` /
      `LibreLms.SharedKernel.Entity` assemblies); during development introduce a temporary
      violation for each (red) then revert (US4 acceptance scenario 2)
- [ ] T021 [US4] `src/Host/Program.cs`: standardize `users`, `orgs`, `adminCourses` groups onto
      the `adminEnrollments` style — group-level
      `.RequireAuthorization(new AuthorizeAttribute { Roles = "SuperUser,OrgAdmin" })` and drop
      the per-handler `[Authorize(Roles = "SuperUser,OrgAdmin")]` (identical effective policy
      today; research R5); `dashboard` group untouched
- [ ] T022 [US4] Gates + commits: build 0 errors → app restart (migrations idempotent, `/` → 302)
      → ArchitectureTests green → `08-rbac.spec.ts` + full Playwright green (no access
      regression, SC-004) [local, host-side] → push → CI SUCCESS → commits `fix(058): drop
      duplicate Database.Migrate calls; standardize admin route-group auth (spec 058)` +
      `test(058): ArchitectureTests — Contracts reverse-dependency + SharedKernel isolation
      (spec 058)` + `docs(058): run log — M4/P4 evidence (host-side local + CI <run id>)`

## Phase 6: Branch completion & gates (XIII.3, XIV–XVII)

- [ ] T023 Finalize `specs/HANDOFF-RUN-LOG.md` Item 10: per-milestone A–F-style lines with
      commits + gate evidence, baseline block from T003, findings for the final report,
      RESULT: COMPLETED line; commit `docs(058): run log — spec 058 complete, all four
      milestones green (spec 058)`
- [ ] T024 XVI: independent verification before merge — dispatch a fresh no-context subagent to
      re-run build + sequential units + Playwright from a clean detached worktree of the branch
      tip (or hand the gate evidence to the human/reviewing session); record the outcome in the
      run log. **Do not merge** — flag the branch for the independent-verification gate
- [ ] T025 XII: `git checkout master` from the worktree at session end (if master is held by the
      main worktree, detach the worktree head and report the constraint instead of forcing it)

## Dependencies & Execution Order

- T001 → T002 → T003 → (milestones strictly in order: M1 → M2 → M3 → M4 — the spec's priority
  order; M2's DashboardService red-proof depends on M1's contract, M3/M4 are independent of each
  other but sequenced after M2 per spec priority).
- Within M1: T004 → T005 → T006 (red) → T007 (green) → T008 → T009.
- Within M2: T010–T013 are mutually independent (`[P]` — parallel subagents, separate files),
  all depend only on M1 landing; T014 after all four.
- Within M3: T015 → T016 → T017 → T018 (ADR before code, per the spec's "decision first").
- Within M4: T019/T021 both edit `Program.cs` (sequential); T020 is independent (`[P]`);
  T022 after all three.
- Phase 6 after all milestones.

## Parallel Execution Examples

- **M2 (max parallelism)**: 4 subagents, one per new test file (T010–T013), each in its own
  file, no shared fixtures (self-contained fakes per house pattern); parent runs T014's gate.
- **M4**: T020 (ArchitectureTests) in a subagent while the parent does T019 + T021 (Program.cs).
- Never parallelize: any two tasks touching the same file (Program.cs in M3/M4; the run log).

## Implementation Strategy

MVP = M1 alone (the user-visible correctness fix, independently committable and gateable). Each
later milestone is a self-contained increment on top: M2 makes M1 trustworthy, M3 documents +
applies the error convention, M4 removes the remaining debt. A failure in any milestone's gate
sequence stops at that milestone (XIV/XV: ≤ 2 attempts, classify before retry 2, diagnosis into
these task notes) — earlier milestones stay committed and reviewable (FR-007/SC-005).
