# Quickstart: spec 058 — Code Review Remediation

Validation/run guide for the four milestones. Prerequisites and command reference:
`CLAUDE.md` §2 (host-side block: compose up, env exports, sequential test runner, Playwright).
This run works **host-side** (declared per CLAUDE.md §5 open conflict); the authoritative gate
per milestone is **branch CI** (fresh DB) — local runs are supporting evidence.

## Prerequisites

```bash
# from the main repo root (owns .env + the long-lived mssql-data volume)
docker compose up -d          # mssql :1433, valkey :6380

# in the shell that runs tests/app (host-side exports — CLAUDE.md §2)
export ConnectionStrings__Sql="Server=localhost,1433;Database=LearningLms;User Id=sa;Password=$(grep MSSQL_SA_PASSWORD .env | cut -d= -f2-);TrustServerCertificate=True"
export ConnectionStrings__Valkey="localhost:6380"
export ASPNETCORE_ENVIRONMENT=Development
```

## M1 (P1) — dashboard stats wired to real attempt data

1. Build: `dotnet build LibreLms.slnx` → 0 errors.
2. Red→green proof (do this *before* wiring `DashboardService`):
   `dotnet test tests/Management.Tests/Management.Tests.csproj --filter FullyQualifiedName~DashboardServiceTests`
   → the org-metrics test fails with expected 0.4 / actual 0.0 (hardcoded-zero behavior).
   Then wire the service and re-run → green.
3. Run the app: `./scripts/restart-app.sh --background` → "Now listening on: http://localhost:5000",
   readiness `GET /` → 302.
4. E2E (the spec's US1 Independent Test):
   `cd tests/Playwright.Tests && npx playwright test tests/21-dashboard-real-stats.spec.ts`
   → all pass. What it proves:
   - new unique learner completes one scored SCORM session (87.5) →
     as **Learner** `GET /api/dashboard`: `completedCourseCount >= 1`, `averageScore == 87.5`;
   - as **OrgAdmin** (root org) `GET /api/dashboard`: `averageCompletionRate > 0`;
   - as **SuperUser** `GET /api/dashboard`: `averageCompletionRate > 0`;
   - US1-2: a freshly created empty org's OrgAdmin sees `averageCompletionRate == 0`
     (zero from real data, not an unwired field).
5. Full local gate: sequential unit runner green; full Playwright suite green (no regressions —
   `04-admin-dashboard.spec.ts`'s "renders a percentage" assertions still hold: seeded org with
   the new learner's attempt renders a real percentage).
6. Authoritative: `git push` → CI run SUCCESS (record run ID).

## M2 (P2) — Management unit tests

1. `dotnet test tests/Management.Tests/Management.Tests.csproj` → all green; test count grew by
   the 4 new files (≥ 4 new tests, SC-002). Expected new files:
   `OrganizationLookupTests`, `UserInfoLookupTests`, `OrgSubtreeTests`, `TreeLayoutServiceTests`
   (+ `DashboardServiceTests` already landed with M1).
2. Non-trivial cases to spot-check in the output:
   - `TreeLayoutServiceTests`: multi-level tree → depths 0/1/2, Y = depth × 130, distinct X;
   - `OrgSubtreeTests`: OrgAdmin subtree = org + multi-level descendants, sibling/ancestor excluded;
   - `OrganizationLookupTests`: deleted org excluded from children + ancestor chain stops.
3. Full local gate (sequential runner + Playwright) + branch CI SUCCESS.

## M3 (P3) — ADR 0014 + catch-block collapse

1. `docs/adr/0014-management-error-handling-convention.md` exists (context → decision →
   consequences, one page).
2. Build 0 errors. In `src/Host/Program.cs`, the 4 cited `users` handlers (plus the uniform
   `orgs`/`adminCourses` handlers) have no multi-block catch: each is
   `catch (<same types as before> ex) { return ManagementErrors.Translate(ex); }`.
   `adminEnrollments` handlers are unchanged.
3. `dotnet test tests/Host.Tests/Host.Tests.csproj` → `ManagementErrorsTests` pins 403-JSON /
   404-no-body / 400-JSON / 400-JSON (argument).
4. No-observable-change proof (SC-003): full Playwright suite green — in particular
   `08-rbac.spec.ts` (401/403 per role for users/orgs/admin courses) and the admin-management
   specs (404/400 paths).
5. Full local gate + branch CI SUCCESS.

## M4 (P4) — housekeeping

1. `src/Host/Program.cs` startup: exactly four `Database.Migrate()` calls
   (`grep -c "Database.Migrate()"` on the startup block = 4).
2. `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj` → the 2 new assertions pass
   (Contracts-reverse-dependency × 4 modules, SharedKernel isolation).
   During development each was manually violated once (temp type → red → reverted) to prove the
   assertion enforces the rule.
3. Auth style: `grep -n "RequireAuthorization(new AuthorizeAttribute" src/Host/Program.cs`
   shows all four groups (`users`, `orgs`, `adminCourses`, `adminEnrollments`) on group-level
   roles; no per-handler `[Authorize(Roles = ...)]` remains in those four groups; `dashboard`
   group untouched.
4. No-access-regression proof (SC-004): `npx playwright test tests/08-rbac.spec.ts` green
   (anonymous → 401/302, wrong role → 403 JSON, per group) + full suite green.
5. App restart on the changed startup path: migrations idempotent, seeders no-op, `/` → 302.
6. Full local gate + branch CI SUCCESS.

## Done (branch level)

- `specs/HANDOFF-RUN-LOG.md` Item 10 complete with per-milestone commits + gate evidence
  (each entry states host-side vs in-container mode).
- All four milestones are separate commit sets on `story/058-code-review-remediation`, each
  referencing spec 058 (SC-005).
- Branch pushed; **not merged** — flagged for the Principle XVI independent-verification gate
  (fresh no-context subagent from a clean worktree + the reviewing session), then merged by a
  non-implementer; gate 3 (post-merge rebuild/restart/re-run) recorded at merge time.
