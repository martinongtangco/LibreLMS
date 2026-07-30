# Research: RBAC Management Portal

## Decision 1: Organization Hierarchy Storage Pattern

**Decision**: Adjacency list pattern (each Organization has a `ParentId` GUID FK, null for root)

**Rationale**:
- Simplest pattern for tree structures up to 10 levels deep (SC-008)
- Direct FK constraint enforces referential integrity
- No recursive CTE complexity needed for CRUD; recursive queries only for subtree traversal
- Existing codebase uses simple FK patterns (Student→Course via Enrollment, ScormPackage→Course)
- EF Core handles adjacency list relationships naturally with `ICollection<Organization> Children`

**Alternatives considered**:
- **Materialized path**: Store full path as string (e.g., `/root/orgA/orgB`). Better for deep trees but more complex for updates. Overkill for 10-level limit.
- **Nested sets**: Left/right values for O(1) subtree queries. Complex for inserts/deletes. Over-engineered for our scale.
- **Closure table**: Separate table for all ancestor-descendant pairs. Most flexible but adds join complexity. Not needed for our use case.

---

## Decision 2: RBAC Enforcement Approach

**Decision**: ASP.NET Core Authorization with custom `RequireOrgScopeAsync` handler + role claims

**Rationale**:
- Reuses built-in ASP.NET Core authorization pipeline — no custom auth layer
- Roles stored as claim on authentication cookie (existing pattern: `Student.Roles` string field extended)
- Custom `AuthorizationHandler<RequireOrgScopeRequirement>` evaluates organizational subtree access
- Policy applied at controller/page level via `[Authorize(Policy = "OrgScope")]`
- SuperUser bypasses all org scope checks in the handler

**Alternatives considered**:
- **Policy-per-endpoint**: Define a policy for each org admin. Too many policies, doesn't scale.
- **Middleware-based access control**: Custom middleware before MVC. Breaks Razor Pages authorization model.
- **Database-level row security**: SQL Server RLS. Tight coupling to MSSQL, harder to test.

**Implementation pattern**:
```
// Claims on authenticated user:
// - Role: "SuperUser" | "OrgAdmin" | "Learner"
// - OrganizationId: Guid (primary org)

// Authorization handler checks:
// 1. If Role == SuperUser → grant
// 2. If Role == OrgAdmin → check if target org is in user's subtree
// 3. If Role == Learner → deny admin operations
```

---

## Decision 3: Course Visibility Inheritance Model

**Decision**: Application-level inheritance query with CourseVisibilityOverride table for opt-out

**Rationale**:
- Course entity stores `OrganizationId` (single FK — which org "owns" the course)
- When listing courses for an org, query courses where `OrganizationId` is the org or any ancestor
- Ancestor traversal done via recursive CTE in SQL or in-memory walk (tree depth ≤ 10)
- `CourseVisibilityOverride` table records (OrganizationId, CourseId, IsHidden) for opt-out
- Check override before showing inherited course

**Alternatives considered**:
- **Duplicate course records per level**: Course row per org that sees it. Wasteful storage, update complexity.
- **Materialized course membership table**: Separate table maps courses to all visible orgs. Needs maintenance on org tree changes.
- **Denormalize course list per org**: Store visible course IDs on each org. Inconsistent, hard to maintain.

**Query pattern for org-scoped course listing**:
```sql
-- Get all ancestor org IDs (recursive CTE)
WITH Ancestors AS (
    SELECT Id, ParentId FROM Organizations WHERE Id = @OrgId
    UNION ALL
    SELECT o.Id, o.ParentId FROM Organizations o INNER JOIN Ancestors a ON o.Id = a.ParentId
)
-- Get courses from org and all ancestors, minus hidden overrides
SELECT c.* FROM Courses c
WHERE c.OrganizationId IN (SELECT Id FROM Ancestors)
  AND NOT EXISTS (
    SELECT 1 FROM CourseVisibilityOverrides v
    WHERE v.OrganizationId = @OrgId AND v.CourseId = c.Id AND v.IsHidden = 1
  )
```

