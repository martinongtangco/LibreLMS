# Implementation Plan: Admin List Pagination with Page Size Toggle

**Branch**: `story/032-admin-pagination` | **Date**: 2026-08-21 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features.

**Input**: Feature specification from `/specs/032-admin-pagination/spec.md`

## Summary

The Admin > Courses, Enrollments, and Learners pages currently list records without usable
pagination (Enrollments and Learners load every row into the page; Courses has a fixed-size
variant with sorting applied only within the fetched page). This plan brings all three admin
lists to the same server-side pagination standard as Browse Courses: one parameterized
**stored procedure** per list executes filtering + sorting + OFFSET/FETCH paging in the
database and returns only the requested page plus the matching total count. All three pages gain
a **page size selector (10 / 30 / 50 / 100, default 10)** that resets to page 1 on change, and
the pagination controls (Previous/Next with hidden boundary controls, "Page X of Y (Z total)")
match Browse Courses.

Technical approach (details in [research.md](research.md)):

- **Three stored procedures**, created via raw-SQL EF migrations on the owning context:
  - `dbo.BrowseCourses` (Catalog context) — *extended in place* with optional
    `@SortBy` / `@SortDirection` parameters (defaults preserve the public browse behavior) and
    `OrganizationId` added to the result set.
  - `dbo.AdminListEnrollments` (Enrollment context) — joins `Enrollments` + `Students` +
    `Courses` (single shared database, `dbo`), filters by student name / course title, orders
    newest-first, OFFSET/FETCH paged.
  - `dbo.AdminListLearners` (Enrollment context) — `Students` only, filters by name/email
    search and exact role, name-ascending, OFFSET/FETCH paged.
- **Contract changes (additive only)** in `Enrollment.Contracts`: paged list methods on
  `IEnrollmentAdmin` and `IUserProvisioning` plus small result records; `UserSummary` gains
  `OrganizationId` so the Management module can resolve org names in bounded batches.
- **Management module** `AdminEnrollmentService` / `UserService` gain paged variants that
  delegate to the new contract methods and enrich org names per distinct org on the page
  (replacing the current full-table loads and per-row scope lookups).
- **Host admin pages** gain `PageNumber` + `PageSize` binding with allowlist validation
  ({10,30,50,100}, default 10), page clamping, page-1 reset on filter/size change, and shared
  pagination-control markup.
- **One new ADR** (cross-module SQL join for the enrollment course-title filter — the join must
  happen inside the paged query for pagination correctness, which a contract-based two-phase
  fetch cannot provide).

## Technical Context

**Language/Version**: C# on .NET 10 (pinned via `global.json`), ASP.NET Core minimal APIs + Razor Pages

**Primary Dependencies**: EF Core 10 (DbContext per module), `Microsoft.Data.SqlClient` (raw ADO.NET `SqlCommand` for stored-procedure calls — established pattern in `CourseCatalogService.BrowseAsync`), NetArchTest (module-boundary tests)

**Storage**: MSSQL — single database (`dbo` tables), four DbContexts (Catalog, Enrollment, Management, Scorm) sharing one connection string; Valkey is untouched by this feature

**Testing**: xUnit integration tests against live MSSQL (docker compose `mssql` service; pattern in `tests/Catalog.Tests/CourseCatalogSearchTests.cs`), Playwright E2E (TypeScript, `tests/Playwright.Tests`), `tests/ArchitectureTests` (build-blocking boundary checks)

**Target Platform**: Linux devcontainer (sandbox per Constitution V); Host process served at `http://localhost:5000`

**Project Type**: Web application (modular monolith: Host + Catalog / Enrollment / Scorm / Management modules)

**Performance Goals**: first-page load and each page navigation < 1s for lists up to 10,000 matching rows (spec NFR-001/002); only page rows + count transferred per request (NFR-002)

**Constraints**: page size restricted to {10, 30, 50, 100}, default 10; invalid sizes fall back to 10; page numbers clamped to 1..last; deterministic ordering with PK tie-break; Browse Courses page behavior unchanged (FR-017)

