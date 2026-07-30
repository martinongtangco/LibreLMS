# Implementation Plan: RBAC Management Portal

**Branch**: `story/009-rbac-management-portal` | **Date**: 2025-07-31 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/009-rbac-management-portal/spec.md`

## Summary

Introduce a hierarchical organization system with role-based access control (SuperUser, Organization Admin, Learner) and a management portal with dashboards. Organizations form a tree rooted at a single top-level entity. Courses cascade down from parent to child organizations by default, with opt-out hiding. Organization Admins can manage learners, sub-organizations, and courses within their organizational subtree. The existing modular monolith architecture is extended with a new `Management` module, and existing `Student` and `Course` entities are augmented with organizational context.

## Technical Context

**Language/Version**: C# / .NET 10 (GA, pinned via global.json)

**Primary Dependencies**: ASP.NET Core minimal APIs + Razor Pages, EF Core (MSSQL), StackExchange.Redis (Valkey), NetArchTest (architecture guardrails)

**Storage**: MSSQL for all durable data (organizations, users, courses, enrollments, visibility overrides). Valkey for SCORM runtime state only (existing pattern).

**Testing**: xUnit (existing), integration tests per module, ArchitectureTests (NetArchTest for module boundary enforcement)

**Target Platform**: Linux server (containerized via devcontainer + docker-compose)

**Project Type**: Modular monolith web application (ASP.NET Core + Razor Pages)

**Performance Goals**: Dashboard renders within 3 seconds for organizations with up to 1,000 learners; SCORM upload processing within 2 minutes

**Constraints**: Module boundaries enforced by ArchitectureTests (Constitution Principle III). Dependencies point inward: Domain → Application → Infrastructure → Endpoints. Cross-module access only through *.Contracts namespaces.

**Scale/Scope**: Organization hierarchies up to 10 levels deep; bulk enrollment up to 500 learners per batch; single root organization with potentially hundreds of descendant orgs.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | PASS | New `Management` module follows existing pattern (Catalog, Enrollment, Scorm) |
| II. Clean Architecture | PASS | Domain → Application → Infrastructure → Endpoints; no MediatR/CQRS/repo wrapper |
| III. Module Boundaries Compiled | PASS | New `Management.Contracts` for cross-module interfaces; ArchitectureTests updated |
| IV. Human-Legible Code | PASS | ADRs for organizational model and RBAC design; explicit control flow |
| V. Sandbox Not Optional | N/A | Development workflow concern, not design |
| VI. Polyglot Storage | PASS | MSSQL for durable data; no new Valkey usage |
| VII. Spec-Driven, Sliced Thin | PASS | Feature went through /speckit.specify first |
| VIII. Branching Discipline | PASS | Branch: `story/009-rbac-management-portal` |
| IX. Plan On Master Only | CHECK | Must verify branch before proceeding |
| X. No Ad-Hoc Fixes | PASS | This change is documented via SpecKit workflow |

## Project Structure

### Source Code (repository root)

```text
src/
├── Host/
│   ├── Pages/
│   │   ├── Admin/
│   │   │   ├── Organizations/          # New: org management pages
│   │   │   │   ├── Index.cshtml        # Org tree view
│   │   │   │   ├── Create.cshtml       # Create org
│   │   │   │   └── Edit.cshtml         # Edit org
│   │   │   ├── Learners/               # New: learner management pages
│   │   │   │   ├── Index.cshtml        # Learner list (scoped)
│   │   │   │   ├── Create.cshtml       # Create learner
│   │   │   │   └── Edit.cshtml         # Edit learner
│   │   │   ├── Courses/                # Existing + enhanced
│   │   │   │   ├── Index.cshtml        # Course list (scoped)
│   │   │   │   ├── Create.cshtml       # Existing
│   │   │   │   └── Upload.cshtml       # SCORM upload (existing, enhanced for org)
│   │   │   ├── Enrollments/            # New: enrollment management
│   │   │   │   ├── Index.cshtml        # Enrollment list
│   │   │   │   └── BulkEnroll.cshtml   # Bulk enrollment
│   │   │   └── Dashboard/              # New: admin dashboards
│   │   │       └── Index.cshtml        # Role-scoped dashboard
│   │   ├── Account/                    # Existing, enhanced for org context
│   │   └── Shared/                     # Existing partials
│   ├── ManagementAuth/                 # New: RBAC middleware + policies
│   └── Program.cs                      # Enhanced: Management module registration
├── Modules/
│   ├── Management/                     # NEW module
│   │   ├── Domain/
│   │   │   ├── Organization.cs         # Org entity (hierarchical)
│   │   │   ├── UserRole.cs             # Role enum/type
│   │   │   └── CourseVisibilityOverride.cs  # Hide inherited courses
│   │   ├── Application/
│   │   │   ├── OrganizationService.cs  # Org CRUD + tree operations
│   │   │   ├── UserService.cs          # User CRUD + role management
│   │   │   ├── EnrollmentService.cs    # Enrollment management (admin-facing)
│   │   │   └── DashboardService.cs     # Metrics aggregation
│   │   ├── Infrastructure/
│   │   │   ├── ManagementDbContext.cs  # EF Core context
│   │   │   ├── Repositories/           # Data access
│   │   │   └── ManagementSeeder.cs     # Seed root org + SuperUser
│   │   └── Endpoints/
│   │       ├── OrganizationEndpoints.cs
│   │       ├── UserEndpoints.cs
│   │       ├── EnrollmentEndpoints.cs
│   │       └── DashboardEndpoints.cs
│   ├── Management.Contracts/           # NEW contracts
│   │   ├── IOrganizationLookup.cs      # Cross-module org lookup
│   │   ├── IUserInfoLookup.cs          # Cross-module user/org lookup
│   │   ├── OrganizationSummary.cs      # DTO
│   │   └── ModuleMarker.cs
│   ├── Catalog/                        # EXISTING — enhanced
│   │   ├── Domain/Course.cs            # Add OrganizationId
│   │   ├── Application/CourseCatalogService.cs  # Add org-scoped queries
│   │   └── Infrastructure/             # Migration + org filter
│   ├── Enrollment/                     # EXISTING — enhanced
│   │   ├── Domain/Student.cs           # Add OrganizationId
│   │   ├── Application/EnrollmentService.cs  # Add org scope checks
│   │   └── Infrastructure/             # Migration + org filter
│   └── Scorm/                          # EXISTING — no domain changes
├── SharedKernel/                       # EXISTING — no changes

