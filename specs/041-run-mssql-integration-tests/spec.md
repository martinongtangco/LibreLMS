# Spec: Run the Skipped MSSQL Integration Tests (Catalog Search)

**Feature Branch**: `story/041-run-mssql-integration-tests`

**Created**: 2026-08-27

**Status**: Implementing

**Input**: User asked to confirm that ALL tests are run. `dotnet test LibreLms.slnx`
reports 74 passed / 12 skipped / 0 failed. All 12 skips are
`Catalog.Tests.CourseCatalogSearchTests` (spec 019 integration + performance tests),
hard-skipped with `[Fact(Skip = "Requires MSSQL")]`.

## Root Cause

The skip attributes encode a local-Windows dev assumption (MSSQL optional,
`localhost:1433`). The project's actual dev environment (Constitution Principle V)
always provides MSSQL as a sibling container, and the devcontainer start is gated on
its healthcheck (`docker-compose.yml` → `depends_on: mssql: condition: service_healthy`).
The tests already resolve the connection string from `ConnectionStrings__Sql`
(devcontainer environment → `Server=mssql,1433;...`), so in the sandbox the skip is
stale: it silently hides 12 passing-capable integration tests from every run in the
environment this project actually uses.

## Fix (test-only — no application code changes)

1. Remove the 12 `Skip = "Requires MSSQL"` attributes in
   `tests/Catalog.Tests/CourseCatalogSearchTests.cs` (plain `[Fact]`).
2. Keep the class doc comment stating the tests require a running MSSQL instance
   (`docker compose up mssql`). If MSSQL is absent the tests now fail loudly
   (connection error) instead of hiding as skips — correct for an environment where
   the service is guaranteed.

## Verification

- `dotnet test LibreLms.slnx` → Total: 86, Skipped: 0, Failed: 0
  (74 existing unit tests + 12 previously-skipped integration tests).
- Playwright E2E suite green against the running app (Principle XIII gate 2),
  re-run after merge (gate 3).
- `git diff --stat master...HEAD` touches only
  `tests/Catalog.Tests/CourseCatalogSearchTests.cs`.
