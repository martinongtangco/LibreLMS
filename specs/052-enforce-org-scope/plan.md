# Implementation Plan: Organization Scope Enforcement

**Branch**: `story/052-enforce-org-scope` | **Date**: 2026-09-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/052-enforce-org-scope/spec.md`

**ADR**: [docs/adr/0010](../../../docs/adr/0010-org-scope-enforced-in-management-services.md) —
enforcement lives in the Management application services via a required
`OrgScope` parameter (written BEFORE code, per the handoff + Principle X step 4).

## Summary

Every Management admin service method that lists or mutates data gains a
required `OrgScope` parameter (SuperUser = system-wide; OrgAdmin = subtree).
List reads are filtered to the subtree; single-target operations refuse
out-of-scope targets (forbidden result → JSON 403 at the Host). The Host
(endpoints + 14 admin pages) builds the `OrgScope` from the existing auth
claims and passes it in. The two SP-backed paged lists gain
`@RootOrgId UNIQUEIDENTIFIER = NULL` via migration. New unit project
`tests/Management.Tests`; E2E cross-org denial block in `08-rbac.spec.ts`.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: none new
**Storage**: MSSQL — two SPs re-created with the scope param (idempotent
DROP+CREATE house pattern); Valkey untouched
**Testing**: new `tests/Management.Tests` (real MSSQL, house pattern) +
`08-rbac.spec.ts` E2E additions + full gate 2
**Constraints**: 172-passed + 1-skip E2E baseline and unit suites stay green
(except the documented pre-existing Enrollment failure — see below)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I/II**: one `OrgScope` value, one shared subtree helper, per-method checks
  next to the queries. Explainable: "admin services refuse to act outside the
  caller's org subtree; the parameter is required by the signature, so no
  surface can forget it." ✅
- **III**: `OrgScope` + subtree helper live in the Management module
  (`Management.Contracts` for the record — it's a cross-module value the Host
  constructs and Management consumes); `IUserProvisioning` /
  `IEnrollmentAdmin` (Enrollment.Contracts) gain the `rootOrgId` param on the
  paged list methods — a contract change with all call sites in the Host/
  Management (verified at task time). ✅
- **IV**: ADR 0010 written and committed with this plan (the design decision
  the handoff mandates). ✅
- **XIII**: red-verify (unit + E2E against the unscoped build) before the fix
  lands; gates with evidence. ✅
- **XIV/XV**: 2-attempt ceiling; if the item genuinely cannot land, record the
  split-per-surface diagnosis and BLOCK (handoff: legitimate outcome). ✅

**Post-design re-check**: PASS.

## Project Structure

### Documentation

```text
specs/052-enforce-org-scope/
├── plan.md
├── research.md
└── quickstart.md
docs/adr/0010-org-scope-enforced-in-management-services.md   # committed with the plan
```

No `data-model.md` (no entity change — `OrgScope` is a value object, not a
row). `contracts/`: the `IUserProvisioning`/`IEnrollmentAdmin` signature
changes are recorded in tasks, not a separate contract doc (existing
contracts, additive parameter).

### Source (files touched)

```text
src/Modules/Management.Contracts/OrgScope.cs            # new: the scope value
src/Modules/Management/Application/OrgSubtree.cs        # new: shared BFS descendant helper
src/Modules/Management/Application/UserService.cs       # scope on all methods
src/Modules/Management/Application/OrganizationService.cs
src/Modules/Management/Application/CourseVisibilityService.cs
src/Modules/Management/Application/AdminEnrollmentService.cs
src/Modules/Management/Application/DashboardService.cs  # reuse OrgSubtree; verify already-scoped paths
src/Modules/Enrollment.Contracts/IUserProvisioning.cs   # + Guid? rootOrgId on ListPagedAsync
src/Modules/Enrollment.Contracts/IEnrollmentAdmin.cs    # + Guid? rootOrgId on List(Paged)Async
src/Modules/Enrollment/Application/UserProvisioningService.cs   # pass @RootOrgId
src/Modules/Enrollment/Application/EnrollmentAdminService.cs
src/Host/Migrations/Enrollment/<ts>_AddOrgScopeToAdminListProcedures.cs   # SP re-creation
src/Host/Program.cs                                       # scope plumbing + 403 mapping (5 groups)
src/Host/Pages/Admin/**/*.cshtml.cs                       # 14 pages: scope + subtree dropdowns
tests/Management.Tests/                                   # new project (+ slnx entry)
tests/Enrollment.Tests/AdminListLearnersTests.cs          # 8→9 column assertion (settled by the SP re-creation)
tests/Playwright.Tests/tests/08-rbac.spec.ts              # subtree-scope E2E block
```

## Design

### `OrgScope` (Management.Contracts)

```csharp
public sealed record OrgScope
{
    public bool IsSuperUser { get; }
    public Guid? OrganizationId { get; }
    public static OrgScope SuperUser { get; } = new(true, null);
    public static OrgScope ForOrgAdmin(Guid orgId) => new(false, orgId);
    // Learners never reach admin services (role gate at the boundary).
    private OrgScope(bool isSuperUser, Guid? organizationId) { ... }
}
```

Host construction (one helper in `AuthHelpers`):
`OrgScope FromClaims(ClaimsPrincipal)` → SuperUser role → `OrgScope.SuperUser`;
OrgAdmin + valid `OrganizationId` claim → `ForOrgAdmin(orgId)`; otherwise
fail closed (the role gate already blocks; treat as `ForOrgAdmin(Guid.Empty)`
= matches nothing).

### Subtree helper (Management module)

`OrgSubtree.DescendantIdsAsync(ManagementDbContext, Guid root)` — the BFS
moved out of `DashboardService` (which now calls it; behavior unchanged).
A service checks membership with `ids.Contains(targetOrgId)`.

### Per-service rules (the check is ONE pattern everywhere)

```text
// lists:  scope.IsSuperUser ? all : filter where OrganizationId in subtree(scope.OrganizationId)
// single target:
//   load target → targetOrg = target.OrganizationId
//   if (!scope.IsSuperUser && !subtree.Contains(targetOrg)) → forbidden result
// creates: targetOrg = the org in the request → same check before insert
```

- `UserService`: `ListAllAsync/ListAllPagedAsync` (filter / `@RootOrgId`),
  `GetByIdAsync`, `UpdateAsync` (current + new org), `DeleteAsync` → checked;
  `CreateAsync` (requested `organizationId`) → checked. Existing
  `ListByOrgScopeAsync` becomes a thin wrapper (subtree, not single org —
  its callers, if any, are re-pointed).
- `OrganizationService`: `ListAllAsync` (subtree for OrgAdmin), `ListByParentAsync`
  (parent must be in subtree), `GetByIdAsync`, `UpdateAsync`, `DeleteAsync` /
  `CanDeleteAsync`, `DisableAsync`/`EnableAsync`, `GetChartTreeAsync`
  (OrgAdmin root = their org), `CreateAsync` (parentId in subtree; root-level
  create = SuperUser only).
- `CourseVisibilityService`: `GetAllCoursesAsync` (subtree filter),
  `GetVisibleCoursesAsync(orgId)` (org check), `SetVisibilityOverrideAsync`
  (org check), `GetOverridesAsync` (org check), `DeleteCourseAsync`
  (course's org check).
- `AdminEnrollmentService`: list/paged (student's org in subtree — the SP
  `@RootOrgId`), `EnrollAsync`/`BulkEnrollAsync` (each student's org in
  subtree — out-of-subtree students rejected with a clean error, not an
  exception), `CancelEnrollmentAsync` (enrolled student's org check).
- `DashboardService`: `GetOrgMetricsAsync` already subtree-correct (BFS →
  `OrgSubtree`); `GetSystemMetricsAsync` stays SuperUser-only (endpoint
  routes by role); `GetRecentActivityAsync` — scope to subtree for OrgAdmin
  if it reads org-wide rows (verify at task time; likely already fine).

Forbidden results: each service result type gains the same
`Forbidden`/`CreateForbidden()` shape spec 050 established; the Host maps it
to `Results.Json(..., statusCode: 403)` (never `Forbid()`).

### SP re-creation (one migration)

`AdminListLearners` / `AdminListEnrollments` re-created idempotently with
`@RootOrgId UNIQUEIDENTIFIER = NULL`; a `#Subtree` temp table (recursive CTE,
`IsDeleted` excluded) is populated when the param is non-NULL; both the row
SELECT and the COUNT SELECT get `AND (@RootOrgId IS NULL OR
s.OrganizationId IN (SELECT Id FROM #Subtree))`. The learner SP keeps its 9
columns — the re-creation settles the pre-existing
`AdminListLearnersTests` 8-vs-9 assertion (test updated to 9 in the same
commit; documented in the item 1 findings).

