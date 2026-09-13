# Quickstart: spec 050 validation

## Prerequisites

- Docker services up (`docker compose up -d`; mssql healthy, valkey up)
- App running **inside the devcontainer** (compose DNS) — see the run log's
  environment notes; browsers staged at `/ms-playwright` in the container
- `ConnectionStrings__Sql` env var for the unit suites (host or container)

## Validation (in order)

1. **Red check (before the fix)** — with only the new unit test file present:
   ```
   dotnet test tests/Scorm.Tests --filter "FullyQualifiedName~ScormSessionOwnershipTests"
   ```
   Expect: the student-B tests FAIL (B currently succeeds on A's session).
   This is the red-verification.

2. **Unit (after the fix)**:
   ```
   dotnet test tests/Scorm.Tests
   ```
   Expect: all green (ownership + owner-path + not-found regression tests).

3. **Gate 1**:
   ```
   dotnet build LibreLms.slnx
   ```
   + app restarted in the devcontainer (`Now listening on:` + HTTP 302 probe).

4. **Gate 2**:
   ```
   dotnet test tests/ArchitectureTests
   dotnet test LibreLms.slnx
   # E2E in the devcontainer (CI=1 serial; see run log):
   docker exec sbxtestwspeckit-devcontainer-1 sh -c \
     "cd /workspace/tests/Playwright.Tests && CI=1 PLAYWRIGHT_BROWSERS_PATH=/ms-playwright npx playwright test"
   ```
   Expect: baseline suite green + new `20-scorm-session-authz` tests green
   (bob 403 ×4, unauthenticated 401 ×4, owner 200 path). Clean non-seeded
   filler courses from the DB before the E2E run (documented run procedure).

5. **Manual sanity (optional)**: as a logged-in learner, open
   `/Scorm/Launch/11111111-1111-1111-1111-111111111111` — the iframe loads and
   the shim's setValue/commit still work (owner flow intact); in a
   logged-out browser console, `fetch('/api/scorm/session/<any-guid>/getValue?element=cmi.core.lesson_status')`
   → 401.
