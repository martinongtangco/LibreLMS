# Research / Verified Facts — spec 052

Verified against the code on 2026-09-13 (handoff claims re-checked).

- **Machinery exists, never called**: `src/Host/ManagementAuth/` contains
  `OrgScopeAuthorizationHandler` (registered at Program.cs:137),
  `RequireOrgScopeRequirement`, `OrgScopeExtensions.IsInSubtreeAsync`,
  `AuthHelpers.IsInOrgSubtree`. Zero call sites outside the folder. The
  `AuthenticatedWithOrgScope` policy is a role assertion only (no subtree
  logic); the handler's requirement is used by no policy.
- **Unscoped surfaces confirmed** (exact endpoints inventoried in the spec;
  all carry `[Authorize(Roles = "SuperUser,OrgAdmin")]` only).
- **Scoped primitives already exist, unused**: `UserService.ListByOrgScopeAsync`
  (single-org, not subtree), `OrganizationService.GetSubtreeAsync`,
  `DashboardService.GetDescendantOrgIdsAsync` (private BFS — the reuse
  target), `AuthHelpers.GetOrganizationAsync`/`GetAncestorOrgIdsAsync` via
  `IOrganizationLookup`.
- **Dashboard is already scoped**: `GET /api/dashboard` routes by role and
  passes the caller's org claim to `GetOrgMetricsAsync`, which sums
  descendants — the reference pattern for what "scoped" looks like here.
- **Admin services are Host-only**: grep confirms `UserService`,
  `OrganizationService`, `CourseVisibilityService`, `AdminEnrollmentService`,
  `DashboardService` are called only from `src/Host/Program.cs`,
  `src/Host/Pages/Admin/**`, and `src/Host/Pages/Courses/Index.cshtml.cs`
  (learner-facing catalog read — check at task time; if it uses an admin
  service for scope-sensitive data it needs the scope too, SuperUser-shaped
  for the public catalog).
- **Paged lists are SP-backed**: `AdminListLearners` (@Search, @Role,
  @PageSize, @PageNumber; 9 columns — spec 042 added ThemePreference; the
  pre-existing unit failure asserts 8) and `AdminListEnrollments`
  (@StudentName, @CourseTitle, @PageSize, @PageNumber). Both do row + count
  SELECTs with identical WHERE clauses — a `#Subtree` temp table +
  `@RootOrgId NULL` param slots in cleanly (idempotent DROP+CREATE house
  pattern, migrations 20260822123208 / 20260829105050).
- **Org tree**: `Organization.ParentId` (+ `IsDeleted` soft-delete); one
  seeded org ("Root Organization", `00000000-...-0001`). All seeded users
  (incl. the OrgAdmin `admin@example.com`) sit in Root — so the existing
  seeded OrgAdmin's subtree is everything, and the E2E cross-org assertions
  need a test-created child org + child OrgAdmin (setup/teardown in the
  spec; no seed change → exact-count baselines untouched).
- **Auth claims**: the cookie carries Role (`ClaimTypes.Role`) +
  `OrgClaimTypes.OrganizationId` + student id (`ClaimTypes.NameIdentifier`)
  (ADR 0009 / AuthClaimsTests pin the set) — everything the Host needs to
  build `OrgScope` is already in the cookie.
- **JSON-403 house pattern** (spec 050): `Results.Json(..., statusCode: 403)`
  — `Results.Forbid()` 302s to AccessDeniedPath under cookie auth.

## Decision: service-level enforcement (ADR 0010)

Recorded in full in `docs/adr/0010-org-scope-enforced-in-management-services.md`:
`OrgScope` is a required parameter on every admin service method; lists
filter, single-target ops check; the Host builds the scope from claims.
Rejected: per-endpoint policy attributes (a forgotten attribute is the hole
being fixed), param-sniffing middleware (implicit, untestable), MSSQL RLS
(scale not justified).

## Decision: 403 for out-of-scope single-target reads

- **Decision**: out-of-scope `GET /api/users/{id}` etc. → 403 (not 404).
- **Rationale**: consistency with spec 050's ownership semantics (mismatch =
  403, not 404); the E2E contract asserts 403; ids are GUIDs (not enumerable),
  so the existence disclosure is not actionable. Recorded in ADR 0010.

## Decision: student-org scoping for enrollments

- **Decision**: an enrollment is in scope iff the ENROLLED STUDENT's org is in
  the caller's subtree (the SP filters `s.OrganizationId`; the non-paged
  path checks the student's org).
- **Rationale**: an org admin manages their people's enrollments; courses are
  shared catalog (visibility is the separate, already-per-org concern
  enforced on the visibility endpoint). Keeps the rule one-dimensional and
  testable.
