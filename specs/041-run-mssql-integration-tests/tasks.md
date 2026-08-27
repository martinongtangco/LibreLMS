# Tasks: 041 — Run the Skipped MSSQL Integration Tests

**Branch**: `story/041-run-mssql-integration-tests`

## 1. Enable the tests

- [x] T001: Remove the 12 `Skip = "Requires MSSQL"` attributes from
  `tests/Catalog.Tests/CourseCatalogSearchTests.cs`

## 2. Verification (Principle XIII)

- [x] T002: `dotnet test LibreLms.slnx` → 86 total, 0 skipped, 0 failed
- [x] T003: Playwright E2E suite green against running app (pre-merge, gate 2)
- [x] T004: Commit on branch; merge to `master`; return to `master` (Principle XII)
- [x] T005: Post-merge regression — rebuild, restart app, re-run Playwright E2E (gate 3)
