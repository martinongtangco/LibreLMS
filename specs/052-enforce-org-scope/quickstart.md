# Quickstart: spec 052 validation

## Prerequisites

- Docker services up; app running in the devcontainer (compose env supplies
  `ConnectionStrings__Sql` — spec 051)
- `ConnectionStrings__Sql` (+ `ConnectionStrings__Valkey`) env for host unit
  runs (see the run log)

## Validation (in order)

1. **Red check — E2E (before the fix)**: run the new 08-rbac subtree block
   against the pre-fix build:
   ```
   docker exec sbxtestwspeckit-devcontainer-1 sh -c "cd /workspace/tests/Playwright.Tests && CI=1 PLAYWRIGHT_BROWSERS_PATH=/ms-playwright npx playwright test tests/08-rbac.spec.ts --reporter=line 2>&1 | tail -6"
   ```
   Expect: the cross-org assertions FAIL (out-of-subtree reads return 200 /
   lists contain foreign rows). Record evidence.
2. **Red check — unit**: after the `OrgScope` params exist but before the
   checks: `dotnet test tests/Management.Tests` → the out-of-scope tests fail.
   Then implement the checks → green.
3. **Gate 1**: `dotnet build LibreLms.slnx` (0 errors) + app restart in the
   devcontainer (`Now listening on:` + HTTP 302 probe).
4. **Gate 2**: ArchitectureTests, full `dotnet test LibreLms.slnx` (note:
   `AdminListLearnersTests` 8→9 assertion is corrected by the SP
   re-creation — it should now PASS, improving the baseline), filler-clean
   AFTER the last unit run, full Playwright serial (CI=1) — baseline
   172+1 plus the new 08-rbac tests.
5. **Manual sanity (optional)**: as a logged-in OrgAdmin (admin@example.com
   is Root — its subtree is everything, so use the test-created child admin
   or create one ad hoc): the admin pages' org dropdown shows only the
   subtree; an out-of-subtree user id in the URL → 403.

## Post-merge

Gate 3 per the standard procedure (rebuild, restart, re-run gate 2 on
master), mark tasks, commit F.
