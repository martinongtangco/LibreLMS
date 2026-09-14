# Quickstart: Continuous Integration

**Branch**: `story/053-add-ci-pipeline` | **Date**: 2026-09-14

## What this adds

1. `Directory.Build.props` (repo root) — shared build settings:
   `Nullable`/`ImplicitUsings` defaults, **NU1903 promoted to a build
   error**, `System.Security.Cryptography.Xml` pinned to 9.0.20 (fixes the
   current vulnerable-transitive warning).
2. `.github/workflows/ci.yml` — one job on `push` + `pull_request`:
   MSSQL + Valkey service containers → restore → build → ArchitectureTests
   → Host start (migrations + seeders, 302 readiness) → filler-clean → the
   five unit test projects → Playwright.

## Verify locally (the item's gate)

Run the workflow's commands in order (local equivalents in parentheses for
the container-bound steps):

```sh
dotnet restore LibreLms.slnx
dotnet build LibreLms.slnx --no-restore          # expect: 0 errors, 0 NU1903
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-build
# Host start + 302 poll  ==  in-container restart + in-container probe
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --no-build
dotnet test tests/Enrollment.Tests/Enrollment.Tests.csproj --no-build
dotnet test tests/Host.Tests/Host.Tests.csproj --no-build
dotnet test tests/Management.Tests/Management.Tests.csproj --no-build
dotnet test tests/Scorm.Tests/Scorm.Tests.csproj --no-build
# filler-clean (same sqlcmd SQL the local workflow uses)
# Playwright  ==  in-container `CI=1 PLAYWRIGHT_BROWSERS_PATH=/ms-playwright npx playwright test`
```

Unit-test env: `ConnectionStrings__Sql` + `ConnectionStrings__Valkey`
sourced in the same shell (`source .../run-env.sh && set +H`).

## Expected results

- build: `Build succeeded. 0 Error(s)` and **no NU1903 lines** (baseline:
  5+ per affected project).
- units: Arch 14, Catalog 32, Enrollment 42, Host 9, Management 55, Scorm 18.
- Playwright: 177 passed + 1 documented skip.

## Notes

- The workflow's MSSQL SA password is a throwaway CI fixture (documented in
  the file), not a real credential.
- No remote CI is triggered or observed from this sandbox (Principle V);
  the local command sequence IS the verification (ADR 0011).
