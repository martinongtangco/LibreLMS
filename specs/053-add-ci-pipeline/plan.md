# Implementation Plan: Continuous Integration

**Branch**: `story/053-add-ci-pipeline` | **Date**: 2026-09-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/053-add-ci-pipeline/spec.md`

**ADR**: [docs/adr/0011](../../../docs/adr/0011-ci-single-job-services-nu1903-error.md) —
one GitHub Actions job with job-level MSSQL+Valkey services; Host starts
before the unit steps; NU1903 promoted to a build error with the vulnerable
package bumped to 9.0.11 (written BEFORE code, per Principle IV).

## Summary

Add `.github/workflows/ci.yml` (push + pull_request): restore → build →
ArchitectureTests → Host start (migrate + seed, 302 readiness) → the five
unit test projects → Playwright, with `mssql` and `valkey` job-level service
containers and the `ConnectionStrings__Sql` / `ConnectionStrings__Valkey`
env keys the code already reads. Add `Directory.Build.props` (NU1903 →
error, `System.Security.Cryptography.Xml` pinned to 9.0.11, centralized
`Nullable`/`ImplicitUsings`). Gate 2 for this item: every workflow command
passes locally in workflow order (container-bound steps via their
established local equivalents) — no remote CI observation (Principle V).

## Technical Context

**Language/Version**: C# / .NET 10 (build), YAML (workflow), Node 22
(Playwright)
**Primary Dependencies**: none new in code; `System.Security.Cryptography.Xml`
9.0.0 → 9.0.11 (transitive pin, design-time graph)
**Storage**: MSSQL service container (2022), Valkey service container (8) —
CI-side only; repo code untouched
**Testing**: the existing suites (170 units + 177 E2E + 1 skip) are the
gate content — unchanged
**Constraints**: build must end at 0 errors / 0 NU1903; no behavior change
to app or tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I/II**: one workflow file, one props file, the exact local commands —
  no new abstractions, no new services in the app. Explainable: "CI runs
  the same gate stack the local workflow runs." ✅
- **III**: no module boundary touched; CI + build config only. ✅
- **IV**: ADR 0011 written and committed with this plan (job shape, DB
  provisioning order, NU1903 promotion + version choice). ✅
- **XIII/XV/XVI**: red-verify = the current build warns NU1903 (evidence in
  research.md) and no workflow exists; after the change, build has 0 NU1903
  and every workflow command passes locally in order; independent
  verification re-runs the local command sequence before merge. ✅
- **V (hazard, handoff)**: no remote CI trigger/observation anywhere in the
  plan; gate 2 is the local command sequence. ✅

## Phase 0 — Research

See [research.md](research.md): vulnerable-package provenance and the fixed
version; service image + health-check choices; the exact local command
sequence (the workflow's steps) and its local equivalents; NU1903 baseline
count.

## Phase 1 — Design

1. `Directory.Build.props` (root):
   - `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`
     (already universal per-project; centralized)
   - `<WarningsAsErrors>NU1903</WarningsAsErrors>`
   - `<PackageReference Include="System.Security.Cryptography.Xml"
     Version="9.0.11" />`
   - Verify: build 0 errors, 0 NU1903 (was 5+ per affected project).
2. `.github/workflows/ci.yml` per ADR 0011 (single job; services mssql +
   valkey with health checks; job env `ConnectionStrings__Sql` /
   `ConnectionStrings__Valkey` / `ASPNETCORE_ENVIRONMENT=Development`;
   steps restore → build → ArchTests → Host-start+302-poll → 5 unit
   projects → Playwright install+run).
3. Validate the YAML (parser) and walk every `run` command against a local
   pass.

## Phase 2 — Tasks

See [tasks.md](tasks.md).

## Local evidence trail (gate 2 for this item)

- Baseline (pre-fix): `dotnet build LibreLms.slnx` emits NU1903 warnings
  (count in research.md); no `.github/` directory.
- Post-fix: build 0 errors / 0 NU1903; then the workflow's command sequence
  executed locally in order, ending with the full Playwright pass
  (container-bound steps via documented equivalents).
- Independent verification (XVI) re-runs the same sequence from a clean
  worktree before merge.
