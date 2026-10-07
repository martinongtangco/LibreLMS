# Feature Specification: Code Review Remediation (2026-10-05 pass)

**Feature Branch**: `story/058-code-review-remediation`

**Created**: 2026-10-05

**Status**: Draft

**Input**: User description: "Remediate findings from the 2026-10-05 full-codebase review: duplicate
Database.Migrate() calls, hardcoded-zero dashboard completion stats, missing Management unit tests,
inconsistent error-handling convention (exceptions vs Result<T>), inconsistent auth-group
declaration style in Program.cs, and gaps in ArchitectureTests' module-boundary coverage. This is a
long-running, multi-milestone remediation: each milestone below is independently committable. The
implementing agent commits to this branch after each milestone; commits are reviewed later, not
blocked on in-flight."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Dashboard shows real completion data (Priority: P1)

Every Management dashboard (System, Org, Personal) currently reports `AverageCompletionRate`,
`CompletedCourseCount`, and `AverageScore` as hardcoded `0.0` /`0` regardless of actual learner
activity (`src/Modules/Management/Application/DashboardService.cs:67,101,116-117`), even though the
Scorm module already tracks per-attempt completion and score data. Any admin or learner viewing a
dashboard today sees a fake "0% completion" no matter how much real progress exists.

**Why this priority**: Highest-impact finding — it's user-visible, wrong on every page load, and
undermines trust in the whole admin reporting surface. P1 because it's a correctness bug with
product-visible impact, not just internal debt.

**Independent Test**: Seed a learner with a completed, scored SCORM attempt; load System, Org, and
Personal dashboards; confirm the completion rate and score reflect that attempt instead of 0.

**Acceptance Scenarios**:

1. **Given** a learner has one completed SCORM attempt with a recorded score, **When** an OrgAdmin
   views the Org dashboard for that learner's organization, **Then** `AverageCompletionRate` and
   `AverageScore` reflect that attempt, not 0.
2. **Given** no learner in an org has any SCORM attempt, **When** the dashboard is viewed, **Then**
   the metric reads 0 explicitly because there is no data (not because the field is unwired).
3. **Given** the System dashboard aggregates across all orgs, **When** it is viewed, **Then** its
   completion/score figures are the aggregate of the same real per-attempt data used by the Org and
   Personal views (no separate hardcoded path).

---

### User Story 2 - Management services have unit test coverage (Priority: P2)

`DashboardService`, `OrganizationLookup`, `UserInfoLookup`, `OrgSubtree`, and `TreeLayoutService`
have no unit tests today (`tests/Management.Tests/` only covers `CourseVisibilityScopeTests`,
`EnrollmentScopeTests`, `OrganizationScopeTests`, `UserServiceScopeTests`). `DashboardService`'s
`GetOrgMetricsAsync` subtree-sum logic spans three cross-module contracts and is currently
unverified by any test — including after Story 1 rewires it to real data.

**Why this priority**: P2 — this is what makes Story 1's fix trustworthy and keeps it from
regressing silently. Sequenced right after the data-wiring fix it covers.

**Independent Test**: Run `dotnet test tests/Management.Tests/Management.Tests.csproj` before and
after; new tests fail on the old hardcoded-zero behavior and pass once Story 1 lands.

**Acceptance Scenarios**:

1. **Given** `DashboardService.GetOrgMetricsAsync` with a seeded org subtree of known depth,
   **When** the unit test calls it with fake/stub contracts for Scorm and Enrollment lookups,
   **Then** the returned aggregate matches a hand-computed expected value.
2. **Given** `TreeLayoutService` with a multi-level org tree, **When** layout is computed, **Then**
   node positions/depths match expected values for at least one non-trivial tree shape.

---

### User Story 3 - One documented error-handling convention (Priority: P3)

