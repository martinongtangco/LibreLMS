# Feature Specification: Fix Stale Agent-Facing Docs After Constitution v1.9.0

**Feature Branch**: `bug/057-fix-stale-agent-docs`

**Created**: 2026-09-23

**Status**: In progress

**Input**: Handoff task 2 (2026-09-23) plus the two Deferred items in the Sync Impact
Report of the v1.9.0 amendment (commit `ea2f426`).

## Problems (each verified against the repo at 490d358)

- **P1 — CLAUDE.md §2 "Host-side gotchas" is stale.** The bullet at lines 121–126 says
  the three SCORM-session specs "each define a `flushScormSessions()` that dials the
  compose hostname `valkey:6379`". Commit `1748f50` (which predates CLAUDE.md's
  commit) changed all three — `14-profile-courses.spec.ts`,
  `15-scorm-launch-ui.spec.ts`, `20-scorm-session-authz.spec.ts` — to read
  `process.env.ConnectionStrings__Valkey` with `valkey:6379` only as a fallback.
  The warning is still live (unexported var → fallback → `ENOTFOUND valkey`), just
  conditional now, and the fix is one line of export.
- **P2 — CLAUDE.md asserts "The in-container run is the canonical one for the full
  suite."** Constitution v1.9.0 (Development Workflow) now names
  `.github/workflows/ci.yml` as the authoritative Principle XIII run; a local run —
  in-container or host-side — is supporting evidence per Principle XVII.
- **P3 — CLAUDE.md §0 principle list omits XVII.** The v1.9.0 Sync Impact Report
  defers exactly this: "CLAUDE.md lists the principles that most often change what an
  agent does (XIII, XIV/XV, XVI, XII, V); XVII belongs in that list."
- **P4 — `specs/036-org-tree-branching/quickstart.md` §3 is stale.** It instructs a
  human to build the acceptance hierarchy (Root → Finance, Sales; Finance → Billing)
  by hand through the UI because "the DB is persistent (seeders only run on an empty
  DB)". Commit `a03eee2` changed the spec's tests to create whatever is missing in
  `beforeAll` via the admin API (idempotent no-op against a database that already has
  the hierarchy). XVII.1 now disallows hand-built fixture data as a test dependency;
  the prose still makes the E2E gate depend on it.

## User Scenarios & Testing

### User Story 1 — An agent reading CLAUDE.md is not misdirected (Priority: P1)

An agent following CLAUDE.md §2 on a Windows host exports
`ConnectionStrings__Valkey` (as §2's export block already instructs) and therefore
never hits the `ENOTFOUND valkey` path; if it does hit it, the doc names the actual
mechanism (missing export → compose-hostname fallback) and the actual fix. No doc
sentence asserts a superseded fact.

**Independent Test**: each factual sentence in the rewritten §2 bullet is checked
against the code it describes (the three specs' `flushScormSessions` and the
export block); the "canonical run" sentence is checked against the constitution's
Development Workflow section. Evidence recorded in tasks.md.

### User Story 2 — The §0 principle list matches the constitution (Priority: P2)

The "principles that most often change what you do" list in CLAUDE.md §0 includes
XVII (Verify Against a Disposable Environment) alongside XIII, XIV/XV, XVI, XII, V,
closing Deferred item (a) of the v1.9.0 amendment.

### User Story 3 — The 036 quickstart matches what the tests actually need (Priority: P2)

`specs/036-org-tree-branching/quickstart.md` §3 no longer presents manual UI
creation of the hierarchy as a prerequisite for the E2E gate. It states that the
tests build any missing nodes themselves (a03eee2), and keeps the UI steps only as
an optional manual exercise of the create-org flow (the feature spec 036 itself
shipped). Closes Deferred item (b).

## Out of Scope (explicit)

- Resolving the sandbox-vs-host conflict in CLAUDE.md §5 (Principle V) — a
  governance decision for a human (constitution amendment vs. stopping host-side
  practice).
- `README.md` staleness (module inventory, devcontainer-centric run instructions) —
  separate docs slice.
- Task 3's run-log entry (record act, not a docs fix).

## Verification Plan

Docs-only slice — no app behavior changes, so Principle XIII is applied as:

- **Gate 1**: `dotnet build` clean + Host responding (sanity that the tree is
  untouched-behavior-wise; the diff is two markdown files).
- **Gate 2**: claim-by-claim cross-check of every rewritten sentence against the
  code/constitution it describes (evidence in tasks.md), plus CI on the branch
  (fresh DB, full suite — authoritative per XVII and a mechanical proof nothing
  else broke). No E2E test is added: the changed artifact is documentation, and the
  "write a test if none covers the change" clause of XIII has no behavior to cover.
- **Gate 3**: post-merge CI on master green + local re-check that the app still
  responds.
- **XVI**: a fresh context-free subagent independently re-verifies each rewritten
  claim against the repo.
