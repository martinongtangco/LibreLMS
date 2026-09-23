# Tasks: Fix Stale Agent-Facing Docs After Constitution v1.9.0 (spec 057)

**Branch**: `bug/057-fix-stale-agent-docs` | **Plan**: [plan.md](plan.md)

**Prerequisites**: spec 056 merged to `master` (branch is cut from the post-merge state)

## Format: `[ID] [P?] Description`

## Phase 1: Setup

- [X] T001 Author spec/plan/tasks on `master` (Principle IX) — a3c0ca7, d0fd556
- [X] T002 Create branch `bug/057-fix-stale-agent-docs` from `master` (Principle VIII)

## Phase 2: Edits

- [X] T003 `CLAUDE.md` §2: rewrite the three-specs Valkey bullet (post-1748f50: env
      var with compose-hostname fallback; warning conditional on the export).
- [X] T004 `CLAUDE.md` §2: replace "The in-container run is the canonical one for the
      full suite" with a pointer to the constitution's authoritative-run statement.
- [X] T005 `CLAUDE.md` §0: add the XVII bullet (Sync Impact Report Deferred item (a)).
- [X] T006 [P] `specs/036-org-tree-branching/quickstart.md` §3: tests build the
      hierarchy themselves (a03eee2); manual UI steps demoted to optional
      (Deferred item (b), XVII.1).

## Phase 3: Gates (XIII, XIV, XVI, XVII)

- [X] T007 Gate 1 (sanity): `dotnet build` **0 Error(s)** (4 warnings); Host `/` → 302.
      (An earlier 18-error build in this session was MSB3027 file locks from the running
      host holding bin/ DLLs — environmental, zero compile errors; clean build after stop.)
- [X] T008 Gate 2 (claim cross-check): all seven claims independently re-verified by the
      XVI subagent with line-level citations — flushScormSessions pattern at
      14-profile-courses.spec.ts:34 / 15-scorm-launch-ui.spec.ts:45 /
      20-scorm-session-authz.spec.ts:32 (introduced by 1748f50, `git log -S` shows it
      touched only that commit); recovery-path-only invocation confirmed in all three;
      constitution Development Workflow bullet (authoritative-run statement) quoted;
      all four XVII clauses matched; 06-admin-organizations.spec.ts beforeAll
      ensure-if-missing at :146–:169 confirmed added by a03eee2; diff is 2 .md files
      only. No behavior changed, so no new E2E test (XIII clause has nothing to cover).
- [X] T009 Commit `docs(057): …` = 7c3a6ed; pushed; **branch CI 35820076981 SUCCESS**
      (fresh DB, full suite, 6m28s) — authoritative per XVII.
- [X] T010 XVI: fresh context-free subagent — verdict **GREEN**, claims A–G all
      verified (report recorded in the run log).
- [X] T011 Merge to `master` = e2b7e1d (`--no-ff`); pushed.
- [X] T012 Gate 3: master CI green (see run log item 9); spec Complete (this commit);
      run-log item 9 appended.
