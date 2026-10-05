# Implementation Plan: Code Review Remediation (2026-10-05 pass) — spec 058

**Branch**: `story/058-code-review-remediation` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `story/<id>-<desc>` for features.

**Input**: Feature specification from `specs/058-code-review-remediation/spec.md`

**Note**: This plan was authored on `master` per Principle IX. The spec commit (3b421c3) was
fast-forwarded onto `master` by an unconfined session before planning, per the deviation recorded
in `HANDOFF.md`. Implementation happens on `story/058-code-review-remediation` in the dedicated
worktree; plan/tasks artifacts live here on `master` (house shape, cf. spec 057: spec a3c0ca7 +
plan d0fd556 on master).

## Summary

Remediate four findings from the 2026-10-05 full-codebase review, as four independently
committable milestones in priority order:

1. **P1 (US1)** — `DashboardService` hardcodes `AverageCompletionRate`/`CompletedCourseCount`/
   `AverageScore` to 0 (DashboardService.cs:67,101,116-117). Wire all three dashboard views to real
   `CourseAttempt` data via a **new `Scorm.Contracts` interface** (`IScormAttemptStats`) — the
   Scorm module owns attempt data; Management may only touch it through Contracts (Principle III).
2. **P2 (US2)** — add `Management.Tests` unit coverage for `DashboardService`,
   `OrganizationLookup`, `UserInfoLookup`, `OrgSubtree`, `TreeLayoutService` (currently 0).
3. **P3 (US3)** — ADR 0014 picking the error-handling convention for Management Application
   services, then collapse the repeated 4-block catches in `Program.cs` into a single shared
   translation point with zero observable HTTP change.
4. **P4 (US4)** — housekeeping: duplicate `Database.Migrate()` calls, two missing
   ArchitectureTests boundary assertions, inconsistent auth-group declaration style.

Technical approach: one new cross-module contract (P1), EF InMemory + fake-contract unit tests
(P2, the existing Management.Tests pattern), a Host-level exception-translation helper (P3), and
three surgical cleanups (P4). No migrations, no schema changes, no new NuGet packages, no new
projects.

## Technical Context

**Language/Version**: C# / .NET 10 (GA, pinned via `global.json`)

**Primary Dependencies**: ASP.NET Core minimal APIs + Razor Pages, EF Core (MSSQL),
StackExchange.Redis (Valkey), xUnit, NetArchTest (all already in the repo; **no new packages**)

**Storage**: MSSQL `LearningLms` (system of record — `CourseAttempt` rows already exist; P1 only
reads them). Valkey untouched (P1 must not read the ephemeral SCORM session bag — attempts are
persisted on `LMSCommit`/`LMSFinish`, Principle VI).

**Testing**: xUnit unit tests (Management.Tests already references
`Microsoft.EntityFrameworkCore.InMemory` 10.0.0 — used by `CourseVisibilityScopeTests`),
ArchitectureTests (NetArchTest), Playwright E2E. Test gate is the sequential per-project runner
(`.pi/skills/sequential-project-test-runner/scripts/run.sh`) — never solution-wide.

**Target Platform**: Linux/Windows dev box (host-side run) + GitHub Actions CI (the authoritative
Principle XIII/XVII run — fresh per-run MSSQL/Valkey).

**Project Type**: modular monolith web app (`src/Host` composition root, 4 modules under
`src/Modules/`, each with `*.Contracts` boundary project)

**Performance Goals**: dev scale (a handful of orgs, dozens of learners) — the existing
dashboard code already uses one bulk query per metric; P1 keeps that shape (one aggregate query
per dashboard view). No new hot-path work.

**Constraints**:
- No observable HTTP behavior change outside US1's intended fix (P3/P4 are behavior-preserving).
- Management may reference Scorm only via `LibreLms.Contracts.Scorm` (Principle III, enforced by
  ArchitectureTests).
