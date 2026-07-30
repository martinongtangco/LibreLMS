# Implementation Plan: Rebrand to Libre LMS

**Branch**: `story/008-rebrand-libre-lms` | **Date**: 2025-07-30 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/008-rebrand-libre-lms`.

**Input**: Feature specification from `/specs/008-rebrand-libre-lms/spec.md`

## Summary

Rename the application from "Learning LMS" to "Libre LMS" across all user-facing surfaces (UI, documentation, configuration) and all internal identifiers (C# namespaces, solution file, database name). No functional behavior changes — this is a purely cosmetic and organizational rebrand.

## Technical Context

**Language/Version**: C# / .NET 10 (GA)

**Primary Dependencies**: ASP.NET Core minimal APIs, EF Core, Razor Pages, HTMX

**Storage**: MSSQL (database name change: `LearningLms` → `LibreLms`); Valkey (no change)

**Testing**: dotnet test (xUnit-based unit tests, NetArchTest-based architecture tests)

**Target Platform**: Linux server (Docker devcontainer)

**Project Type**: Web application (modular monolith)

**Performance Goals**: No change — rebrand has zero performance impact

**Constraints**: Must preserve all existing functionality; migration snapshots retain original namespaces for EF Core compatibility; git history and completed spec slices (001–007) are left unchanged

**Scale/Scope**: ~60 source files with namespace changes; 4 documentation files; 1 solution file; 1 layout template; 1 database config

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ Pass | No structural changes to module boundaries |
| II. Clean Architecture | ✅ Pass | No architectural layer changes |
| III. Compiled Boundaries | ✅ Pass | Module boundary rules unchanged; ArchitectureTests updated to use new namespace |
| IV. Human-Legible Code | ✅ Pass | Simple rename operations; no clever patterns needed |
| V. Sandbox | ✅ Pass | All work stays in devcontainer |
| VI. Polyglot Storage | ✅ Pass | Database name change only; no storage model changes |
| VII. Spec-Driven | ✅ Pass | Following spec → plan → tasks → implement flow |
| VIII. Branching | ✅ Pass | Dedicated branch `story/008-rebrand-libre-lms` |
| IX. No Ad-Hoc Fixes | ✅ Pass | This change follows the full SpecKit workflow |

**Verdict**: No violations. All principles respected. Proceed to implementation.

## Project Structure

### Documentation (this feature)

```text
specs/008-rebrand-libre-lms/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code — Files Affected by Rebrand

```text
# Solution file
LearningLms.slnx                    → rename to LibreLms.slnx

# Host project (namespaces + UI branding)
src/Host/Program.cs                 → namespace: LearningLms → LibreLms
src/Host/ScormHelpers.cs            → namespace: LearningLms → LibreLms
src/Host/appsettings.Development.json → DB name: LearningLms → LibreLms
src/Host/Pages/Shared/_Layout.cshtml → "Learning LMS" → "Libre Lms" (3 instances)
src/Host/Pages/Account/Login.cshtml.cs     → namespace rename
src/Host/Pages/Account/Logout.cshtml.cs    → namespace rename
src/Host/Pages/Admin/Courses/Create.cshtml.cs → namespace rename
src/Host/Pages/Admin/Upload.cshtml.cs      → namespace rename
src/Host/Pages/Courses/Detail.cshtml.cs    → namespace rename
src/Host/Pages/Courses/Index.cshtml.cs     → namespace rename
src/Host/Pages/Error.cshtml.cs             → namespace rename
src/Host/Pages/Error.cshtml                → @using rename
src/Host/Pages/MyCourses/Index.cshtml.cs   → namespace rename
src/Host/Pages/Scorm/Launch.cshtml         → page title rename
src/Host/Pages/Scorm/Launch.cshtml.cs      → namespace rename

# SharedKernel (namespace only)
src/SharedKernel/Entity.cs          → namespace rename
src/SharedKernel/IDomainEvent.cs    → namespace rename
src/SharedKernel/Result.cs          → namespace rename

# Catalog Module
src/Modules/Catalog/                → all .cs files: namespace rename
src/Modules/Catalog.Contracts/      → all .cs files: namespace rename

# Enrollment Module
src/Modules/Enrollment/             → all .cs files: namespace rename
src/Modules/Enrollment.Contracts/   → all .cs files: namespace rename

# Scorm Module
src/Modules/Scorm/                  → all .cs files: namespace rename
src/Modules/Scorm.Contracts/        → all .cs files: namespace rename

# Tests
tests/ArchitectureTests/            → namespace rename
tests/Catalog.Tests/                → namespace rename
tests/Enrollment.Tests/             → namespace rename
tests/Scorm.Tests/                  → namespace rename

# Documentation
README.md                           → "Learning LMS" → "Libre LMS"
.specify/memory/constitution.md     → "Learning LMS" → "Libre LMS"

# EXCLUDED (retain LearningLms for compatibility)
src/Host/Migrations/                → all migration files unchanged
specs/001-* through specs/007-*     → historical spec files unchanged
.git/                               → git history unchanged
```

**Structure Decision**: The existing modular monolith structure is preserved entirely. The rebrand touches namespace declarations, string literals, the solution filename, and configuration values — but does not reorganize any directories, rename any project files (.csproj), or change any module boundaries.

## Complexity Tracking

Not applicable — no constitution violations to justify.
