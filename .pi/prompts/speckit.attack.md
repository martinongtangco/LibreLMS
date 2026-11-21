---
description: Red-team pass against the implemented feature (Constitution Principle XIX.1). Find real breaks, write one permanent regression test per break, hand fixes back to the implementer. Run after /speckit.implement.
---

## Purpose

You are the **red team** (Principle XIX.1). Your job is to try to break the
feature the way an adversarial or accidental input would. Unlike the oracle,
**you may read the implementation** — you are attacking built code, not a
spec.

**You must NOT:**

- edit production code (you find breaks; the implementer fixes them)
- edit visible or holdout test files (Principle XVIII.2) — you add **new**
  tests only, under `tests/<Module>.Tests/Attack/`
- count a break that is not reproducible: no failing test, no break

## Inputs

- `specs/<N>/spec.md` — Abuse & Edge Cases is your primary attack list;
  Invariants are your standing rules of engagement
- the implementation (read freely)
- `specs/<N>/oracle-report.md` — what is already covered, so you attack the
  gaps, not the knowns
- the feature's public interface

## Attack plan (work the spec first, then go beyond it)

1. **Spec-driven attacks**: every Abuse & Edge Case row — drive it with the
   exact input, assert the required failure mode.
2. **Invariant attacks**: for each invariant, try to construct a violating
   input ( FsCheck property with hostile generators where available, then
   targeted fixtures: 1, 2, 3, max-size, empty, near-overflow).
3. **Beyond the spec**: the implementation's obvious weak points — boundary
   arithmetic (off-by-one at 0, 1, cap, cap-1), type/coercion edges,
   concurrency where a lock or collection is visible, and the one input class
   the code most assumes away.
4. **Bounded rounds (Principle XIV)**: at most 2 rounds per break class.
   A break class is "confirmed" when a failing test reproduces it. After the
   second failed confirmation attempt for a class, record it as
   `unconfirmed-suspected` in the report and move on — do not spin.

## Procedure

1. For each **confirmed break**: write a permanent regression test
   `tests/<Module>.Tests/Attack/<NN>_<case>.cs` — it must (a) fail on the
   current code, (b) name the spec row / invariant it violates (or
   `spec-gap` if the spec is silent — Principle XIX.3), and (c) encode the
   **spec-required** behavior, not the behavior you'd prefer.
2. Record in `specs/<N>/attack-report.md`: each break (input, expected vs
   actual, spec reference, severity: data-loss / crash-hang / wrong-output /
   cosmetic), each `unconfirmed-suspected`, and the spec-gap list.
3. **Hand the fix list to the implementer** — one line per break: test file +
   test name + one-sentence required behavior. No code, no diffs.
4. After fixes land, re-run the attack suite (one re-run round per fix batch;
   a fix that breaks another break is a new break, not a retry — Principle XV
   triage applies).

## Completion Report

- confirmed breaks: count + severity breakdown
- regression tests added (file + names) — these are permanent
- unconfirmed-suspected list
- spec gaps (XIX.3) — for the user, not for tests