### E2E (08-rbac.spec.ts — new describe block)

Setup (SuperUser context, via the API): create a child org `OrgA` under Root,
create learner `orga-learner@...` in `OrgA`, create OrgAdmin `orga-admin@...`
in `OrgA` (via `POST /api/users`), enroll the learner in a seeded course.
Assertions (orga-admin context):
1. `GET /api/users` → contains orga-learner, does NOT contain alice (Root).
   `GET /api/users/{alice}` → 403. `PUT` alice's role/org → 403.
2. `GET /api/organizations` → no org outside the subtree visible beyond it;
   `GET /api/organizations/{rootOther}` → 403.
3. `PUT /api/admin/courses/{seeded}/visibility?organizationId=<outside>` → 403;
   same with `orgA` → 200 (in-scope write works).
4. `GET /api/admin/enrollments` → orga-learner's enrollment present, alice's
   absent; `POST /api/admin/enrollments` enrolling alice → 403.
Teardown (SuperUser): delete the two users + the org (soft delete OK).
SuperUser regression: existing "SuperUser Full Access" tests stay green;
Learner denial tests stay green.

## Testing Strategy

- **Unit (tests/Management.Tests, red-verified)**: per-service — subtree
  list filtering, out-of-scope single-target refusal, SuperUser system-wide,
  org create/update/delete scoping. Sequence: add `OrgScope` params + checks
  in two steps so the red is real (params first, checks second) — same
  pattern as spec 050.
- **E2E**: the 08-rbac block above (red-verified against the pre-fix build:
  out-of-subtree reads return 200 pre-fix).
- **Regression guard**: full gate 2 (units + full Playwright 172+1;
  filler-clean after the last unit run).
