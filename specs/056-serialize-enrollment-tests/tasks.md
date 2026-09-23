# Tasks: Serialize Enrollment.Tests on the Shared Database (spec 056)

**Branch**: `bug/056-serialize-enrollment-tests` | **Plan**: [plan.md](plan.md)

**Prerequisites**: spec.md, plan.md, research.md

**Organization**: one code task (the one-file fix), then the XIII/XVI/XVII gate
sequence. No [P] tasks — the single code change has nothing to parallelize with.

## Format: `[ID] [P?] Description`

## Phase 1: Setup

- [X] T001 Author spec/plan/research/tasks on `master` (Principle IX) — c03462d, c588ae1, 8911fa6
- [X] T002 Create branch `bug/056-serialize-enrollment-tests` from `master` (Principle VIII)

## Phase 2: Implementation

- [X] T003 Add `tests/Enrollment.Tests/AssemblyInfo.cs`:
      `[assembly: CollectionBehavior(DisableTestParallelization = true)]` + comment
      naming `AdminListLearnersTests.empty_search_is_no_filter`, the sibling's per-row
      `AdmPg032E` filler INSERTs, and the CI evidence (Expected 25, Actual 24). Mirror
      the Catalog.Tests file's shape.

## Phase 3: Gates (XIII, XIV, XVI, XVII)

- [X] T004 Gate 1 (local, supporting): build **0 Error(s)** (9 pre-existing warnings, no
      NU1903); Host `Now listening on: http://localhost:5000` + `:7095`; `/` → **302**.
- [X] T005 Local unit gate ×3 (supporting): three consecutive `run.sh` passes, each
      `===== TOTAL: passed=197 failed=0 skipped=0 =====` (Arch 14, Catalog 39, Enrollment
      42, Host 29, Management 55, Scorm 18).
- [X] T006 Commit `test(056): …` = 256247e; pushed; **branch CI 35812601046 SUCCESS**
      (fresh DB, full build-and-test job, 6m36s) — authoritative gate 2 per XVII.
- [X] T007 XVI: fresh context-free subagent, detached clean worktree @256247e — build 0
      errors; unit gate 197/197; target test green in isolation; fix file present; local
      Playwright RED with documented environmental root cause (WSL2 swap thrashing: 81% of
      1 GiB swap, host RAM 91% used → intermittent 30s SQL timeouts on catalog pages; SP
      and app respond in ms when not stalled; a one-line test-only diff cannot be the
      cause). Classified **Blocking — environment** per XV.3; diagnosis recorded, no retry
      budget burned (XIV).
- [X] T008 Merge to `master` = 978cc50 (`--no-ff`); pushed.
- [X] T009 Gate 3: **master CI 35817464596 SUCCESS** (fresh DB, 6m36s) — authoritative.
      Local (supporting): rebuild 0 errors after the merge; host restarted on the merged
      build (`Now listening` ×2, `/` → 302); local full E2E environmentally RED (same
      WSL2 swap signature — 33 passed / 1 skipped / rest failed-or-not-run in two full
      attempts, then XIV budget exhausted; per XVII local runs are supporting, CI is the
      proof).
- [X] T010 Spec status → Complete (this commit); run-log item 8 appended.
