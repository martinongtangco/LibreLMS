# Research: Rebrand to Libre LMS

**Created**: 2025-07-30
**Feature**: [spec.md](spec.md)

## Scope of Changes

A full-text scan of the repository identified all locations where "Learning LMS" or "LearningLms" appears. The results are categorized below.

### User-Facing Branding (UI)

| File | Reference | Change |
|------|-----------|--------|
| `src/Host/Pages/Shared/_Layout.cshtml` | `<title>@ViewData["Title"] - Learning LMS</title>` | → "Libre LMS" |
| `src/Host/Pages/Shared/_Layout.cshtml` | `<span class="brand">Learning LMS</span>` | → "Libre LMS" |
| `src/Host/Pages/Shared/_Layout.cshtml` | `&copy; 2025 Learning LMS` | → "Libre LMS" |
| `src/Host/Pages/Scorm/Launch.cshtml` | `<title>Error - Learning LMS</title>` | → "Libre LMS" |

### Internal Namespaces (C# source files)

All `namespace LearningLms.*` declarations across ~60 files must be renamed to `namespace LibreLms.*`. This includes:

- **Host project**: Program.cs, ScormHelpers.cs, all Pages/*.cshtml.cs
- **SharedKernel**: Entity.cs, IDomainEvent.cs, Result.cs
- **Catalog module**: all files in Application/, Domain/, Endpoints/, Infrastructure/, and ModuleMarker.cs
- **Catalog.Contracts**: all files
- **Enrollment module**: all files in Application/, Domain/, Endpoints/, Infrastructure/, and ModuleMarker.cs
- **Enrollment.Contracts**: all files
- **Scorm module**: all files in Application/, Domain/, Endpoints/, Infrastructure/, and ModuleMarker.cs
- **Scorm.Contracts**: all files
- **Tests**: ArchitectureTests, Catalog.Tests, Enrollment.Tests, Scorm.Tests

### Configuration Files

| File | Reference | Change |
|------|-----------|--------|
| `src/Host/appsettings.Development.json` | `Database=LearningLms` | → `Database=LibreLms` |

### Solution File

| File | Change |
|------|--------|
| `LearningLms.slnx` | Rename to `LibreLms.slnx` |

### Documentation

| File | References | Change |
|------|-----------|--------|
| `README.md` | Multiple instances of "Learning LMS" and "LearningLms" | → "Libre LMS" / "LibreLms" |
| `.specify/memory/constitution.md` | Title and body references | → "Libre LMS" |

### Excluded Files (no changes)

- **Migration snapshots** (`src/Host/Migrations/`): Retain original `LearningLms` namespaces to preserve EF Core migration chain compatibility
- **Historical specs** (`specs/001-*` through `specs/007-*`): Leave as-is for audit trail
- **Git history**: No rewrites

## Decisions

| Decision | Rationale | Alternatives considered |
|----------|-----------|------------------------|
| Keep migration snapshots unchanged | EF Core migrations depend on exact type names; changing them breaks the migration chain | Regenerate all migrations from scratch — too risky for a cosmetic change |
| Keep historical spec files unchanged | They document the decisions made during prior slices; altering them would corrupt the record | Rename them — unnecessary work with no value |
| Rename solution file | The solution file name is the top-level identifier for the project | Keep the solution file name — creates inconsistency |
| Update database name in config | The DB name is part of the application identity | Keep DB name — creates confusion in dev environments |
