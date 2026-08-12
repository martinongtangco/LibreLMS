# Implementation Plan: Admin Courses Management Overhaul

**Branch**: `bug/025-admin-courses-management` | **Date**: 2025-08-11 | **Spec**: [spec.md](./spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects.

**Input**: Feature specification from `/specs/025-admin-courses-management/spec.md`

## Summary

Fix five issues in the Admin/Courses page: (1) add a "Create Course" button and fix the broken create flow, (2) add an Edit page with a new `UpdateAsync` service method, (3) fix the delete flow by resolving org names in `GetAllCoursesAsync`, (4) add search, category filter, column sorting, and pagination reusing the existing `BrowseAsync` stored procedure pattern, and (5) improve table contrast by wrapping in a card surface and adding alternating row colors.

**Additionally**, revise the SCORM-Course relationship: (6) make `ScormPackage.CourseId` nullable so packages can exist in an "available pool" without a course, (7) integrate SCORM upload/association into the course creation form — admin can create a course with no SCORM, upload new SCORM, or associate an existing unassociated SCORM package, (8) add SCORM management to the course edit form — admin can add or replace SCORM content, (9) repurpose the Admin/Upload page for SCORM pool management — upload to pool, list available packages, delete orphaned packages, (10) add delete confirmation warning when a course with SCORM is deleted.

Changes span the Host project's Razor Pages, the Catalog module's `CourseCatalogService`, and the Scorm module's `ScormPackageService` and `ScormDbContext`. A database migration is needed for the nullable CourseId change.

## Technical Context

**Language/Version**: C# / .NET 10 (GA), ASP.NET Core minimal APIs + Razor Pages

**Primary Dependencies**: EF Core (MSSQL), HTMX (for partial page updates), StackExchange.Redis (Valkey — not used in this feature)

**Storage**: MSSQL via EF Core `CatalogDbContext` (Courses table), `ScormDbContext` (ScormPackages table), and `ManagementDbContext` (CourseVisibilityOverrides table)

**Testing**: xUnit (unit tests), Playwright (E2E tests), NetArchTest (architecture tests)

**Target Platform**: Linux server (dev container), web portal

**Project Type**: Web application (Razor Pages + minimal API endpoints)

**Performance Goals**: Pagination loads within 1 second for 100+ courses (using stored procedure); CRUD operations complete within 2 seconds; SCORM upload and extraction complete within 5 seconds for packages up to 50MB

**Constraints**: Module boundaries enforced by ArchitectureTests; no cross-module references except through `*.Contracts`; admin pages use direct service injection (not HTTP client)

**Scale/Scope**: Multi-page refactor across Host Razor Pages, Catalog module, and Scorm module. ~8 files modified, ~4 files created, 1 database migration.

**Key Unknowns (resolved in research.md)**:
- How to handle the Scorm → Catalog module boundary for SCORM association during course creation
- Whether to use a transaction spanning both CatalogDbContext and ScormDbContext
- How the unique index on CourseId should handle nullable values in MSSQL

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | PASS | Changes span Host, Catalog, and Scorm modules; each change stays within its module's boundaries |
| II. Clean Architecture | PASS | `UpdateAsync` added to `CourseCatalogService` (Application layer); SCORM methods added to `ScormPackageService` (Application layer); Edit page uses direct DI |
| III. Module Boundaries | NEEDS CLARIFICATION | Course creation coordinates Catalog (create Course) and Scorm (upload SCORM) via Host orchestration. See research.md for transaction strategy. |
| IV. Human-Legible Code | PASS | Changes follow existing patterns (direct DI, Razor Pages, HTMX pagination) |
| V. Sandbox Not Optional | N/A | Implementation happens in dev container |
| VI. Polyglot Storage | PASS | No Valkey changes; only MSSQL via EF Core. SCORM content stored in wwwroot (filesystem) — existing pattern |
| VII. Spec-Driven | PASS | This plan follows from spec 025 (revised with SCORM requirements) |
| VIII. Branching Discipline | PASS | Branch: `bug/025-admin-courses-management` |
| IX. Plan On Master | PASS | Planning on master branch |
| X. No Ad-Hoc Fixes | PASS | Spec revised before any code changes |
| XI. Parallel Implementation | N/A | Applied during `/speckit.implement` |
| XII. Return to Master | N/A | Applied after implementation |
| XIII. Verification Before Claim | N/A | Applied during implementation (build + E2E tests) |

**Pre-Design Concerns**:
- **Principle III (Module Boundaries)**: Course creation with SCORM requires coordinating two DbContexts (CatalogDbContext and ScormDbContext). Host orchestrates by calling both services. Since both contexts use the same MSSQL database, transaction coordination is needed. This does not violate module boundaries — Host is the orchestrator.

**Post-Design Re-Evaluation**:

| Principle | Status | Notes |
|-----------|--------|-------|
| III. Module Boundaries | PASS | Resolved: Host orchestrates both services. No cross-module reference — Host references both Catalog and Scorm independently. Sequential saves (course first, SCORM second) with graceful degradation if SCORM fails. |
| I. Modular Monolith | PASS | Unchanged. All changes within existing module boundaries. |
| II. Clean Architecture | PASS | Unchanged. New methods follow Application layer patterns. |
| VI. Polyglot Storage | PASS | SCORM content stored in wwwroot (filesystem) — existing pattern. No Valkey changes. |
| All others | PASS | No new violations introduced by SCORM integration. |

**Resolution of NEEDS CLARIFICATION** (from research.md):
- **Module boundary for Course+SCORM creation**: Host page model injects both `CourseCatalogService` and `ScormPackageService`. No cross-module reference.
- **Transaction strategy**: Sequential saves. Course created first; if SCORM upload fails, course exists without SCORM (valid state) and admin can add SCORM via edit page.
- **Nullable CourseId index**: Filtered unique index `WHERE CourseId IS NOT NULL` in MSSQL allows multiple null values while enforcing 1:1 for associated packages.

## Project Structure

### Documentation (this feature)

```text
specs/025-admin-courses-management/
├── plan.md              # This file
├── research.md          # Phase 0 findings
├── data-model.md        # Phase 1 entity definitions
├── quickstart.md        # Phase 1 validation scenarios
└── contracts/           # Not applicable (internal admin page, no external API contract)
```

### Source Code Changes

```text
src/Host/
├── Pages/Admin/Courses/
│   ├── Index.cshtml       # Modified: add Create button, search/filter, sorting, pagination, card wrapper, SCORM status column
│   ├── Index.cshtml.cs    # Modified: add search, filter, sort, pagination query params; use BrowseAsync; include SCORM status
│   ├── Create.cshtml      # Modified: add SCORM section (radio: None/Upload/Associate); file input for SCORM ZIP; dropdown for existing SCORM
│   ├── Create.cshtml.cs   # Modified: inject CourseCatalogService + ScormPackageService; handle SCORM upload/association on create
│   ├── Edit.cshtml        # NEW: edit form for course details + SCORM management (view current, upload new)
│   └── Edit.cshtml.cs     # NEW: OnGet (load course + SCORM status), OnPost (save changes + optional SCORM upload)
├── Pages/Admin/Upload.cshtml      # Modified: remove course dropdown; add SCORM pool list with delete; upload to available pool (no CourseId)
├── Pages/Admin/Upload.cshtml.cs   # Modified: remove course selection; add ListAvailableAsync + delete handler; upload without courseId
├── wwwroot/css/site.css           # Modified: table contrast fix + SCORM status badge styling

src/Modules/Catalog/
├── Application/
│   └── CourseCatalogService.cs  # Modified: add UpdateAsync method
├── Endpoints/
│   ├── CreateCourseRequest.cs   # Modified: add optional ScormPackageId field for association
│   └── UpdateCourseRequest.cs   # NEW: request DTO for course updates

src/Modules/Scorm/
├── Domain/
│   └── ScormPackage.cs          # Modified: CourseId becomes nullable (Guid?)
├── Application/
│   └── ScormPackageService.cs   # Modified: add ListAvailableAsync, AssociateWithCourseAsync, ReplacePackageAsync; UpdateAsync handles null courseId
├── Infrastructure/
│   └── ScormDbContext.cs        # Modified: CourseId index becomes filtered unique (non-null only)
└── Endpoints/
    └── (upload endpoint modified to accept nullable courseId)

src/Modules/Management/
└── Application/
    └── CourseVisibilityService.cs  # Modified: fix GetAllCoursesAsync to resolve org names

src/Host/Migrations/Scorm/
└── AddScormPackageNullableCourseId.cs  # NEW: EF migration for nullable CourseId + filtered index

tests/
└── (E2E tests added during implementation)
```

**Structure Decision**: Changes follow the existing modular layout. Host pages use direct DI into Catalog, Scorm, and Management services (no HTTP calls). The Catalog module gains one new method (`UpdateAsync`) and one new DTO (`UpdateCourseRequest`). The Scorm module gains methods for listing available packages, associating with a course, and replacing packages. A database migration makes `ScormPackage.CourseId` nullable with a filtered unique index.

## Complexity Tracking

Not applicable — no Constitution violations to justify.
