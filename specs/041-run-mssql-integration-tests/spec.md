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

## Defects Discovered While Enabling (all in the test file, all latent)

The tests have never executed (skipped since they were written in spec 019), so
four latent defects surfaced:

1. **Raw-ADO connections never opened** — `EnsureStoredProceduresExist` built
   `new SqlCommand(sql, new SqlConnection(cs))` without `Open()` (the EF context's
   connection is a different object) → `InvalidOperationException: ExecuteNonQuery
   requires an open and available Connection`.
2. **Invalid T-SQL batch** — the stored procedure body was embedded in
   `EXEC('CREATE PROCEDURE ...')`; the first unescaped single quote in the body
   (`t.name = 'Courses'`) terminates the outer string → `Incorrect syntax near
   'Courses'`.
3. **Stale SP + removed feature** — the helper dropped and recreated the
   `BrowseCourses` SP with spec 019's four-parameter FTS-based definition, but the
   SP is now owned by Host migrations: `20260805020000` removed full-text search
   (devcontainer MSSQL image does not support fulltext indexes) and
   `20260822123232` extended the SP to six parameters (sort) returning
   `OrganizationId`. The service (`CourseCatalogService.BrowseAsync`) calls the SP
   with six parameters and maps six columns, so the stale four-parameter SP would
   break every call. The two FTS tests (`FTS_index_exists_after_migration`,
   `Stored_procedure_fallback_to_like_when_fts_unavailable`) assert/manipulate a
   fulltext index the app no longer creates — obsolete.
4. **Unique-index collision in seed data** — `Courses` carries a unique index on
   `(Title, OrganizationId)` (`IX_Courses_Title_OrganizationId`), added after spec
   019's tests were written. Multiple tests reuse the same titles with the same
   organization, and cleanup deleted by category only, so seeding violated the
   unique index (cross-test and prior-run leftovers).

## Fix (test-only — no application code changes)

1. Remove the 12 `Skip = "Requires MSSQL"` attributes in
   `tests/Catalog.Tests/CourseCatalogSearchTests.cs` (plain `[Fact]`).
2. Delete `EnsureStoredProceduresExist` (broken SQL, stale SP shadowing the
   migration-owned one) and the two obsolete FTS tests.
3. Follow the established spec-032 pattern (`BrowseCoursesSortTests`):
   `MigrationsAssembly(Host)` + `Database.Migrate()` so the test context is
   self-contained (applies pending migrations, no-op when the app already did).
4. Make seeding unique-index-safe: one `TestOrgId` constant, and cleanup in
   `SeedTestCoursesAsync` deletes by title within the test org (plus the target
   category) so cross-test and prior-run leftovers can't collide with
   `IX_Courses_Title_OrganizationId`; the two directly-inserted rows
   ("Other Course", "Python for Data Science") get the same leftover cleanup.
5. Keep the class doc comment stating the tests require the sibling `mssql`
   container and the same database the Host uses. If MSSQL is absent the tests
   now fail loudly (connection error) instead of hiding as skips — correct for an
   environment where the service is guaranteed.

## Verification

- `dotnet test LibreLms.slnx` → Total: 84, Skipped: 0, Failed: 0
  (74 existing unit tests + 10 previously-skipped integration tests; the 2
  obsolete FTS tests are deleted, not skipped).
- Playwright E2E suite green against the running app (Principle XIII gate 2),
  re-run after merge (gate 3).
- `git diff --stat master...HEAD` touches only
  `tests/Catalog.Tests/CourseCatalogSearchTests.cs` (plus this spec's own
  bookkeeping).
