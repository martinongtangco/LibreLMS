# Feature Specification: Organization Scope Enforcement

**Feature Branch**: `story/052-enforce-org-scope`

**Created**: 2026-08-31

**Status**: Complete (2026-09-14)

**Input**: Hardening-loop handoff, item 4 (verified against the code on 2026-08-31):

The organization-scope authorization machinery exists, is registered in DI, and
is never called. `OrgScopeAuthorizationHandler`, `RequireOrgScopeRequirement`,
`OrgScopeExtensions.IsInSubtreeAsync` and `AuthHelpers.IsInOrgSubtree` have
zero call sites outside `src/Host/ManagementAuth/`. Every admin surface gates
on role alone, so an OrgAdmin has the same reach as a SuperUser across every
organization.

## User Scenarios

1. **OrgAdmin of a child organization** logs in and opens the admin surfaces
   (users, organizations, courses/visibility, enrollments, dashboard). They
   see and can modify **only** their own organization subtree — nothing from
   sibling or unrelated branches. *(Priority: P1)*
2. **SuperUser** uses the same surfaces and sees/modifies everything, exactly
   as today. *(Priority: P2)*
3. **Learner** is still denied every admin surface (regression guard).
   *(Priority: P3)*

## Confirmed unscoped surfaces (inventory, verified)

**Minimal API (Program.cs):**
- `GET /api/users` — `ListAllAsync` for everyone; the code comment says
  OrgAdmin "would need subtree filtering". `GET/POST/PUT/DELETE /api/users`
  likewise unscoped.
- `GET /api/organizations` (+ `/picker`, `/{id}`, POST/PUT/DELETE) —
  `ListAllAsync` for everyone; no parent/target org checks.
- `GET /api/admin/courses` — `GetAllCoursesAsync` for everyone.
  `PUT /api/admin/courses/{id}/visibility` — takes `organizationId` from the
  query and never checks it against the caller's org.
  `DELETE /api/admin/courses/{id}` — any OrgAdmin deletes any course.
- `GET/POST /api/admin/enrollments` (+ `/bulk`, DELETE) — unscoped.
- `GET /api/dashboard` — already subtree-correct (caller's org claim →
  `GetOrgMetricsAsync` sums descendants); kept as the reference pattern.

**Razor Pages (all 14 under `Pages/Admin/`, e.g. `Learners/Index.cshtml.cs`)**:
`[Authorize(Roles = "SuperUser,OrgAdmin")]` only; org filter dropdowns
populate from `ListAllAsync()`.

**Services**: `UserService`, `OrganizationService`,
`CourseVisibilityService`, `AdminEnrollmentService` (Management module) take
no scope parameter; the paged learner/enrollment lists run raw-SQL stored
procedures (spec 042/048 pattern) with no org filter. Scoped primitives
already exist and are unused: `UserService.ListByOrgScopeAsync`,
`OrganizationService.GetSubtreeAsync`, `AuthHelpers.IsInOrgSubtree`,
`DashboardService.GetDescendantOrgIdsAsync` (private).

## Fix (design recorded in ADR 0010 before code — Principle X step 4)

Enforce subtree scope **inside the Management application services**: every
admin list/read/write method takes a caller `OrgScope` (SuperUser system-wide;
OrgAdmin subtree-filtered for lists, checked for single-target operations).
The Host boundary (endpoints + pages) builds the `OrgScope` from the existing
auth claims and passes it; out-of-scope single-target operations return 403
(JSON, house pattern from spec 050 — never `Forbid()`), lists are filtered.
The SP-backed paged lists gain a `@RootOrgId NULL` parameter via migration
(NULL = system-wide; otherwise subtree via recursive CTE in the SP). ADR 0010
records why service-level beats endpoint-attribute enforcement (a forgotten
attribute is exactly the hole being fixed).

## Acceptance Criteria

1. An OrgAdmin of a child org, on `GET /api/users`: sees only users whose
   organization is in their subtree (API surface; the Learners page likewise).
   Out-of-subtree `GET/PUT/DELETE /api/users/{id}` → 403;
   `POST /api/users` with an out-of-subtree `organizationId` → 403.
2. An OrgAdmin of a child org, on `/api/organizations`: list/picker show only
   their subtree; out-of-subtree `GET/PUT/DELETE /api/organizations/{id}` →
   403; `POST` with an out-of-subtree `parentId` (or root-level create) → 403.
3. An OrgAdmin of a child org, on `/api/admin/courses`: list shows only
   courses of their subtree; `PUT .../visibility` with an out-of-subtree
   `organizationId` → 403; `DELETE` of a course whose org is out of subtree →
   403.
4. An OrgAdmin of a child org, on `/api/admin/enrollments`: list shows only
   enrollments of students in their subtree; `POST` (and `/bulk`) enrolling a
   student out of subtree → 403/409-free rejection; `DELETE` of an
   out-of-subtree student's enrollment → 403.
5. SuperUser: every surface returns system-wide results, all writes succeed
   (unchanged behavior).
6. Learner: every admin surface still denied (regression).
7. E2E coverage in `08-rbac.spec.ts` asserts cross-org denial for at least
   the **users, organizations, course-visibility and enrollments** surfaces
   (a child org + child-org OrgAdmin is created by the test via the SuperUser
   API, cleaned up after — no seed-data change, so existing exact-count E2E
   baselines are untouched).
8. The existing 172-passed + 1-skip E2E baseline and the unit suites stay
   green (the documented pre-existing Enrollment failure excepted).

## Testing Strategy

- **Unit (new `tests/Management.Tests`, house pattern)**: per service —
  subtree list filtering, single-target 403 results, SuperUser system-wide;
  red-verified against the unscoped services where the signature allows
  (services gain the `OrgScope` parameter with the check in the same change;
  red = the pre-fix E2E/API behavior, demonstrated by the new E2E assertions
  against the pre-fix build).
- **E2E**: `08-rbac.spec.ts` gains a "RBAC — OrgAdmin subtree scope" describe
  block (setup: SuperUser creates a child org + child OrgAdmin; asserts AC 1–4
  over real HTTP; teardown: delete admin + org).
- **Regression guard**: full gate 2 (units + full Playwright 172+1).

## Out of Scope

- RLS / database-level enforcement (ADR 0010 records why not).
- Session GUID rotation, rate limiting, audit logging.
- The pre-existing `AdminListLearnersTests` 8-vs-9 column failure (documented,
  item 1) — unless the learner-list SP migration for this spec touches the same
  SP, in which case the assertion is corrected as part of the migration (the
  SP is being re-created anyway for scoping — see plan).
