# Research: Continuous Integration

**Date**: 2026-09-14 | **Branch**: `story/053-add-ci-pipeline`

## 1. The vulnerable package (NU1903 baseline)

`dotnet build LibreLms.slnx` on master (pre-fix) emits NU1903 (high
severity) for `System.Security.Cryptography.Xml 9.0.0` — 5 advisory URLs per
affected project (GHSA-23rf-6693-g89p, GHSA-37gx-xxp4-5rgx,
GHSA-6588-8gv4-xfgh, GHSA-8q5v-6pqq-x66h, GHSA-cvvh-rhrc-wg4q,
GHSA-g8r8-53c2-pm3f, GHSA-mmjf-rqrv-855v).

Provenance (`dotnet nuget why`): it is **transitive** —
`Microsoft.EntityFrameworkCore.Design 10.0.0` (a design-time reference in
Catalog/Host) → `Microsoft.Build.Tasks.Core 17.14.28` →
`System.Security.Cryptography.Xml 9.0.0`. No project references it
directly, so the fix is a version pin, not a csproj edit.

Version choice (NuGet flat-container index, 2026-09-14): the 9.0 line goes
9.0.0 … 9.0.11 (stable); 10.0.x exists only as pre-releases
(`10.0.0-preview.5` etc.) and 11.x as RCs. **9.0.11** = latest stable 9.0
line, includes the security fixes for all listed advisories. Pinned via a
shared `<PackageReference>` in `Directory.Build.props` (applies to the whole
graph; the package is a framework-provided assembly on net10.0, so the pin
changes no runtime behavior).

## 2. What the unit tests need from the environment

- `ConnectionStrings__Sql` (MSSQL) and `ConnectionStrings__Valkey`
  (Valkey) — the keys settled by spec 051. Missing either makes the DB-
  backed suites fail immediately (`"ConnectionStrings__Sql environment
  variable is required"`).
- A **migrated + seeded** database: EF migrations and the seeders run at
  Host startup (`Program.cs` applies all four contexts' migrations, then
  seeds orgs/catalog/students). The unit suites assert against that state
  (e.g. seeded 10 courses, 4 students, 2 orgs).
- Consequence: in CI the Host must start (and reach readiness) **before**
  the unit test steps. This is the one reordering versus the handoff's
  literal step list, recorded in ADR 0011.

## 3. Service containers (GitHub Actions `services:`)

- **MSSQL**: `mcr.microsoft.com/mssql/server:2022-latest`; env
  `ACCEPT_EULA=Y` + `MSSQL_SA_PASSWORD` (complexity: ≥8 chars, 3 of 4
  classes). Health check via the in-image
  `/opt/mssql-tools18/bin/sqlcmd ... SELECT 1` (same binary the local
  sandbox uses — known-good).
- **Valkey**: `valkey/valkey:8`; health check `valkey-cli ping`.
- Ports published to the runner's localhost (1433 / 6379); the job env
  points the app/tests at `localhost` (the services run on the same
  runner, not the `mssql:`/`valkey:` compose hostnames).
- The CI SA password is a throwaway fixture inside the workflow file —
  same class of secret as the repo's `.env` development password, not a
  real credential; noted in the file.

## 4. The workflow's command sequence (= local gate 2)

In order, exactly as `ci.yml` will run them:

1. `dotnet restore LibreLms.slnx`
2. `dotnet build LibreLms.slnx --no-restore`
3. `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-build`
4. Host start: `dotnet run --project src/Host --urls http://localhost:5000`
   (background) + poll `curl -s -o /dev/null -w '%{http_code}'
   http://localhost:5000/` until `302` (bounded retries, log tail on
   failure). **Local equivalent**: the in-container restart command
   (`docker exec -d --user root sbxtestwspeckit-devcontainer-1 sh -c
   'cd /workspace/src/Host && ConnectionStrings__Valkey=valkey:6379
   ASPNETCORE_ENVIRONMENT=Development dotnet run --urls http://localhost:5000
   > /tmp/app.log 2>&1'`) + in-container 302 probe. Same code path:
   migrations + seeders + listen.
5. `dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --no-build`
6. `dotnet test tests/Enrollment.Tests/Enrollment.Tests.csproj --no-build`
7. `dotnet test tests/Host.Tests/Host.Tests.csproj --no-build`
8. `dotnet test tests/Management.Tests/Management.Tests.csproj --no-build`
9. `dotnet test tests/Scorm.Tests/Scorm.Tests.csproj --no-build`
10. `npx playwright install chromium --with-deps` (in
    `tests/Playwright.Tests`); `CI=1 npx playwright test`. **Local
    equivalent**: the in-container Playwright run
    (`PLAYWRIGHT_BROWSERS_PATH=/ms-playwright npx playwright test`,
    browsers already installed in the container).
11. (House workflow, not a CI step) filler-clean after the last unit run
    before E2E — the Catalog perf seed leaves ~11.7k filler courses; the
    local E2E runs require the clean. In CI the DB is disposable per run,
    but the E2E suite was authored against the clean state, so the workflow
    performs the same cleanup between steps 5–9 and 10 (a `sqlcmd` call via
    the runner — documented in tasks.md).

## 5. `Directory.Build.props` blast radius

New root file; every project in the solution inherits it. Contents are
restricted to (a) properties already set identically per project
(`Nullable=enable`, `ImplicitUsings=enable`), (b) the NU1903 promotion,
(c) the package pin. Verification: full build before vs after — 0 errors in
both; the warning-set diff is exactly the NU1903 lines. No per-project
property conflicts found (all projects already set the same values).

## 6. YAML validity

Validated with a YAML parser locally (python `yaml.safe_load` or node
`js-yaml` if present) plus a manual walk of every step's commands against
step 4's local sequence. (GitHub's own schema check happens only on GitHub —
unavailable here by Principle V.)