Management's Application services (`UserService`, `OrganizationService`, `AdminEnrollmentService`,
`CourseVisibilityService`) throw exceptions (`ForbiddenAccessException`, `KeyNotFoundException`,
`InvalidOperationException`, `ArgumentException`) that `Program.cs` catches with the same four-block
pattern repeated roughly ten times (e.g. `Program.cs:462-473`, `480-497`, `504-521`, `526-543`).
Meanwhile `SharedKernel.Result<T>` exists and is used only by `RegistrationService` and
`ScormPackageService`. Two conventions coexist with no documented reason to prefer either.

**Why this priority**: P3 — real debt, but it's a refactor that needs a decision first (see ADR
requirement below), not a quick fix, so it's sequenced after the two concrete bugs/gaps above.

**Independent Test**: An ADR exists in `docs/adr/` picking one convention; a follow-up commit on
this branch collapses at least the four repeated catch-blocks cited above into the chosen pattern
without changing any endpoint's observable status codes or response bodies.

**Acceptance Scenarios**:

1. **Given** the ADR has picked a convention, **When** an endpoint handler that previously had a
   four-block catch is refactored, **Then** its HTTP status codes and JSON error shapes for each
   error case are unchanged (verified by existing/added tests).

---

### User Story 4 - Housekeeping: dead code and boundary-test gaps (Priority: P4)

Two small, independent cleanups bundled as one low-risk milestone:

- `Program.cs:161-167` calls `enrollmentCtx.Database.Migrate()`, `scormCtx.Database.Migrate()`, and
  `managementCtx.Database.Migrate()` twice each (duplicate block). Idempotent but confusing.
- `tests/ArchitectureTests/ModuleBoundaryTests.cs:21-29` only asserts module-internals don't
  reference other modules' internals. It does not assert a `*.Contracts` project stays free of a
  reverse dependency on its own module's internals, nor that `SharedKernel` stays free of
  dependencies on any module. Both hold true today but are unenforced.
- `Program.cs:767` declares `adminEnrollments`'s auth at the route-group level
  (`RequireAuthorization(new AuthorizeAttribute{Roles=...})`) while sibling groups (`users`, `orgs`,
  `adminCourses`) use a bare `.RequireAuthorization()` plus a repeated per-handler
  `[Authorize(Roles=...)]`. Functionally equivalent today; standardize on one style so a new handler
  added to any group can't silently inherit the wrong pattern.

**Why this priority**: P4 — lowest risk, no behavior change intended, good fill-in work between the
higher-priority milestones.

**Independent Test**: `dotnet build` succeeds; `tests/ArchitectureTests/ArchitectureTests.csproj`
gains the two new assertions and passes; every endpoint under `adminEnrollments`, `users`, `orgs`,
and `adminCourses` still returns the same 401/403 behavior for anonymous/wrong-role callers as
before (covered by existing Playwright/auth tests).

**Acceptance Scenarios**:

1. **Given** `Program.cs:161-167` after the fix, **When** the Host starts, **Then** each context's
   `Database.Migrate()` is called exactly once.
2. **Given** the new ArchitectureTests assertions, **When** a hypothetical reverse-dependency is
   introduced (manually verified during development, not left in the tree), **Then** the test
   fails — confirming the assertion actually enforces the rule.

---

### Edge Cases

- What happens when a learner has SCORM attempts but no completed ones (Story 1)? Completion rate
  must read 0% from real data, distinguishable only in code, not in the UI, from "no data" — both
  render as 0%, which is correct; the bug being fixed is that *all* dashboards showed 0% universally
  regardless of underlying data.
- What happens when the ADR (Story 3) concludes the two conventions should both stay, scoped
  differently (e.g. `Result<T>` for expected business-rule outcomes, exceptions for truly
  exceptional/infrastructure failures)? That is an acceptable ADR outcome; Story 3's acceptance
  criteria only requires a documented decision and at least one refactor applying it, not a
  wholesale rewrite.
- How does the system handle an org subtree with zero members when computing dashboard aggregates
  (Story 1/2)? Must not divide by zero; must return 0, not throw.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `DashboardService` MUST compute `AverageCompletionRate`, `CompletedCourseCount`, and
  `AverageScore` from real SCORM attempt data (via `Scorm.Contracts`) for System, Org, and Personal
  dashboard views, replacing the current hardcoded `0.0` placeholders.
