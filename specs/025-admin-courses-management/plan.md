# Implementation Plan: Admin Courses Management Overhaul

**Branch**: `bug/025-admin-courses-management` | **Date**: 2025-08-11 | **Spec**: [spec.md](./spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects.

**Input**: Feature specification from `/specs/025-admin-courses-management/spec.md`

## Summary

Fix five issues in the Admin/Courses page: (1) add a "Create Course" button and fix the broken create flow, (2) add an Edit page with a new `UpdateAsync` service method, (3) fix the delete flow by resolving org names in `GetAllCoursesAsync`, (4) add search, category filter, column sorting, and pagination reusing the existing `BrowseAsync` stored procedure pattern, and (5) improve table contrast by wrapping in a card surface and adding alternating row colors.

All changes are confined to the Host project's Razor Pages and the Catalog module's `CourseCatalogService`. No new modules or database migrations are needed.

## Technical Context

**Language/Version**: C# / .NET 10 (GA), ASP.NET Core minimal APIs + Razor Pages

**Primary Dependencies**: EF Core (MSSQL), HTMX (for partial page updates), StackExchange.Redis (Valkey — not used in this feature)

**Storage**: MSSQL via EF Core `CatalogDbContext` (Courses table) and `ManagementDbContext` (CourseVisibilityOverrides table)

**Testing**: xUnit (unit tests), Playwright (E2E tests), NetArchTest (architecture tests)

**Target Platform**: Linux server (dev container), web portal

**Project Type**: Web application (Razor Pages + minimal API endpoints)

**Performance Goals**: Pagination loads within 1 second for 100+ courses (using stored procedure); CRUD operations complete within 2 seconds

**Constraints**: Module boundaries enforced by ArchitectureTests; no cross-module references except through `*.Contracts`; admin pages use direct service injection (not HTTP client)

**Scale/Scope**: Single-page refactor + one new page (Edit). ~5 files modified, ~2 files created.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | PASS | Changes stay within Host (Razor Pages) and Catalog module; no new module boundaries crossed |
| II. Clean Architecture | PASS | `UpdateAsync` added to `CourseCatalogService` (Application layer); Edit page uses direct DI |
| III. Module Boundaries | PASS | Host references Catalog through `CourseCatalogService` (registered in DI); no Contracts project change needed |
| IV. Human-Legible Code | PASS | Changes follow existing patterns (direct DI, Razor Pages, HTMX pagination) |
| V. Sandbox Not Optional | N/A | Implementation happens in dev container |
| VI. Polyglot Storage | PASS | No Valkey changes; only MSSQL via EF Core |
| VII. Spec-Driven | PASS | This plan follows from spec 025 |
| VIII. Branching Discipline | PASS | Branch: `bug/025-admin-courses-management` |
| IX. Plan On Master | PASS | Planning on master branch |
| X. No Ad-Hoc Fixes | PASS | Spec created before any code changes |
| XI. Parallel Implementation | N/A | Applied during `/speckit.implement` |
| XII. Return to Master | N/A | Applied after implementation |
| XIII. Verification Before Claim | N/A | Applied during implementation (build + E2E tests) |

**Post-Design Re-Evaluation**: All gates still pass. No violations introduced.

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
│   ├── Index.cshtml       # Modified: add Create button, search/filter, sorting, pagination, card wrapper
│   ├── Index.cshtml.cs    # Modified: add search, filter, sort, pagination query params; use BrowseAsync
│   ├── Create.cshtml      # Minor: fix redirect to /Admin/Courses
│   ├── Create.cshtml.cs   # Modified: inject CourseCatalogService directly instead of HttpClient
│   ├── Edit.cshtml        # NEW: edit form for course details
│   └── Edit.cshtml.cs     # NEW: OnGet (load course), OnPost (save changes)
├── wwwroot/css/site.css   # Modified: table contrast fix (card wrapper, alternating rows)

src/Modules/Catalog/
├── Application/
│   └── CourseCatalogService.cs  # Modified: add UpdateAsync method
└── Endpoints/
    └── UpdateCourseRequest.cs   # NEW: request DTO for course updates

src/Modules/Management/
└── Application/
    └── CourseVisibilityService.cs  # Modified: fix GetAllCoursesAsync to resolve org names

tests/
└── (E2E tests added during implementation)
```

**Structure Decision**: Changes follow the existing modular layout. Host pages use direct DI into Catalog and Management services (no HTTP calls). The Catalog module gains one new method (`UpdateAsync`) and one new DTO (`UpdateCourseRequest`). No new modules or Contracts changes needed.

## Complexity Tracking

Not applicable — no Constitution violations to justify.
