# Feature Specification: Serialize Enrollment.Tests on the Shared Database

**Feature Branch**: `bug/056-serialize-enrollment-tests`

**Created**: 2026-09-23

**Status**: Complete (2026-09-23 — merged to master @ 978cc50; authoritative gates green:
branch CI 35812601046 and master CI 35817464596, both fresh-DB full-suite success per
Principle XVII; local gates supporting — see tasks.md T004–T009 and run log item 8)

**Input**: Handoff task 1 (2026-09-23). The CI flake is on record: run 35807132600 FAILED
at `Enrollment.Tests.AdminListLearnersTests.empty_search_is_no_filter`
(`Assert.Equal() Failure: Values differ` — one filler row between the two reads), and the
next run on identical test code, 35807448405, succeeded — green-but-flaky. Same defect
class as the one fixed in Catalog.Tests by `f0ba9d6` (direct-to-master commit — recorded
in `specs/HANDOFF-RUN-LOG.md`).

## Root Cause (verified against the code at 490d358)

- xUnit runs test **classes** in parallel by default, and `tests/Enrollment.Tests/` has
  neither an `AssemblyInfo.cs` nor any collection configuration.
- `AdminListEnrollmentsTests` (`IAsyncLifetime`) seeds 12 filler students + 6 filler
  courses (marker prefix `AdmPg032E`) in `InitializeAsync` and deletes them in both
  `InitializeAsync` (idempotent stale cleanup) and `DisposeAsync` — on the shared
  `Students`/`Courses`/`Enrollments` tables that the `dbo.AdminListLearners` stored
  procedure aggregates over.
- `AdminListLearnersTests.empty_search_is_no_filter` issues **two catalog-wide SP reads**
  (empty-string search, then NULL search) and asserts the totals are equal. When one of
  the sibling class's inserts/deletes lands between the two reads, the totals differ by
  exactly the filler rows whose lifecycle straddled the gap — the CI failure
  (`Expected 25, Actual 24`).

The assertion itself is correct: an empty search string must behave exactly like NULL
(no filter) — that is the SP's documented contract (spec 042). The concurrency is the
bug, not the assertion.

## User Scenarios & Testing

### User Story 1 — The unit gate is deterministic (Priority: P1)

`Enrollment.Tests` must pass on a clean, seeded database regardless of how xUnit
interleaves it with its sibling test classes — the same property Catalog.Tests gained in
`f0ba9d6`. A gate that passes once and fails the next run is not a gate (Principle XIII
evidence must be trustworthy; Principle XVII requires it to hold on a fresh database).

**Why this priority**: this is the only known red in an otherwise green unit gate, and it
is the same defect class the repo has already decided to fix with assembly-level
serialization rather than assertion weakening.

**Independent Test**: run the sequential test gate (`run.sh`) repeatedly; then run CI
(fresh per-run database) on the branch and on master.

**Acceptance Scenarios**:

1. **Given** a clean seeded database, **When** `Enrollment.Tests` runs, **Then**
   `empty_search_is_no_filter` passes on every run — the interleaving that caused the
   CI failure is structurally impossible, not merely unlikely.
2. **Given** the full sequential gate (all six unit projects), **When** it runs three
   times in a row locally, **Then** all 197 tests pass all three times (supporting
   evidence per Principle XVII).
3. **Given** the branch is pushed, **When** CI runs against its freshly created
   MSSQL/Valkey pair, **Then** the unit gate and the E2E suite are green (the
   authoritative Principle XIII run).

## Out of Scope (explicit)

- **Loosening the assertion** (e.g. comparing with a tolerance, or only comparing row
  sets). The assertion is the contract; weakening it would convert a real race into a
  silent contract erosion.
- **Per-test-class `[Collection]` attributes or a `.runsettings`**: the house pattern,
  established by `f0ba9d6`, is assembly-level `CollectionBehavior(
  DisableTestParallelization = true)`. It is one file, it covers the whole project, and
  it matches the sibling fix reviewers already know.
- **`Scorm.Tests`**: evaluated and deliberately left unchanged — see `research.md`
  §"Scorm.Tests decision" for the per-class evidence.
- **Isolating the shared database** (per-project DBs, `CollectionDefinition`-scoped
  fixtures, transactions). A larger design change for a problem the assembly-level
  fix eliminates; not justified by a one-file, five-second cost.

## Verification Plan (Principles XIII, XIV, XVI, XVII)

- **Gate 1** — `dotnet build` clean + the Host running and responding (local,
  supporting evidence).
- **Gate 2** — full sequential unit gate locally ×3 (supporting) **plus** CI on the
  branch: fresh database, full suite (authoritative per Principle XVII).
- **Gate 3** — after merging to `master`: local rebuild/restart/re-run, and CI on
  master green.
- **XVI** — a fresh, context-free subagent re-runs build + unit gate + Playwright from
  a clean checkout of the branch before the merge.