---

## Decision 4: Student Entity Extension for Organizations

**Decision**: Add `OrganizationId` GUID FK to existing `Student` entity; rename role handling

**Rationale**:
- Minimal change to existing entity — just add one FK column
- Existing `Roles` string field extended: values change from "Admin" to "SuperUser", "OrgAdmin", "Learner"
- Student entity name preserved (domain consistency) — business term "Learner" maps to `Student` domain entity
- Migration adds NOT NULL column with default root org ID, then backfills

**Alternatives considered**:
- **Separate User entity**: Create new User table, link Student to User. Adds join complexity, breaks existing enrollment/SCORM references.
- **Organization table with users collection**: EF Core owned entity pattern. Same FK, different modeling — adjacency list is clearer.

---

## Decision 5: Course Entity Extension for Organizations

**Decision**: Add `OrganizationId` GUID FK to existing `Course` entity

**Rationale**:
- Single FK per course — the org that "owns" it
- Inheritance handled at query time (Decision 3), not storage time
- Migration adds NOT NULL column with default root org ID, backfills existing courses
- Existing course upload flow enhanced to specify target org

**Alternatives considered**:
- **Course-Org many-to-many**: A course belongs to multiple orgs. Violates FR-010 (exactly one org per course).
- **Separate org-course mapping table**: Adds indirection for a 1:1 relationship. Unnecessary complexity.

---

## Decision 6: Dashboard Metrics Aggregation

**Decision**: EF Core raw SQL queries for dashboard metrics, with caching via response caching

**Rationale**:
- Dashboard reads are the most performance-critical path (SC-004: 3-second load)
- Raw SQL with aggregation (COUNT, AVG, SUM) avoids loading full entity graphs into memory
- Response caching (VaryByQueryKeys on org scope) reduces repeated queries
- No Valkey involvement — metrics are derived from MSSQL data, not ephemeral state

**Alternatives considered**:
- **Pre-computed metric tables**: Materialized views updated on data changes. Over-engineered for current scale.
- **In-memory aggregation**: Load all entities and aggregate in C#. N+1 problem, memory-intensive at scale.
- **Valkey cached metrics**: Write metrics to Valkey on data changes. Breaks Principle VI (Valkey for ephemeral SCORM state only).

---

## Decision 7: Module Boundary for Management

**Decision**: New `Management` module with `Management.Contracts` for cross-module interfaces

**Rationale**:
- Follows existing module pattern exactly (Catalog, Enrollment, Scorm)
- Management.Contracts exposes:
  - `IOrganizationLookup` — look up org hierarchy info (used by Catalog/Enrollment for scope checks)
  - `IUserInfoLookup` — look up user's role and primary org (used by other modules for access validation)
- ArchitectureTests updated to include Management module in boundary checks
- Management module depends on Catalog.Contracts and Enrollment.Contracts (read-only lookups)
- No circular dependencies: Catalog/Enrollment do NOT depend on Management.Contracts

**Alternatives considered**:
- **Add org logic to Enrollment module**: Enrollment already handles users. But org management is a distinct capability that doesn't belong to enrollment domain.
- **Put org logic in SharedKernel**: SharedKernel is for cross-cutting abstractions (Entity, Result), not business domain logic.

---

## Decision 8: Authentication Integration

**Decision**: Extend existing cookie-based authentication; add organization claims on login

**Rationale**:
- Existing authentication uses cookie middleware with Login/Logout pages
- On successful login, add `OrganizationId` and `Role` claims to the principal
- No new authentication mechanism — reuse existing password/hash flow
- Existing `Student.PasswordHash` used for authentication; `Student.Roles` extended for authorization
- Access denied redirects to existing Login page (existing pattern)

**Alternatives considered**:
- **JWT tokens**: Breaks existing session model, adds infrastructure complexity. Overkill for web portal.
- **Windows/SSO auth**: Out of scope — constitution notes auth is handled separately.