**Scale/Scope**: 3 admin pages, 3 stored procedures (1 extension + 2 new), 2 module contract files, ~8 service/page files, 1 new Playwright spec + 1 new integration test class; target 10,000 rows per list (current volumes: ~10 courses, low double-digit users/enrollments)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Modular Monolith | ✅ PASS | Changes stay inside existing modules + Host pages; no new deployables |
| II | Clean Architecture, Applied Simply | ✅ PASS | SP calls live in module Application services over their own DbContext connection (existing pattern); additive contract methods only; no MediatR/CQRS/repository wrappers |
| III | Module Boundaries Are Compiled | ✅ PASS (with documented gray zone) | No new cross-module project references. One SQL-level cross-module join: `AdminListEnrollments` (Enrollment module) joins the Catalog-owned `Courses` table for the course-title filter. Project-reference boundary is intact (NetArchTest still passes); the data-level join is documented in research.md and a new ADR (0008) because the filter must be applied *before* paging |
| IV | Human-Legible AI-Authored Code | ✅ PASS | Explicit control flow; one new ADR for the non-obvious cross-module SQL join decision |
| V | The Sandbox Is Not Optional | ✅ N/A | No change to sandboxing |
| VI | Polyglot Storage With a Reason | ✅ PASS | All durable data stays in MSSQL; no Valkey usage |
| VII | Spec-Driven, Sliced Thin | ✅ PASS | Vertical slice: spec 032 exists; no module scaffolded ahead of demand |
| VIII | Branching Discipline | ✅ PASS | Implementation will run on `story/032-admin-pagination` from `main`/`master` |
| IX | Plan On Master Only | ✅ PASS | Planning executed on `master` (verified via `git branch --show-current`) |
| X | No Ad-Hoc Fixes | ✅ PASS | Issue captured in spec 032 with root cause before any code change |
| XI | Parallel Implementation With Subagents | ✅ PLANNED | `/speckit.tasks` will mark independent work `[P]` (per-module SP+service work, per-page UI work, tests) |
| XII | Return to Master After Implementation | ✅ PLANNED | Enforced at the end of `/speckit.implement` |
| XIII | Verification Before Claim | ✅ PLANNED | New Playwright spec for all three admin pages' pagination + page-size toggle; xUnit integration tests for SP behavior; post-merge regression re-run |

**Gate result: PASS** — no violations, no Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/032-admin-pagination/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── stored-procedures.md
│   ├── module-contracts.md
│   └── admin-pages-query.md
└── tasks.md             # Phase 2 output (/speckit.tasks - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Host/
│   ├── Migrations/
│   │   ├── Catalog/        # new migration: extend BrowseCourses (sort params + OrganizationId)
│   │   └── Enrollment/     # new migration: create AdminListEnrollments + AdminListLearners
│   └── Pages/Admin/
│       ├── Courses/Index.cshtml(.cs)      # page-size toggle; remove in-memory sorting
│       ├── Enrollments/Index.cshtml(.cs)  # new pagination controls + page-size toggle
│       └── Learners/Index.cshtml(.cs)     # new pagination controls + page-size toggle
├── Modules/
│   ├── Catalog/Application/
│   │   └── CourseCatalogService.cs        # BrowseAsync gains sortBy/sortDirection; DTO gains OrganizationId
│   ├── Enrollment/
│   │   ├── Application/
│   │   │   ├── EnrollmentAdminService.cs  # implements new paged contract method (SP call)
│   │   │   └── UserProvisioningService.cs # implements new paged contract method (SP call)
│   │   └── (Infrastructure/EnrollmentDbContext.cs — connection source for raw SP calls)
│   ├── Enrollment.Contracts/
│   │   ├── IEnrollmentAdmin.cs            # + ListPagedAsync + page-result records
│   │   ├── IUserProvisioning.cs           # + ListPagedAsync + page-result record
│   │   └── IUserLookup.cs                 # UserSummary + OrganizationId
│   └── Management/Application/
│       ├── AdminEnrollmentService.cs      # + paged admin variant (delegates + org enrichment)
│       └── UserService.cs                 # + paged admin variant (delegates + org enrichment)
├── docs/adr/
│   └── 0008-cross-module-sql-join-for-admin-listing.md
tests/
├── Catalog.Tests/       # extend: BrowseCourses sort behavior + OrganizationId
├── Enrollment.Tests/    # new: AdminListEnrollments + AdminListLearners SP tests (incl. edge cases)
└── Playwright.Tests/
    ├── tests/16-admin-pagination.spec.ts  # new: self-contained filler-data E2E (pattern: 11-course-pagination)
    └── pages/               # extend AdminEnrollmentsPage / AdminLearnersPage; add AdminCoursesPage
```

**Structure Decision**: Existing modular-monolith layout (Constitution I/II). No new
projects, folders, or boundaries. Each change lands in the module that owns the data:
Catalog owns the course list, the Enrollment module owns enrollments and learner accounts,
Management keeps its admin-facade role (delegating to Enrollment.Contracts, as established by
spec 027 R9), and Host renders.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations — section intentionally empty.
