# ADR-0010: Organization Scope Is Enforced in the Management Application Services

**Status**: Accepted
**Date**: 2026-09-13
**Supersedes**: N/A

## Context

Spec 052 fixes the finding that the org-scope machinery (`OrgScopeAuthorizationHandler`, `RequireOrgScopeRequirement`, `OrgScopeExtensions.IsInSubtreeAsync`, `AuthHelpers.IsInOrgSubtree`) exists, is registered in DI, and is never called: every admin surface gates on role alone, so an OrgAdmin reaches every organization. The fix must place the subtree check where it cannot be forgotten per call site. The surfaces are ~20 minimal-API endpoints across five groups plus 14 admin Razor Pages, all of which call the same four Management application services (`UserService`, `OrganizationService`, `CourseVisibilityService`, `AdminEnrollmentService`) — verified: those services are called only from the Host.

## Decision

The subtree scope is enforced **inside the Management application services**. A new `OrgScope` value (in `Management.Contracts`) — `SuperUser` (system-wide) or `ForOrgAdmin(orgId)` (subtree) — becomes a required parameter on every service method that lists or mutates admin data:

- **List reads** are filtered to the caller's subtree (OrgAdmin) or unfiltered (SuperUser).
- **Single-target reads/writes** load the target, check subtree membership, and refuse out-of-scope targets with a forbidden result the Host maps to a JSON 403 (spec 050's house pattern — never `Results.Forbid()`, which 302s under cookie auth).
- The Host boundary builds the `OrgScope` from the existing auth claims (`RoleNames` role + `OrganizationId` claim, via the existing `AuthHelpers`) and passes it in; pages and endpoints stay thin.
- The SP-backed paged lists (learners, enrollments) gain a `@RootOrgId UNIQUEIDENTIFIER NULL` parameter via migration: NULL = system-wide (SuperUser), otherwise the SP computes the subtree with a recursive CTE and filters in one query (the spec 042/048 pattern keeps paging/counts in SQL — post-filtering in C# would break both).
- Subtree computation reuses the existing BFS (`DashboardService.GetDescendantOrgIdsAsync` becomes the shared `OrgSubtree` helper in the Management module).

**Rejected alternatives**:

1. **Endpoint-attribute/policy enforcement** (`[Authorize(Policy = ...)]` with a per-endpoint `RequireOrgScopeRequirement(targetOrgId)`) — the requirement needs the *target* org, which lives in different places per surface (query param, route value, body field, or the loaded row's `OrganizationId`). Every endpoint would need bespoke requirement wiring, and a forgotten attribute is exactly the hole being fixed: the previous machinery was built, registered, and never applied. Service-level enforcement has no per-call-site artifact to forget — the parameter is required by the signature.
2. **Middleware that sniffs route/query params** — implicit and untestable in isolation; breaks silently when a surface adds a parameter; duplicates the service's knowledge of where the target org lives.
3. **MSSQL row-level security** — the strongest guarantee, but the app connects as `sa`, dev scale is a handful of orgs, and RLS would add a second enforcement layer (session context, policy, testing surface) the codebase's scale does not justify. Revisit if the org count or threat model changes.

## Consequences

**Positive**:

- The data path is the enforcement point: no endpoint or page can reach out-of-subtree rows regardless of how it is wired; new admin surfaces are scoped by default (the parameter is required)
- Scope logic lives next to the queries that need it (subtree filter in SQL/EF, one query); the Host stays a thin claims→`OrgScope` translation
- The unused handler/policy machinery is left in place (still correct for future policy-based needs) but the fix does not depend on it — Principle II: one enforcement mechanism in production

**Negative**:

- Every admin service method's signature changes; all call sites are in the Host (verified), so the blast radius is contained, but it is wide (~20 endpoints + 14 pages)
- The learner/enrollment SPs are re-created by migration — the re-creation also settles the documented pre-existing `AdminListLearnersTests` 8-vs-9 column assertion (the spec 042 SP shape is kept; the test is corrected as part of this migration)
- An OrgAdmin's out-of-scope `GET /api/users/{id}` returns 403 (the E2E contract), which discloses that *something* exists at that id; ids are GUIDs (not enumerable) and the alternative (404) was considered and rejected for consistency with spec 050's ownership semantics

## Related

- Spec 052 (organization scope enforcement)
- ADR 0008 (cross-module SQL join for admin listing) — the SP pattern the scoped `@RootOrgId` parameter extends
- Spec 050 (SCORM session ownership) — the JSON-403 house pattern for refused ownership checks
