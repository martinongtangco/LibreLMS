# Plan: Fix Stale Agent-Facing Docs After Constitution v1.9.0 (spec 057)

**Branch**: `bug/057-fix-stale-agent-docs` | **Spec**: [spec.md](spec.md)

**Constitution version**: 1.9.0

## Summary

Four surgical doc edits, two files:

1. `CLAUDE.md` §2 (lines ~121–126): rewrite the stale Valkey bullet to describe the
   post-`1748f50` reality — the three specs read
   `process.env.ConnectionStrings__Valkey` and fall back to the compose hostname
   `valkey:6379` only when it is unset. The warning stays, conditional: unexported var
   → fallback → `ENOTFOUND valkey` on the stale-session recovery path.
2. `CLAUDE.md` §2: drop "The in-container run is the canonical one for the full
   suite" and point instead at the constitution's Development Workflow
   (`.github/workflows/ci.yml` = the authoritative Principle XIII run; local runs are
   supporting evidence per XVII).
3. `CLAUDE.md` §0: add a XVII bullet to the "principles that most often change what
   you do" list (closes Sync Impact Report Deferred item (a)).
4. `specs/036-org-tree-branching/quickstart.md` §3: state that the E2E tests build
   any missing hierarchy nodes themselves (a03eee2 — idempotent, via the admin API,
   no-op against a database that already has them); demote the manual UI steps to an
   optional exercise of the create-org flow, not a test prerequisite (closes Deferred
   item (b); satisfies XVII.1).

## Files

| File | Change |
|---|---|
| `CLAUDE.md` | Edits 1–3 (three small rewrites, no section restructuring) |
| `specs/036-org-tree-branching/quickstart.md` | Edit 4 (§3 rewritten; §1–§2, §4–§N untouched) |

## Verification (docs-only — XIII applied honestly)

- Gate 1: `dotnet build` clean + Host responding (tree sanity).
- Gate 2: claim-by-claim cross-check (each rewritten sentence vs. the code/constitution
  line it cites — table in tasks.md) + branch CI green (authoritative, XVII).
- Gate 3: post-merge CI green + local 302.
- XVI: fresh subagent re-verifies the claims independently.

## Constitution Check

- **VIII**: `bug/057-fix-stale-agent-docs` from `master` (after 056 merges, so the
  branch is cut from the post-merge state).
- **IX**: spec/plan/tasks on `master`.
- **X**: documented before any edit.
- **IV**: no ADR — no architectural decision taken; the sandbox-conflict ADR/amendment
  is explicitly out of scope (human governance call).
- **XIII/XIV/XVI/XVII**: as above; retry budget 2 per gate with XV triage.
