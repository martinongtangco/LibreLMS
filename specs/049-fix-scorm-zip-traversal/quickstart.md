# Quickstart: spec 049 validation

## Prerequisites

- Docker services up: `docker compose up -d` (mssql healthy, valkey up)
- `ConnectionStrings__Sql` env var set (devcontainer default) for the unit suites

## Validation (in order)

1. **Red check (before the fix)** — on the branch, with only the new test file
   present:
   ```
   dotnet test tests/Scorm.Tests --filter "FullyQualifiedName~ScormUploadTraversalTests"
   ```
   Expect: traversal tests FAIL (files written outside the content dir / upload
   "succeeds"). This is the red-verification required by the spec.

2. **Unit (after the fix)**:
   ```
   dotnet test tests/Scorm.Tests
   ```
   Expect: all green, including the 6 traversal/cap/regression tests.

3. **Gate 1**:
   ```
   dotnet build LibreLms.slnx
   ./scripts/restart-app.sh --background
   ```
   Expect: 0 errors + `Now listening on:` line.

4. **Gate 2**:
   ```
   dotnet test tests/ArchitectureTests
   dotnet test LibreLms.slnx
   cd tests/Playwright.Tests && npx playwright test
   ```
   Expect: baseline 170 passed + 1 documented skip (verify-email).

5. **Manual sanity (optional)**: upload a known-good SCORM zip via
   `POST /api/scorm/upload` as an admin and launch the course — the legitimate
   path is unchanged.