- **FR-002**: `DashboardService.GetOrgMetricsAsync`, `TreeLayoutService`, `OrganizationLookup`, and
  `UserInfoLookup` MUST have unit test coverage in `tests/Management.Tests/` exercising at least one
  non-trivial case each.
- **FR-003**: An ADR MUST be added to `docs/adr/` (next sequential number after 0013) documenting
  the chosen error-reporting convention (exceptions vs. `Result<T>`, or a scoped mix) for Management
  Application services, and at least the four cited repeated catch-blocks in `Program.cs` MUST be
  refactored to follow it without changing observable HTTP behavior.
- **FR-004**: `Program.cs`'s duplicate `Database.Migrate()` calls (lines ~161-167) MUST be reduced
  to one call per context.
- **FR-005**: `tests/ArchitectureTests/ModuleBoundaryTests.cs` MUST gain assertions that (a) no
  `*.Contracts` project depends on its own module's `Domain`/`Application`/`Infrastructure`, and (b)
  `SharedKernel` has no dependency on any module or its contracts.
- **FR-006**: The auth-declaration style across `adminEnrollments`, `users`, `orgs`, and
  `adminCourses` route groups in `Program.cs` MUST be made consistent (one style, applied
  everywhere), with no change to which roles can access which endpoint.
- **FR-007**: Each user story above MUST land as its own commit (or tightly-scoped commit set) on
  `story/058-code-review-remediation`, following this repo's observed commit convention
  (`feat(058): …` / `fix(058): …` / `test(058): …` / `docs(058): …`), so progress is reviewable
  milestone-by-milestone without waiting for the whole branch to finish.

### Key Entities

- **Dashboard metrics**: `AverageCompletionRate`, `CompletedCourseCount`, `AverageScore` — derived,
  not stored; currently computed as constants, must become real aggregates over SCORM attempt data
  scoped by org subtree (System/Org) or by learner (Personal).
- **Org subtree**: existing concept (`OrgSubtree`, `TreeLayoutService`) — this spec adds test
  coverage, does not change its shape.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every Management dashboard view (System, Org, Personal) renders a non-hardcoded
  completion rate and score that changes when underlying SCORM attempt data changes.
- **SC-002**: `tests/Management.Tests/` test count increases by at least 4 tests covering the
  previously-untested services, and all pass.
- **SC-003**: An ADR exists documenting the error-handling convention decision, and at least the 4
  cited repeated catch-blocks are refactored to match it with zero change in endpoint status
  codes/response shapes (verified by existing tests still passing).
- **SC-004**: `dotnet build` has zero duplicate-migrate dead code; `ArchitectureTests` has 2 new
  passing assertions; auth-declaration style is uniform across the four cited route groups with no
  access-control regression (verified by existing auth/Playwright tests still passing).
- **SC-005**: All four milestones land as separately reviewable commits on
  `story/058-code-review-remediation`, each referencing this spec number.

## Assumptions

- This is remediation of findings from a prior read-only review (2026-10-05), not new product
  scope — no new user-facing features are introduced beyond "dashboards show correct numbers."
- The implementing agent works in its own git worktree on `story/058-code-review-remediation` and
  commits incrementally; a separate reviewing agent/human checks in periodically rather than
  blocking each commit.
- Story 3 (error-handling ADR) may conclude with a scoped-mix decision rather than full
  standardization on one mechanism; FR-003's bar is "documented decision + at least one applied
  refactor," not "every service rewritten."
- Per-run CI (`.github/workflows/ci.yml`) against a fresh database remains the authoritative
  Principle XIII verification gate for every milestone; any host-side local run is supporting
  evidence only (Constitution §0, Principle XVII).
- Each user story's acceptance scenarios double as its `/speckit.tasks` breakdown input; the
  implementing agent runs `/speckit.plan` and `/speckit.tasks` on this spec before writing code, per
  the normal spec-kit cycle.
