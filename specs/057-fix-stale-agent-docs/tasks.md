# Tasks: Fix Stale Agent-Facing Docs After Constitution v1.9.0 (spec 057)

**Branch**: `bug/057-fix-stale-agent-docs` | **Plan**: [plan.md](plan.md)

**Prerequisites**: spec 056 merged to `master` (branch is cut from the post-merge state)

## Format: `[ID] [P?] Description`

## Phase 1: Setup

- [ ] T001 Author spec/plan/tasks on `master` (Principle IX)
- [ ] T002 Create branch `bug/057-fix-stale-agent-docs` from `master` (Principle VIII)

## Phase 2: Edits

- [ ] T003 `CLAUDE.md` §2: rewrite the three-specs Valkey bullet (post-1748f50: env
      var with compose-hostname fallback; warning conditional on the export).
- [ ] T004 `CLAUDE.md` §2: replace "The in-container run is the canonical one for the
      full suite" with a pointer to the constitution's authoritative-run statement.
- [ ] T005 `CLAUDE.md` §0: add the XVII bullet (Sync Impact Report Deferred item (a)).
- [ ] T006 [P] `specs/036-org-tree-branching/quickstart.md` §3: tests build the
      hierarchy themselves (a03eee2); manual UI steps demoted to optional
      (Deferred item (b), XVII.1).

## Phase 3: Gates (XIII, XIV, XVI, XVII)

- [ ] T007 Gate 1 (sanity): `dotnet build` 0 errors; Host responds 302.
- [ ] T008 Gate 2 (claim cross-check): table mapping every rewritten sentence → the
      code/constitution line it asserts (paste the lines). No behavior changed, so no
      new E2E test (XIII clause has nothing to cover).
- [ ] T009 Commit `docs(057): …` on the branch; push; branch CI green (authoritative,
      XVII).
- [ ] T010 XVI: fresh context-free subagent independently re-verifies each rewritten
      claim; record its report.
- [ ] T011 Merge to `master` (`Merge bug/057-…: <what changed> (spec 057)`); push.
- [ ] T012 Gate 3: master CI green; local 302 re-check. Mark spec Complete; append the
      run-log entry to `specs/HANDOFF-RUN-LOG.md`.
