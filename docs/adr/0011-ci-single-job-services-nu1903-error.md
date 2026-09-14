# ADR-0011: CI runs as one GitHub Actions job with service containers; NU1903 is a build error

**Status**: Accepted
**Date**: 2026-09-14
**Supersedes**: none

## Context

The constitution's gates (XIII, XVI) are currently self-reported by the agent
that wrote the code — the weakest evidence form Principle XVI itself
names. The fix is a GitHub Actions workflow (push + pull request) that
mechanically runs the local gate stack: restore, build, ArchitectureTests,
unit test projects, Host, Playwright.

Open decisions:

1. **Job shape** — one job vs. several (e.g. a fast build+unit job plus a
   separate e2e job).
2. **Database/cache for the unit tests** — the unit projects are not
   pure: they run against a real MSSQL (`ConnectionStrings__Sql`, spec
   041) and expect a migrated + seeded database (the seeders run at Host
   startup). Something must provide that before the unit steps.
3. **NU1903** — the build currently warns about a high-severity vulnerable
   package (`System.Security.Cryptography.Xml 9.0.0`, transitive via
   `Microsoft.EntityFrameworkCore.Design 10.0.0` → `Microsoft.Build.Tasks.Core
   17.14.28`). The spec requires the build to fail on NU1903.
4. **Where shared build settings live** — there is no `Directory.Build.props`
   today; every project repeats `Nullable`/`ImplicitUsings`.

Constraint: Principle V forbids outbound network from this sandbox, so the
workflow is verified locally (every command, in order) — it cannot be
watched running on GitHub. The design must therefore be boring and
reproducible: the same commands the local gate already runs.

## Decision

**One job, service containers at the job level.** `.github/workflows/ci.yml`
declares `mssql` (`mcr.microsoft.com/mssql/server:2022-latest`) and `valkey`
(`valkey/valkey:8`) as job `services:` with health checks, and exports
`ConnectionStrings__Sql` / `ConnectionStrings__Valkey` (the keys the code
already reads, spec 0051) as job env. Steps, in order:

1. checkout, setup-dotnet 10, setup-node 22
2. `dotnet restore LibreLms.slnx`
3. `dotnet build LibreLms.slnx --no-restore`
4. `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-build`
5. start the Host in the background (`dotnet run --project src/Host
   --urls http://localhost:5000`) and poll for HTTP 302 (bounded retries)
6. each unit project, `dotnet test <project> --no-build`:
   Catalog, Enrollment, Host, Management, Scorm
7. `npx playwright install chromium --with-deps`; `npx playwright test`
   (CI=1) in `tests/Playwright.Tests`

**Why one job**: the unit steps need the migrated+seeded DB, which only the
Host startup produces; the E2E step needs the Host running. A single job
makes the dependency explicit and linear — no artifact passing, no
"which job seeds the DB" puzzle. Runtime (~10–15 min on a runner) is
acceptable for this repo.

**Why the Host starts before the unit tests**: this is the one deliberate
reordering versus the handoff's literal step list (which names the services
after the unit tests). The unit projects require a live, migrated, seeded
MSSQL (they fail fast with "ConnectionStrings__Sql environment variable is
required" without env, and assert against seeded rows); in the local
development workflow the app is always running while units run. The CI job
reproduces exactly that: services → Host (migrate + seed) → units → E2E.

**NU1903 is an error, in `Directory.Build.props`**:
`<WarningsAsErrors>NU1903</WarningsAsErrors>`. A known-vulnerable package
must not land as a waved-through warning.

**The vulnerable package is bumped in the same change**:
`System.Security.Cryptography.Xml` 9.0.0 → **9.0.11** (latest 9.0-line
stable as of 2026-09-14; NuGet has no stable 10.x yet — 10.0.x/11.x are
pre-releases/RCs, not acceptable for a dependency pin). Added as a shared
`<PackageReference>` in `Directory.Build.props`, which pins the transitive
version for the whole graph (it is pulled by EFCore.Design, which is a
design-time asset — pinning at the root covers every project without
touching each csproj).

**`Directory.Build.props` holds**: the NU1903 promotion, the package pin,
and the already-universal `Nullable`/`ImplicitUsings` defaults, centralized
for the first time. It must change no build outcome beyond removing the
NU1903 warnings (verified: 0 errors before and after, warning set diff is
exactly the NU1903 lines).

**Verification stays local** (gate definition for this item): every command
the workflow invokes is run locally in the workflow's order; the two
container-bound steps use their established local equivalents (in-container
Host restart + 302 probe; in-container Playwright with `PLAYWRIGHT_BROWSERS_
PATH=/ms-playwright`). No remote triggering, no remote observation
(Principle V / XV).

**Rejected alternatives**

- *Separate build+unit and e2e jobs*: saves a few minutes of wall-clock by
  parallelizing, but duplicates the service-container setup, the Host
  startup, and the migrate+seed logic across two files — and the unit job
  still needs the DB, so nothing is actually avoided. Rejected for
  simplicity (Constitution II).
- *MSSQL via the `docker` action / docker-in-docker instead of `services:`*:
  `services:` is the first-class mechanism, gives health-check gating for
  free, and needs no privileged runner. Rejected.
- *Unit tests against an InMemory/EF test database instead of real MSSQL*:
  changes what the gates test (the SP-backed paths are the point — specs
  032/041/052); large rewrite, out of scope. Rejected.
- *Deferring the NU1903 error-promotion to a follow-up (allowed by the
  handoff)*: leaves the build green while shipping a known-vulnerable
  package — the exact failure mode the spec closes. Bumping to 9.0.11 is
  low-risk (design-time transitive, no API surface used directly) and lands
  the promotion with a clean build. Rejected.

## Consequences

**Positive**

- Master can no longer receive a commit whose build or gates were merely
  claimed: the workflow re-runs them mechanically on push/PR.
- A vulnerable package now fails the build instead of warning.
- One file defines the whole gate stack; the local workflow and CI are the
  same commands.

**Negative**

- The workflow file encodes assumptions (image tags, health-check commands,
  service ports) that can only be exercised on GitHub — verified locally as
  far as Principle V allows, and pinned to boring, current stable images.
- A single serial job makes PR feedback slower than a parallel design
  (~10–15 min).
- `Directory.Build.props` now exists: future per-project overrides of the
  centralized properties must remember it is inherited.

## Related

- ADR-0010 (service-level scope enforcement — the code this CI protects)
- spec 051 (connection-string key settled on `Sql` — the env keys the job
  exports)
- specs 041 (unit tests on real MSSQL), 032 (SP-backed paged lists)
