# Implementation Plan: Course Browse Search, Filter, and Pagination

**Branch**: `story/019-course-search-pagination` | **Date**: 2025-07-31 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features.

**Input**: Feature specification from `/specs/019-course-search-pagination/spec.md`

## Summary

Fix the broken search box and category filter on the Browse Courses Razor Page, and add server-side pagination. Search and pagination will be implemented as T-SQL stored procedures using SQL Server Full-Text Search (FTS) for tokenized, language-aware course title search, combined with `OFFSET/FETCH` for efficient pagination. The existing HTMX partial-update pattern will be extended to handle pagination controls. The HTMX parameter binding bug (filters not sending all parameters) will be fixed with `hx-include`.

## Technical Context

**Language/Version**: C# / .NET 10.0 (GA, pinned via `global.json`)

**Primary Dependencies**: ASP.NET Core Razor Pages, EF Core 10.0 (SqlServer provider), HTMX 2.0.4 (CDN), xUnit (testing)

**Storage**: MSSQL Server 2022 (docker compose service `mssql`) — Courses table in `CatalogDbContext`

**Testing**: xUnit with `Microsoft.NET.Test.Sdk`; existing `tests/Catalog.Tests/` project uses in-memory or integration patterns

**Target Platform**: Linux server (Docker container via `.devcontainer`)

**Project Type**: Web application (modular monolith — ASP.NET Core minimal APIs + Razor Pages)

**Performance Goals**: Search/filter results visible within 500ms of user input; pagination within 1 second; scale to 10,000+ courses

**Constraints**: T-SQL for search and pagination (user mandate); HTMX for partial-page updates; EF Core DbContext is the repository (Constitution II); module boundaries enforced by ArchitectureTests (Constitution III)

**Scale/Scope**: Single page (Browse Courses) with one new stored procedure, one new FTS index, and pagination UI. No changes to other pages or modules.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ Pass | All changes stay within Catalog module + Host (Razor Pages). No cross-module boundary crossed. |
| II. Clean Architecture | ✅ Pass | T-SQL stored procedure called from `CourseCatalogService` (Application layer) via EF Core `FromSqlRaw()`. Domain unchanged. |
| III. Module Boundaries Compiled | ✅ Pass | No new cross-module references. Host already references Catalog directly. |
| IV. Human-Legible Code | ✅ Pass | Stored procedure is self-documenting T-SQL. ADR not needed — pattern follows existing catalog queries. |
| V. Sandbox Not Optional | ✅ Pass | All work inside `.devcontainer`. |
| VI. Polyglot Storage | ✅ Pass | MSSQL remains system of record. No new Valkey usage. |
| VII. Spec-Driven, Sliced Thin | ✅ Pass | Vertical slice: fix search + filter + pagination on one page. |
| VIII. Branching Discipline | ✅ Pass | Branch: `story/019-course-search-pagination`. |
| IX. Plan On Master Only | ✅ Pass | Planning running on `master`. |
| X. No Ad-Hoc Fixes | ✅ Pass | Spec exists at `specs/019-course-search-pagination/spec.md`. |

**Post-Design Re-Check**: All principles still pass. The T-SQL stored procedure approach is an infrastructure concern within the Catalog module's `Infrastructure` layer — it does not introduce new abstractions or cross boundaries.

## Project Structure

### Documentation (this feature)

```text
specs/019-course-search-pagination/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 research decisions
├── data-model.md        # Phase 1 data model
├── quickstart.md        # Phase 1 validation guide
├── contracts/
│   └── browse-courses-htmx.md  # HTMX endpoint contract
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code Changes

```text
src/
├── Modules/
│   └── Catalog/
│       ├── Application/
│       │   └── CourseCatalogService.cs      # ADD: BrowseAsync() method with pagination params
│       └── Infrastructure/
│           ├── CatalogDbContext.cs           # UNCHANGED (raw SQL via Database property)
│           ├── CatalogSeeder.cs              # UNCHANGED
│           └── Migrations/                   # ADD: EF migration for FTS index creation
│               └── 20250731_AddFullTextIndexAndBrowseProcedure.cs
├── Host/
│   └── Pages/
│       ├── Courses/
│       │   └── Index.cshtml                  # UPDATE: Fix HTMX params, add pagination partial
│       │   └── Index.cshtml.cs               # UPDATE: Add PageNumber/PageSize, call BrowseAsync
│       └── Shared/
│           ├── _CourseList.cshtml            # UPDATE: Include pagination controls
│           └── _Pagination.cshtml            # ADD: Reusable pagination partial

tests/
└── Catalog.Tests/
    └── CourseCatalogSearchTests.cs           # ADD: Integration tests for BrowseAsync
```

**Structure Decision**: Changes are scoped to three areas:
1. **Catalog.Infrastructure** — T-SQL stored procedure + FTS index (database-level, no new C# files)
2. **Catalog.Application** — `CourseCatalogService.BrowseAsync()` method (one new method on existing class)
3. **Host.Pages** — Razor Page model update + new `_Pagination.cshtml` partial (UI layer)

No new modules, no new contracts, no new cross-module dependencies. The stored procedure is created via an EF Core migration's `Up()` method using raw SQL execution.

## Complexity Tracking

Not applicable — no constitution violations. All changes follow established patterns.
