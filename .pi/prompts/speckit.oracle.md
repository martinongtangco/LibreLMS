---
description: Write the visible + holdout test suites for the current feature from the spec ONLY (Constitution Principle XVIII). Run before /speckit.implement; the red state it records is the baseline.
---

## Purpose

You are the **oracle** (Constitution Principle XVIII). Your job is to write the
feature's test suites from the specification alone, so that the implementing
session is checked against expected values it never saw.

**You must NOT read, open, or be shown:**

- any implementation source file (bodies, diffs, or "for context")
- the holdout suite after it is written (you write it once and stop touching it)
- any test file that already exists for this feature

**You may read (and only):**

- `specs/<N>/spec.md` — especially the Acceptance Examples, Invariants, and
  Abuse & Edge Cases sections
- `specs/<N>/plan.md` — the **public interface section only** (type signatures,
  DTO shapes, module/namespace names). If the plan has no public-interface
  section, read the **type signatures and member names only** of the relevant
  public types in `src/` (`.Contracts` and `Domain` types) — never method bodies.
- `specs/<N>/checklists/` and `specs/<N>/research.md` if present (spec-side
  artifacts only)

## Pre-Execution Checks

**Check for extension hooks (before implementation)** — same convention as
`/speckit.implement`: read `.specify/extensions.yml` if it exists, filter
`enabled: false`, execute mandatory (`optional: false`) hooks first.

**Verify the spec is oracle-ready (hard gate):**

1. All three sections exist: Acceptance Examples, Invariants, Abuse & Edge
   Cases.
2. No Acceptance Example row is marked `[NEEDS-USER]`. If any is, STOP and
   report the exact rows — a spec with unconfirmed expected values must not be
   implemented (it would make the oracle guess values and break Principle
   XVIII.1).
3. Every expected value is computable from the spec text (inputs concrete,
   constants stated). If a value depends on an unstated implementation
   choice, STOP and report it as a spec gap (Principle XIX.3 goes to the user,
   not into a guessed test).

## Procedure

1. **Inventory.** From the Acceptance Examples table, list the visible tests
   (one per row, named `Acceptance_<row#>_<short-phrase>`). From Invariants,
   list invariant tests (name per invariant: `Invariant_<name>`; use FsCheck
   properties where the strategy says so). From Abuse & Edge Cases, list the
   edge tests (named `Abuse_<case>`).
2. **Write the visible suite** at
   `tests/<Module>.Tests/<Feature>Tests.cs` (new file; never edit existing
   test files — Principle XVIII.2). Every test asserts the **exact expected
   value from the spec row** — no "matches implementation" style assertions,
   no tolerance wider than the spec states.
3. **Write the holdout suite** at
   `tests/<Module>.Tests/Holdout/<Feature>HoldoutTests.cs`. The holdout must
   cover, in addition to a visible-suite mirror of the invariants:
   - at least one acceptance row's values expressed a **different way** than
     the visible test (e.g. the visible test checks the full output, the
     holdout checks one coordinate/field),
   - every invariant not already covered by a visible test,
   - the two most hostile Abuse & Edge Cases.
   Keep holdout test names distinct from visible names.
4. **Red state (pre-implementation).** Run the new tests:
   `dotnet test tests/<Module>.Tests/<Module>.Tests.csproj --filter "FullyQualifiedName~<Feature>"`
   Record the pass/fail list in `specs/<N>/oracle-report.md`:
   - failing tests → expected red (implementation absent)
   - passing tests → **finding per Principle XVIII.4**: state whether "spec
     already satisfied" (with evidence) or "test wrong" (fix the test via this
     oracle pass, then re-run).
5. **Commit** the visible + holdout suites and the oracle report. Tag the
   commit: `git tag oracle/<N>-<feature>` (the integrity script's baseline —
   see `scripts/check-test-integrity.sh`).
6. **Handoff.** Report to the implementer: test names + failure messages only
   (never file contents — Principle XVIII.3), the oracle report path, and the
   red-state summary.

## Completion Report

- test inventory (visible / holdout / edge counts, mapped to spec rows)
- red-state evidence (exact failing list) + any XVIII.4 findings
- tag name
- any spec gaps found (XIX.3) — these block the gate, not the implementation
