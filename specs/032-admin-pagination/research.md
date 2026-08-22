# Research: Admin List Pagination with Page Size Toggle

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-21

All technical unknowns from the Technical Context were resolved during codebase study.
No `[NEEDS CLARIFICATION]` items remain.

---

## R1. Pagination algorithm: OFFSET/FETCH vs keyset (seek) pagination

**Decision**: OFFSET/FETCH (`OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY`) inside each
stored procedure, exactly like the existing `BrowseCourses` procedure (spec 019).

**Rationale**:
- The user asked for "the same pagination implemented in Browse Courses" — OFFSET/FETCH is the
  established, user-approved pattern in this codebase.
- Page sizes are small (≤ 100) and target volume is ≤ 10,000 rows per list; OFFSET scans at
  most `offset + page` index rows, which is negligible at this scale.
- Keyset pagination would require cursor state in the URL/JS, non-uniform UI behavior, and
  breaks the "jump back one page after a delete" requirement. Constitution II ("every
  abstraction must be explainable in one plain sentence") favors the simpler option.

**Alternatives considered**:
- *Keyset/seek method* — rejected: more complex UI + API state, no measurable benefit at
  10k-row scale, diverges from the reference implementation the user cited.
- *LINQ `Skip/Take` in EF Core* — rejected: the user explicitly required stored procedures;
  also loses the single-query count+page guarantee.

---

## R2. Stored procedure placement and creation mechanism

**Decision**:
- `dbo.BrowseCourses` — **extended in place** (Catalog context migration): add optional
  `@SortBy NVARCHAR(20) = N'title'`, `@SortDirection NVARCHAR(4) = N'asc'` and add
  `c.OrganizationId` to the result set. Defaults preserve the public Browse Courses behavior
  byte-for-byte (FR-017).
- `dbo.AdminListEnrollments`, `dbo.AdminListLearners` — **new** procedures created in a
  migration on the Enrollment context.

All three are created with raw `migrationBuilder.Sql(...)` EF Core migrations following the
existing pattern (`20260805020000_AddFullTextIndexAndBrowseProcedure.cs`), including the
`IF OBJECT_ID(...) IS NOT NULL DROP PROCEDURE` guard so re-running the migration is idempotent.
Migrations run automatically at Host startup (`Database.Migrate()` in `Program.cs`).

**Rationale**: One query per list = one procedure; the owning module's migration owns its
procedure. Extending `BrowseCourses` keeps a single source of truth for the course-list query
instead of a second, drifting duplicate.

**Alternatives considered**:
- *Separate `AdminListCourses` procedure* — rejected: duplicates the WHERE/filter logic of
  `BrowseCourses`; the sort parameter is harmless to the public page (defaults).
- *EF Core `FromSqlRaw` on a query* — rejected: mixed bag (EF would re-wrap paging); the raw
  `SqlCommand` + reader pattern in `CourseCatalogService.BrowseAsync` is the established,
  human-legible way to consume these procedures.

---

## R3. Cross-module join for the enrollment course-title filter

**Decision**: `AdminListEnrollments` joins `Enrollments e JOIN Students s ON ... JOIN Courses c
ON e.CourseId = c.Id` directly in T-SQL. All tables live in one database (`dbo`), verified via
the shared `Sql` connection string for all four DbContexts.

**Rationale**: FR-014 requires the course-title filter to apply *before* paging, and FR-015
requires stable, gap-free pages. The current contract-based design (`IEnrollmentAdmin.ListAsync`
→ then `ICourseLookup` for titles) cannot page correctly: filtering by course title after
fetching a page would return partial pages and wrong totals. The join must be inside the paged
query. Constitution III is about *compiled* boundaries (project references, enforced by
NetArchTest) — no new cross-module project reference is introduced; the Enrollment module's
C# still compiles against its own domain + Contracts only. This is a data-level seam,
documented in ADR 0008 per Constitution IV.

**Alternatives considered**:
- *Two-phase (page enrollments+students, then filter titles via ICourseLookup)* — rejected:
  mathematically unable to produce correct page boundaries/totals for a course-title filter.
- *Duplicate course titles into the Enrollment schema* — rejected: denormalization with sync
  burden for a teaching project; violates "no abstraction without a current problem" (II).

---

## R4. Org-name enrichment without full-table loads

**Decision**:
- **Courses page**: the extended `BrowseCourses` returns `OrganizationId`; the page resolves
  org names for the **distinct org IDs on the current page** via `IOrganizationLookup`
  (cached in a page-local dictionary). This replaces the current `GetAllCoursesAsync()`
  full-catalog load (which exists only to build an org-name dictionary).
- **Enrollments page**: `AdminListEnrollments` returns `s.OrganizationId` per row;
  `IUserLookup.GetUsersAsync(pageStudentIds)` supplies name/email in one batch (already the
  pattern); `UserSummary` **gains an `OrganizationId` field** (additive contract change) so the
  Management service can resolve org names for the page's distinct orgs in bounded lookups.
  This eliminates the current per-row `GetUserScopeAsync` call (N+1 over *all* rows today).
- **Learners page**: `AdminListLearners` returns `OrganizationId` per row (same `Students`
  table); `StudentProvisionedDto` already carries `OrganizationId`, so the Management
  `UserService` paged variant resolves org names for distinct orgs on the page.

**Rationale**: Bounded by page size (≤ 100 distinct students/courses, usually far fewer
distinct orgs) while respecting module boundaries (org names always via
`IOrganizationLookup` contract).

**Alternatives considered**:
- *Join `Organizations` (Management-owned) inside the SQL* — rejected: would create a second
  cross-module SQL join in the same feature; the contract path is already available and
  bounded, so the simpler boundary-respecting option wins.
- *Keep per-row `GetUserScopeAsync`* — rejected: N+1 per page load (up to 100 round trips);
  the batched approach is strictly better at near-zero added complexity.

---

## R5. Page size toggle mechanics

**Decision**:
- `pageSize` is an ordinary GET query/form parameter on each admin index page, offered as a
  `<select>` with exactly 10 / 30 / 50 / 100, defaulting to 10.
- The page model **allowlists** the value: anything else (999, 15, 12, empty, negative)
  resolves to 10 (FR-012).
- Changing the size submits the filter form with `pageNumber=1` (hidden reset field, same
  mechanism the Courses page already uses for filter changes) — FR-007.
- Every pagination link (Previous/Next/sort links) carries the current `pageSize` — FR-016.
- No browser persistence (spec assumption: request state only).

**Rationale**: Full-page GET navigation is the existing admin pattern (Constitution ADR 0005
explicitly avoids HTMX for navigation); a plain form select is the fewest moving parts.

**Alternatives considered**:
- *HTMX partial swap like Browse Courses* — rejected: ADR 0005 (no HTMX for navigation), and
  the spec assumption fixed full-page navigation; spec 028 also showed the extra failure
  surface of the HTMX binding path.
- *Cookie/localStorage persistence* — rejected: not requested; adds state management and
  cross-page leakage the spec explicitly excludes.

---

## R6. Deterministic ordering (no dupes/gaps while paging)

**Decision**: every procedure orders by the user-visible sort key **plus the row's primary key
as final tie-breaker**:

| List | Default order |
|---|---|
| Courses | `@SortBy` column (Title/Category/Duration) ASC/DESC, then `c.Id ASC` |
| Enrollments | `EnrolledAt DESC`, then `e.Id DESC` |
| Learners | `Name ASC`, then `s.Id ASC` |

Sort columns/directions are validated in the procedure (unknown `@SortBy` → Title; unknown
direction → ASC) and in the page model (allowlist) — no dynamic SQL string concatenation.

**Rationale**: `EnrolledAt` and names have many ties; without a PK tie-break, SQL Server is
free to reorder equal keys between requests, which is exactly the "row appears on two pages /
vanishes" failure mode of FR-015/NFR-003.

**Alternatives considered**:
- *`ROW_NUMBER()` window then page* — rejected: forces a full materialization of the sorted
  set and adds a CTE the reader must parse for no benefit over a stable ORDER BY.

---

## R7. Out-of-range pages and pages emptied by deletes

**Decision**:
- Page model computes `totalPages = max(1, ceil(totalCount / pageSize))` and clamps
  `effectivePage = max(1, min(requestedPage, totalPages))` — the same clamp the public browse
  handler already implements (spec 028 fix).
- After a row action (cancel enrollment / delete course) the page re-queries with the same
  filters: if the returned page is empty and `effectivePage > 1`, it re-queries
  `effectivePage - 1`; if page 1 is empty, the empty state renders (spec edge cases).
- The clamped (effective) page is what gets rendered and what pagination links build from, so
  a tampered `pageNumber=99999` never produces an error or a blank-but-paginated page (FR-011).

**Rationale**: Reuses proven behavior from the browse handler; keeps URL state authoritative
while guaranteeing a valid view.

**Alternatives considered**:
- *Redirect on out-of-range* — rejected: extra round trip; the browse page precedent renders
  the clamped page directly and spec 028's E2E expectations match that.

---

## R8. Test strategy

**Decision**:
1. **xUnit integration tests** (live MSSQL via `docker compose up mssql`, connection string
   from environment — pattern: `tests/Catalog.Tests/CourseCatalogSearchTests.cs`):
   - `Catalog.Tests`: extended `BrowseCourses` — sort by each column × direction,
     `OrganizationId` present, default parameters reproduce legacy behavior, page+count
     correctness, whitespace/special-character search safety.
   - `Enrollment.Tests` (new class): `AdminListEnrollments` + `AdminListLearners` — filter
     combinations, paging math, determinism (two consecutive identical queries return
     identical page boundaries), clamping inputs, PK tie-break with tied `EnrolledAt` values.
   - Tests create and clean up their own filler rows (unique marker prefix), mirroring the
     Playwright pattern, so they are order-independent.
2. **Playwright E2E** (new `tests/Playwright.Tests/tests/16-admin-pagination.spec.ts`,
   self-contained per the 11-course-pagination spec pattern): SuperUser login; create filler
   learners/enrollments/courses in `beforeAll`, delete in `afterAll`; verify on all three
   admin pages: controls render, page size toggle (10→50 etc.) re-renders at page 1,
   Previous hidden on page 1 / Next hidden on last page, filter+pagination compose,
   `pageSize=999` → 10 and `pageNumber=999` → last page via URL.
3. **ArchitectureTests** re-run unchanged (Constitution III gate).

**Rationale**: Constitution XIII requires E2E proof of the changed behavior plus regression
after merge; integration tests pin the SP contracts (the feature's core) independently of the
UI layer.

**Alternatives considered**:
- *UI-only (Playwright) testing* — rejected: the stored-procedure behavior (clamping,
  determinism, count correctness) is exactly what deserves direct, fast tests.
- *Unit tests with an in-memory SQL shim* — rejected: no SQL Server emulator fits the sandbox
  budget; the project's established integration pattern already targets live MSSQL.