- `dotnet build` must stay 0 errors (NU1903 is an error per `Directory.Build.props`).
- E2E tests must stay idempotent against the persistent dev DB (unique-per-run identities,
  `>=`/lower-bound assertions, best-effort teardown) — repo house pattern.

**Scale/Scope**: ~15 source/test files touched across 4 milestones; no new modules or projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Modular Monolith | PASS | No new modules; Scorm exposes a new Contracts interface, Management consumes it — the designed seam. |
| II. Clean Architecture | PASS | New abstractions: `IScormAttemptStats` (+ 1 summary record) — "cross-module contract for SCORM attempt aggregates"; `ManagementErrors.Translate` — "translates Management's expected business exceptions into their HTTP responses". Both one-sentence explainable. No MediatR/CQRS/repository layers. |
| III. Module Boundaries Compiled | PASS | P1 crosses into Scorm only via `Scorm.Contracts`; P4 *strengthens* the compiled checks (2 new ArchitectureTests assertions). |
| IV. Human-Legible AI-Authored Code | PASS | P3 decision recorded as ADR 0014; explicit control flow kept; no clever generalization (the ADR explicitly rejects a per-endpoint config-driven mapper). |
| V. Sandbox Not Optional | DEVIATION (declared, not silently) | CLAUDE.md §5 open conflict: constitution mandates in-devcontainer work; recent practice (specs 051–057) is host-side with Docker for services. This run is **host-side** (stated in every gate report). The conflict is left unresolved per §5. |
| VI. Polyglot Storage | PASS | Dashboard aggregates read MSSQL `CourseAttempts` only; nothing read from or added to Valkey. |
| VII. Spec-Driven Sliced Thin | PASS | Spec → plan → tasks → implement; this slice is remediation of a reviewed finding set. |
| VIII. Branching Discipline | PASS | Implementation on `story/058-code-review-remediation` (worktree); planning artifacts on `master` per house shape; no code commits on master. |
| IX. Plan On Master Only | PASS | This plan + tasks authored on `master` after the documented spec fast-forward. |
| X. No Ad-Hoc Fixes | PASS | Every change is scoped to this spec's four user stories. |
| XI. Parallel Implementation | N/A at plan time | Independent P2 test files / P4 items may run as parallel subagents; parent remains sole writer. |
| XII. Return to Master | PASS (deferred) | `git checkout master` from the worktree at branch end (blocked while the main worktree holds master — will detach or report). |
| XIII. Verification Before Claim | GATED | Per milestone: build + running app + sequential units + Playwright (local, host-side, supporting) **and** branch CI on a fresh DB (authoritative, observed via `gh`). |
| XIV/XV. Bounded Retry / Triage | GATED | Max 2 attempts per failing gate per milestone; classify before retry 2; diagnosis into task notes + stop on 2nd failure. |
| XVI. Independent Verification | GATED | The reviewing Claude session (separate worktree) + a fresh no-context subagent re-run from a clean worktree before merge; **no self-merge**. |
| XVII. Disposable Environment | GATED | Authoritative evidence = branch CI (fresh per-run DB). Local runs against the long-lived dev DB are supporting only; every local report states host-side mode. E2E tests create their own data (unique learner/org per run) and read endpoints from configuration. |

**Gate result**: PASS — no unjustified violations (Principle V deviation is the pre-existing,
documented §5 conflict, declared in every gate report, not newly introduced).

## Project Structure

### Documentation (this feature)

```text
specs/058-code-review-remediation/
├── spec.md              # (already committed — 3b421c3)
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # /speckit.tasks output (separate commit)
```

### Source Code (touched paths)