tests/
├── ArchitectureTests/                  # Updated: add Management module
│   └── ModuleBoundaryTests.cs
├── Management.Tests/                   # NEW
│   ├── Domain/
│   ├── Application/
│   └── Integration/
└── Catalog.Tests/ & Enrollment.Tests/  # EXISTING — updated for org context
```

**Structure Decision**: New `Management` module follows the exact same Clean Architecture pattern as Catalog, Enrollment, and Scorm. Existing Catalog and Enrollment modules are augmented with `OrganizationId` on their key entities. The Host project gains management portal Razor Pages and RBAC middleware. Management.Contracts exposes IOrganizationLookup and IUserInfoLookup for cross-module access to organizational context.

## Phase 0 Research Findings

See [research.md](research.md) for detailed research outcomes.

**Key decisions resolved**:
- Organization hierarchy: Adjacency list pattern (ParentId FK on Organization table) — simplest for tree structures up to 10 levels, no recursive CTE complexity
- RBAC enforcement: ASP.NET Core Authorization with custom `RequireOrgScope` policy — reuses framework infrastructure, no custom auth layer needed
- Student→User rename: Keep `Student` entity name but add role enum; "Learner" is the business term, "Student" is the domain entity (consistent with existing code)
- Course visibility: Materialized path for inheritance — each course has OrganizationId; queries traverse ancestors via recursive CTE or application-level traversal
- Dashboard metrics: Direct SQL queries via EF Core RawSQL for aggregation — dashboard reads are performance-critical, avoid N+1 with raw queries

## Phase 1 Design Artifacts

See [data-model.md](data-model.md), [contracts/](contracts/), and [quickstart.md](quickstart.md).

## Constitution Check (Post-Design Re-Evaluation)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | PASS | Management module is a new vertical; no microservice split |
| II. Clean Architecture | PASS | DbContext is the repository; no extra abstraction layers |
| III. Module Boundaries Compiled | PASS | Management.Contracts for cross-module access; ArchitectureTests updated to include Management |
| IV. Human-Legible Code | PASS | ADR drafted for org hierarchy and RBAC policy design |
| VI. Polyglot Storage | PASS | All new data in MSSQL; no Valkey changes |
| VII. Spec-Driven, Sliced Thin | PASS | Planning complete, ready for /speckit.tasks |

## Complexity Tracking

No constitution violations — all design decisions align with existing patterns.
