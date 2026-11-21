---
description: Mutation gate on the changed code (Constitution Principle XIX). Run Stryker.NET --since on the diff, report surviving mutants, route each to the oracle (test) or the user (spec gap). Run after /speckit.implement.
---

## Purpose

You run the **mutation gate** (Principle XIX.2): prove the feature's tests
would catch *wrong* code, not just right code. A surviving mutant is a
hypothesis "the spec's expected values don't pin down this behavior" — it is
closed by a new test (oracle) or a spec decision (user), never by excluding
code or moving the threshold.

## Procedure

1. **Scope.** `git diff master...HEAD --name-only -- src/` → the changed
   source files. If empty: report "no src diff — mutation gate not
   applicable" and stop. Map each file to its module's test project
   (`src/Modules/<M>/` → `tests/<M>.Tests/`).
2. **Environment (Principle XVII / XIX.4).** For each test project that
   touches the database: create a throwaway database (e.g.
   `CREATE DATABASE Mutants_<N>_<run>`), point `ConnectionStrings__Sql` at it,
   ensure this run is the only writer, and DROP it afterwards. For pure
   services (no DB in the test path), record "disposable DB: not required".
   Never run mutants against the shared development database.
3. **Baseline check first:** run the test project once, unmutated
   (`dotnet test tests/<M>.Tests/...`). Red baseline → stop with the failure
   list (a mutation run on a red baseline proves nothing; `--break-on-initial-test-failure`
   is the tool-side guard if you prefer one command).
4. **Mutate the diff.** From the **test project directory**:
   ```
   dotnet stryker --since:master --test-case-filter "FullyQualifiedName~<M>" --break-on-initial-test-failure
   ```
   (verified CLI surface: `--since[:<committish>]`, `--test-case-filter`,
   `--threshold-high/--threshold-low`, `--break-on-initial-test-failure`;
   everything else — reporters, `since.target`, `since.ignore-changes-in` —
   lives in `stryker-config.json`, which must NOT contain
   `since.ignore-changes-in` entries that exclude changed production code:
   that is the "shrunken surface" violation, XIX.2.)
   For a PR-style base, use `--since:<merge-base>` instead of `master`.
5. **Report** to `specs/<N>/mutation-report.md`:
   - overall + per-file mutation score (the run's raw numbers — do not round
     up),
   - **surviving mutant list**: file, span, mutation type, the tests that
     ran over it (from the HTML/console report),
   - threshold verdict: `--threshold-low 80 --threshold-high 100` are the
     starting gate for a first-known-baseline spec; the spec's own Success
   Criteria section may raise them (never lower them mid-run — XIX.2).
6. **Route every survivor** (XIX.2 / XIX.3):
   - spec pins the expected behavior → **oracle task**: a test from the spec
     that kills it;
   - spec is silent → **spec gap** for the user (quote the mutant + the
     behavior it exposes); do not write a test that encodes the
     implementation's accident.
7. **Gate verdict line** (for the completion claim, XIX.1):
   `MUTATION GATE: <PASS|FAIL> — score <X>%, <N> survivors, <M> spec-gaps,
   threshold low=80 high=100, since=<target>`.

## Gate self-test (XIX.5) — required when this gate's configuration is new
or materially changed

1. Scratch copy: `git worktree add /tmp/gate-selftest HEAD` (or a temp clone
   of the branch).
2. Plant exactly three realistic bugs in the copy's changed code:
   (a) an off-by-one in a loop/bound, (b) a wrong operator (`<=` → `<` or
   `&&` → `||`), (c) a missing validation (drop a null/empty/range guard).
3. Run the same `dotnet stryker --since` command against the copy.
4. Verdict: each planted bug must be caught by **the unit test suite**
   (the mutant is killed) — a bug that the suite misses is a real finding
   about the suite, record it; a bug that Stryker never even mutates is a
   finding about the gate's scope, record it.
5. Revert (delete the worktree). Report `GATE SELF-TEST: <caught>/3` in the
   mutation report. A 0/3 or 1/3 gate is not a gate — do not report PASS.

## Completion Report

- gate verdict line (step 7)
- score, survivor count, spec-gap count
- routed tasks (oracle) / spec gaps (user)
- self-test result if the configuration changed
