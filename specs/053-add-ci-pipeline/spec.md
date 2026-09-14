# Feature Specification: Continuous Integration

**Feature Branch**: `story/053-add-ci-pipeline`

**Created**: 2026-09-14

**Status**: Complete (2026-09-14)

**Input**: Hardening-loop handoff, item 5:

> The constitution mandates three verification gates (Principle XIII) and
> independent verification before merge (Principle XVI), but nothing enforces
> them mechanically — there is no `.github/` directory and no CI configuration
> of any kind. Every gate is currently self-reported by the agent that wrote
> the code, which Principle XVI itself identifies as the weakest form of
> evidence.
>
> Fix: add a GitHub Actions workflow that runs on push and pull request:
> restore, build the solution, run `tests/ArchitectureTests`, run the unit
> test projects, then stand up MSSQL and Valkey as service containers, start
> the Host, and run the Playwright suite. Fail the build on NU1903 so a
> known-vulnerable package cannot land silently. Add a `Directory.Build.props`
> to hold the shared warning and analyzer settings, since none exists today.

## Problem

Every gate in this repo (build, unit, E2E, independent verification) is run
manually by the agent doing the work. Nothing prevents a commit that does not
build, or that breaks a gate, from reaching master: the only enforcement is
the agent's discipline. The hardening loop (specs 049–052) has repeatedly
shown how much value the gates provide — and how easy it is for one to be
skipped or mis-reported.

Additionally, `dotnet build` currently emits **NU1903 warnings** (known high-
severity vulnerability in `System.Security.Cryptography.Xml 9.0.0`, pulled in
transitively by `Microsoft.EntityFrameworkCore.Design 10.0.0` →
`Microsoft.Build.Tasks.Core 17.14.28`) that are waved through as warnings. A
build that warns about a vulnerable package and still succeeds is a green
light it should not give.

## Scope

1. **`.github/workflows/ci.yml`** — one workflow, runs on `push` and
   `pull_request`:
   - job-level **service containers**: MSSQL (`mcr.microsoft.com/mssql/
     server:2022-latest`) and Valkey (`valkey/valkey:8`), both with health
     checks; the job's env provides `ConnectionStrings__Sql` and
     `ConnectionStrings__Valkey` (the same keys the code and test projects
     already read — spec 051 settled the key on `Sql`).
   - steps, in order:
     1. checkout + setup .NET 10 + setup Node 22
     2. `dotnet restore LibreLms.slnx`
     3. `dotnet build LibreLms.slnx --no-restore`
     4. `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-build`
     5. start the Host in the background (`dotnet run --project src/Host
        --urls http://localhost:5000`) and wait for readiness (HTTP 302
        probe, bounded retry). Starting the Host is what runs the EF
        migrations and the seeders, so every later step sees a migrated,
        seeded database — the same state the local development workflow
        provides by keeping the app running.
     6. the unit test projects, one `dotnet test <project> --no-build` each:
        `Catalog.Tests`, `Enrollment.Tests`, `Host.Tests`,
        `Management.Tests`, `Scorm.Tests`
     7. Playwright: `npx playwright install chromium --with-deps`, then
        `npx playwright test` (CI=1) in `tests/Playwright.Tests`
   - any step failing fails the build (default Actions behavior).
2. **Fail the build on NU1903**: `Directory.Build.props` sets
   `<WarningsAsErrors>NU1903</WarningsAsErrors>` so a known-vulnerable
   package can no longer land silently.
3. **Bump the vulnerable package**: `System.Security.Cryptography.Xml`
   9.0.0 → **9.0.20** (latest 9.0-line stable as of 2026-09-14; the 8
   advisories require at most 9.0.18 — verified against the OSV advisory
   database; no stable 10.x exists yet — 10.0.x/11.x on NuGet are
   pre-releases/RCs). Added as a
   shared `<PackageReference>` in `Directory.Build.props`, which pins the
   transitive version for the whole graph. This makes the NU1903 error
   promotion land with a clean build in the same change (the handoff allows
   either bumping in this spec or deferring the promotion — bumping is the
   complete option).
4. **`Directory.Build.props`** at the repo root: shared build settings —
   the NU1903 error promotion, the package pin, and explicit
   `Nullable`/`ImplicitUsings` defaults (already set per project; centralizing
   them is the point of the file). It must not change build outcomes beyond
   the intended error promotion: the full build stays 0 errors before and
   after.

## Gate definition for this item (handoff hazard)

Principle V forbids arbitrary outbound network from the sandbox, so this work
**cannot** be verified by watching GitHub Actions run it, and must not try to
trigger or observe a remote run (Blocking under Principle XV on first
occurrence, not retryable). Gate 2 for this item means: **every command the
workflow invokes passes locally, in the workflow's order**. The two steps
that require the container environment (starting the Host, Playwright) are
verified with their established local equivalents: the in-container app
restart + 302 probe, and the in-container Playwright run. This mapping is
recorded in the run log as the local-evidence trail.

## Out of scope

- Actually running the workflow on GitHub (no outbound network; no remote
  observation or triggering).
- Branch protection rules, required status checks, code owner review
  configuration (GitHub-side settings, not repo files).
- Dependabot / Renovate configuration.
- Caching, matrix builds, parallel test sharding — one job, one machine,
  serial, matching the existing local workflow.
- Changing what the gates test (the test suites are as-is from specs
  049–052).

## Acceptance criteria

1. `.github/workflows/ci.yml` exists, is valid YAML (parses; every `run`
   command is a real command from this repo's toolchain), triggers on push
   and pull request, and uses job-level MSSQL + Valkey services with health
   checks.
2. Every command the workflow invokes passes locally in the workflow's order
   (documented local equivalents for the container-bound steps), ending with
   a full Playwright pass.
3. `Directory.Build.props` exists, promotes NU1903 to an error, and pins
   `System.Security.Cryptography.Xml` 9.0.11.
4. `dotnet build LibreLms.slnx` completes with **0 NU1903 warnings** and
   0 errors (before and after the props file; the only warning-set change is
   the NU1903 removal).
5. The unit test projects and ArchitectureTests pass with the build produced
   by the workflow's exact build command (`--no-build` test runs).
6. Independent verification (Constitution XVI) green before merge, as with
   every item in the loop.
