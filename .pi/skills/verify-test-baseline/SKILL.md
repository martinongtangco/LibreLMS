---
name: verify-test-baseline
description: Establishes and records the verified test baseline (exact pass/fail/skip counts per project, cause of every known-red test, and the repo's real commit-message convention) before writing a handoff prompt or trusting a gate result. Use before implementation starts and before any handoff prompt for the autonomous SpecKit loop is written.
disable-model-invocation: true
metadata:
  origin: memory/handoff-prompt-baseline-hygiene
---

# Verify Test Baseline

## Why

A handoff prompt that states only an aggregate count ("170 passed, 1 documented skip")
forces the next agent to re-discover and re-triage every pre-existing failure itself,
burning retry budget (constitution Principle XIV caps gate attempts at 2). A handoff that
asserts a commit-message convention nobody actually checked sends the agent chasing a
style that doesn't exist in this repo's history. Both mistakes spend limited retries on
questions the handoff should have answered up front, and a wrong baseline can turn a
healthy run into a false BLOCKED.

## Steps

1. Run the sequential test gate — never `dotnet test LibreLms.slnx` directly:

   ```bash
   ../sequential-project-test-runner/scripts/run.sh
   ```

2. For every failing or skipped test, determine whether it is pre-existing on a clean
   checkout of the current base branch (i.e. not caused by uncommitted or in-progress
   changes). If there's any doubt, verify in a clean worktree rather than assuming.

3. Record, per project: passed / failed / skipped counts, and for every non-passing test
   its name and a one-line cause (e.g. "asserts 8 SP columns; spec 042's migration
   `20260829105050` re-created the SP with 9 — pre-existing, unrelated to this spec").

4. Run `git log -1 --format=%B` (and a few more recent commits if the shape is unclear)
   and describe the **actual** commit-message convention observed. Do not assert a
   convention (e.g. "always ends with two attribution lines") without having checked —
   describe what is really there, even if it contradicts session tooling defaults.

5. Output both as an explicit "Verified Baseline" block, suitable for pasting directly
   into a handoff prompt or gate report:

   ```
   ## Verified Baseline (as of <git-sha>)
   - Catalog.Tests: 42 passed, 0 failed, 0 skipped
   - Enrollment.Tests: 18 passed, 1 failed (pre-existing: <cause>), 0 skipped
   - ...
   Commit message convention observed: <describe what git log actually shows>
   ```

## When to run this

- Before writing any handoff prompt that drives `/speckit.implement` or the autonomous
  SpecKit loop.
- Before treating a `dotnet test` result as ground truth for a Done-when check.
