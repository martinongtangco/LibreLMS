# Plan: Serialize Enrollment.Tests on the Shared Database (spec 056)

**Branch**: `bug/056-serialize-enrollment-tests` | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

**Constitution version**: 1.9.0

## Summary

Add `tests/Enrollment.Tests/AssemblyInfo.cs` containing
`[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]`, mirroring
`tests/Catalog.Tests/AssemblyInfo.cs` (commit `f0ba9d6`). One new file, zero changes to
any test or assertion. This makes the interleaving that produced the CI failure
(`empty_search_is_no_filter`, `Expected 25, Actual 24`) structurally impossible for the
whole project, present and future.

## Technical Approach

- **Pattern source**: `tests/Catalog.Tests/AssemblyInfo.cs` — same attribute, same
  shape of explanatory comment (adapted: name the actual classes and the actual
  non-atomic per-row filler INSERTs, per research.md §1).
- **Why not per-class collections / runsettings / DB isolation**: research.md §2
  (house pattern established by the Catalog fix; one file; no new configuration
  surface; proportionate to a five-second cost).
- **Scorm.Tests**: explicitly unchanged, with the per-class evidence recorded in
  research.md §3. The standing condition for revisiting is documented there.

## Files

| File | Change |
|---|---|
| `tests/Enrollment.Tests/AssemblyInfo.cs` | **New** — assembly-level `CollectionBehavior(DisableTestParallelization = true)` + comment |
| `tests/Enrollment.Tests/Enrollment.Tests.csproj` | None — SDK-style projects compile every `.cs` under the directory; no csproj edit needed (same as Catalog.Tests) |
| `specs/056-*/spec.md`, `research.md`, `plan.md`, `tasks.md` | Status updates at gate milestones |

## Risks

- **Runtime cost**: Catalog.Tests went ~9s → ~14s with this fix. Enrollment.Tests is
  currently ~8s; expect roughly ~12–16s. Acceptable for gate determinism.
- **Masking future races**: serialization removes *this class of* race (cross-class
  interleaving). A race *within* one class would still exist — none are known; the
  suite's classes are self-contained within their marker prefixes.
- **XVII compliance**: local gate runs against the dev database are *supporting*
  evidence only. The authoritative run is CI (fresh per-run MSSQL/Valkey), on the
  branch and again on master.

## Verification Sequence (Principles XIII, XIV, XV, XVI, XVII)

1. **Gate 1 (local, supporting)**: `dotnet build LibreLms.slnx` clean; Host started and
   responding (HTTP 302 on `/`).
2. **Local unit gate ×3 (supporting)**: `run.sh` three consecutive full passes
   (197/197 each) — the flake is intermittent, so a single pass proves nothing; three
   is the minimum to make "no red in N runs" meaningful.
3. **Commit + push branch → CI (gate 2, authoritative)**: fresh-DB full run
   (unit + E2E). If CI is red, classify per XV before any retry (XIV caps at 2).
4. **XVI independent verification**: fresh context-free subagent re-runs build + unit
   gate + Playwright from a clean checkout of the branch; reports independently.
5. **Merge** to `master` with the conventional merge message; push.
6. **Gate 3 (post-merge)**: local rebuild/restart/re-run of the E2E suite, and CI on
   master green (authoritative).
7. Update spec status and `specs/HANDOFF-RUN-LOG.md`.

## Constitution Check

- **III** (module boundaries): untouched — test project only.
- **VIII** (branching): `bug/056-serialize-enrollment-tests` from `master`; merge after
  green gates.
- **IX** (plan on master): spec/plan/tasks authored on `master`.
- **X** (no ad-hoc fixes): documented here before any code edit.
- **XIII/XIV/XV** (gates, bounded retry, triage): sequence above; retry budget 2 per
  gate with classification before attempt 2.
- **XVI** (independent verification): step 4.
- **XVII** (disposable environment): CI is the proof; local runs labeled supporting.
