# Implementation Plan: Course Catalog & Enrollment

**Branch**: `001-course-catalog-enrollment` | **Date**: 2025-07-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-course-catalog-enrollment/spec.md`

## Summary

Students browse a course catalog, view course details, enroll in courses, and see their enrolled courses. Implemented as two module implementations (`Catalog` and `Enrollment`) within the existing modular monolith, using .NET 10, EF Core against MSSQL, and Razor Pages for the web portal. The Catalog module owns course data; the Enrollment module owns student/enrollment data and depends on `Catalog.Contracts` for course validation.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.103, pinned in `global.json`)

**Primary Dependencies**: ASP.NET Core minimal APIs + Razor Pages, EF Core (MSSQL provider), xUnit (tests)

**Storage**: MSSQL 2022 (via EF Core Code-First migrations)

**Testing**: xUnit with `Microsoft.NET.Test.Sdk`; NetArchTest for architecture boundary enforcement

**Target Platform**: Linux server inside devcontainer (Docker Compose)

**Project Type**: Web application (modular monolith, one deployable process)

**Performance Goals**: Sub-500ms page loads for catalog browsing; sub-1s enrollment confirmation

**Constraints**: Modular monolith (Principle I), compiled module boundaries (Principle III), no MediatR/CQRS (Principle II), spec-driven workflow (Principle VII)

**Scale/Scope**: Learning project — small number of seeded courses (10-20), single-user demo scenario. No horizontal scaling needed.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ Pass | Catalog and Enrollment as separate modules, single Host process |
| II. Clean Architecture (Simple) | ✅ Pass | EF Core directly, no MediatR, no repository abstraction |
| III. Compiled Boundaries | ✅ Pass | Enrollment depends on Catalog.Contracts only; ArchitectureTests enforces |
| IV. Human-Legible Code | ✅ Pass | All decisions documented in ADRs and this plan |
| V. Sandbox | ✅ Pass | All work inside devcontainer |
| VI. Polyglot Storage | ✅ Pass | Only MSSQL used; Valkey not needed for this slice |
| VII. Spec-Driven | ✅ Pass | Spec → Plan → Tasks → Implement workflow followed |

### Post-Design Re-Check (after Phase 1)

All gates remain passing. No constitution violations introduced by the design. The Enrollment module's dependency on `Catalog.Contracts` is an explicit, justified cross-module boundary that the architecture tests verify.

## Project Structure

### Documentation (this feature)

```text
specs/001-course-catalog-enrollment/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0: decisions and clarifications
├── data-model.md        # Phase 1: entities and relationships
├── quickstart.md        # Phase 1: validation guide
├── contracts/
│   └── api.md           # Phase 1: API endpoint contracts
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
src/
├── Host/
│   ├── Program.cs                  # Composition root, maps all module endpoints
│   ├── Host.csproj                 # References all modules + contracts
│   └── appsettings*.json           # Connection strings, logging config
│
├── SharedKernel/
│   ├── Entity.cs                   # Entity<TId> base class
│   ├── Result.cs                   # Result/Result<T> pattern
│   └── IDomainEvent.cs             # Domain event marker interface
│
├── Modules/
│   ├── Catalog/
│   │   ├── Domain/
│   │   │   └── Course.cs           # Course entity (extends Entity<Guid>)
│   │   ├── Application/
│   │   │   ├── CourseCatalogService.cs    # Browse, search, detail
│   │   │   └── CourseLookup.cs            # Implements ICourseLookup contract
│   │   ├── Infrastructure/
│   │   │   ├── CatalogDbContext.cs   # EF Core DbContext for courses
│   │   │   └── CatalogSeeder.cs      # Seed data (sample courses)
│   │   ├── Endpoints/
│   │   │   └── CatalogEndpoints.cs   # GET /api/courses, GET /api/courses/{id}
│   │   ├── Catalog.csproj
│   │   └── ModuleMarker.cs
│   │
│   ├── Catalog.Contracts/
│   │   ├── CourseSummary.cs         # DTO: Id, Title
│   │   ├── ICourseLookup.cs         # Interface for cross-module access
│   │   ├── Catalog.Contracts.csproj
│   │   └── ModuleMarker.cs
│   │
│   ├── Enrollment/
│   │   ├── Domain/
│   │   │   ├── Student.cs            # Student entity
│   │   │   └── Enrollment.cs         # Enrollment entity
│   │   ├── Application/
│   │   │   └── EnrollmentService.cs  # Create enrollment, list enrollments
│   │   ├── Infrastructure/
│   │   │   ├── EnrollmentDbContext.cs  # EF Core DbContext
│   │   │   └── EnrollmentSeeder.cs     # Seed test students
│   │   ├── Endpoints/
│   │   │   └── EnrollmentEndpoints.cs  # POST /api/enrollments, GET /api/enrollments/my
│   │   ├── Enrollment.csproj
│   │   └── ModuleMarker.cs
│   │
│   ├── Enrollment.Contracts/
│   │   ├── Enrollment.Contracts.csproj
│   │   └── ModuleMarker.cs
│   │
│   └── Scorm/                      # Not touched in this slice
│
tests/
├── ArchitectureTests/
│   └── ModuleBoundaryTests.cs      # Verifies cross-module boundaries
├── Catalog.Tests/
│   └── (unit tests for Catalog module)
└── Enrollment.Tests/
    └── (unit tests for Enrollment module)
```

**Structure Decision**: The existing modular monolith layout is preserved. Each module follows Domain → Application → Infrastructure → Endpoints folder structure within a single `.csproj` (per constitution Principle II). The Enrollment module references `Catalog.Contracts` (the only allowed cross-module dependency) and `SharedKernel`. Razor Pages for the web portal are mapped alongside minimal APIs in the Host's `Program.cs`.

## Complexity Tracking

No constitution violations. No complexity tracking needed.