```text
# P1 — US1 (new contract + wiring)
src/Modules/Scorm.Contracts/IScormAttemptStats.cs        # NEW: IScormAttemptStats + AttemptStatsSummary
src/Modules/Scorm/Application/ScormAttemptStatsService.cs # NEW: implementation over ScormDbContext
src/Modules/Scorm/Endpoints/ScormModuleExtensions.cs      # register IScormAttemptStats (scoped)
src/Modules/Management/Application/DashboardService.cs    # wire 3 views to real stats (ctor +1 dep)
tests/Management.Tests/DashboardServiceTests.cs           # NEW (lands with P1: red→green regression guard)
tests/Playwright.Tests/tests/21-dashboard-real-stats.spec.ts # NEW: US1 independent test (E2E)

# P2 — US2 (unit tests only)
tests/Management.Tests/OrganizationLookupTests.cs         # NEW
tests/Management.Tests/UserInfoLookupTests.cs             # NEW
tests/Management.Tests/OrgSubtreeTests.cs                 # NEW
tests/Management.Tests/TreeLayoutServiceTests.cs          # NEW

# P3 — US3 (ADR + catch-block collapse)
docs/adr/0014-management-error-handling-convention.md     # NEW (next after 0013)
src/Host/ManagementErrors.cs                              # NEW: single translation point
src/Host/Program.cs                                       # users (4 cited) + orgs + adminCourses handlers
tests/Host.Tests/ManagementErrorsTests.cs                 # NEW: pins the 4 mappings

# P4 — US4 (housekeeping)
src/Host/Program.cs                                       # dedupe Migrate(); uniform group auth
tests/ArchitectureTests/ModuleBoundaryTests.cs            # 2 new assertions
```

**Structure Decision**: no new projects/modules — remediation stays inside the existing
monolith layout. The only new cross-module surface is one interface + one record in
`Scorm.Contracts` (the boundary the architecture already provides).

## Milestone → Commit Map (FR-007)

| Milestone | Story | Commit(s) on `story/058-code-review-remediation` |
|---|---|---|
| M1 (P1) | US1 | `feat(058): wire dashboard completion/score metrics to real SCORM attempt data (spec 058)` |
| M2 (P2) | US2 | `test(058): unit tests for Management lookup/subtree/layout services (spec 058)` |
| M3 (P3) | US3 | `docs(058): ADR 0014 — Management error-handling convention (spec 058)` + `fix(058): collapse repeated Management catch blocks via ManagementErrors per ADR 0014 (spec 058)` |
| M4 (P4) | US4 | `fix(058): drop duplicate Database.Migrate calls; standardize admin route-group auth (spec 058)` + `test(058): ArchitectureTests — Contracts reverse-dependency + SharedKernel isolation (spec 058)` |

Each milestone: code commit(s) → gate 1 (local, host-side) → push → gate 2 (branch CI, fresh DB)
→ run-log entry in `specs/HANDOFF-RUN-LOG.md` (committed on the branch as
`docs(058): run log — …`). Convention verified from `git log` (conventional-commit subject with
spec number; `fix(ci)`/`test(e2e)`-style scope variants exist; no attribution lines in current
history).

## Verification Strategy (per milestone)

- **Gate 1 (local, host-side, supporting)**: `dotnet build LibreLms.slnx` 0 errors → restart Host
  (`scripts/restart-app.sh --background`, readiness = `/` → 302) → sequential unit runner green →
  Playwright (full suite or the milestone's spec) green. Requires `docker compose up -d` (MSSQL +
  Valkey) and the documented host env exports (`ConnectionStrings__Sql`, `ConnectionStrings__Valkey`,
  `ASPNETCORE_ENVIRONMENT=Development`).
- **Gate 2 (authoritative, XVII)**: push branch → CI (`.github/workflows/ci.yml`, fresh MSSQL/Valkey
  per run) → observed to SUCCESS via `gh run watch`/`gh run view`. Record the CI run ID in the
  run log.
- **Gate 3**: after the (externally performed) merge to master — rebuild/restart/re-run; the
  implementing session does **not** merge (XVI). Flagged for the independent-verification gate.

## Complexity Tracking

No constitution violations requiring justification (see Constitution Check).
