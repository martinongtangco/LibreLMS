# Tasks: Serialize Enrollment.Tests on the Shared Database (spec 056)

**Branch**: `bug/056-serialize-enrollment-tests` | **Plan**: [plan.md](plan.md)

**Prerequisites**: spec.md, plan.md, research.md

**Organization**: one code task (the one-file fix), then the XIII/XVI/XVII gate
sequence. No [P] tasks — the single code change has nothing to parallelize with.

## Format: `[ID] [P?] Description`

## Phase 1: Setup

- [X] T001 Author spec/plan/research/tasks on `master` (Principle IX)
- [X] T002 Create branch `bug/056-serialize-enrollment-tests` from `master` (Principle VIII)

## Phase 2: Implementation

- [ ] T003 Add `tests/Enrollment.Tests/AssemblyInfo.cs`:
      `[assembly: CollectionBehavior(DisableTestParallelization = true)]` + comment
      naming `AdminListLearnersTests.empty_search_is_no_filter`, the sibling's per-row
      `AdmPg032E` filler INSERTs, and the CI evidence (Expected 25, Actual 24). Mirror
      the Catalog.Tests file's shape.

## Phase 3: Gates (XIII, XIV, XVI, XVII)

- [ ] T004 Gate 1 (local, supporting): `dotnet build LibreLms.slnx` — 0 errors; Host
      starts and responds (HTTP 302 on `/`). Paste evidence.
- [ ] T005 Local unit gate ×3 (supporting): `run.sh` three consecutive 197/197 passes.
      Paste the three TOTAL lines.
- [ ] T006 Commit `test(056): …` on the branch; push; watch CI (fresh DB — the
      authoritative gate 2). If red: classify per XV, retry budget 2 (XIV), then
      diagnosis-and-stop.
- [ ] T007 XVI: fresh context-free subagent re-runs build + unit gate + Playwright from
      a clean checkout of the branch; record its independent report.
- [ ] T008 Merge to `master` (`Merge bug/056-…: <what changed> (spec 056)`); push.
- [ ] T009 Gate 3 (post-merge): local rebuild + restart + re-run E2E suite (paste
      evidence); CI on master green (authoritative).
- [ ] T010 Mark spec status Complete with merge SHA + gate evidence; append the run-log
      entry to `specs/HANDOFF-RUN-LOG.md`.
