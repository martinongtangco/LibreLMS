# Implementation Plan: SCORM Launch & Completion

**Branch**: `002-scorm-launch-completion` | **Date**: 2025-07-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-scorm-launch-completion/spec.md`

## Summary

Students launch SCORM 1.2 courses, track progress during interactive sessions, commit completion results, and resume from checkpoints. Implemented as the `Scorm` module within the existing modular monolith, using .NET 10, EF Core against MSSQL for durable records, and StackExchange.Redis against Valkey for live session state. The Scorm module depends on `Catalog.Contracts` (course validation) and a new `IEnrollmentLookup` in `Enrollment.Contracts` (enrollment validation). SCORM content is served as static files from extracted ZIP packages, with a JavaScript API shim enabling standard SCORM 1.2 content communication.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.103, pinned in `global.json`)

**Primary Dependencies**: ASP.NET Core minimal APIs + Razor Pages, EF Core (MSSQL provider), StackExchange.Redis (Valkey client), System.IO.Compression (ZIP extraction), System.Xml.Linq (manifest parsing), xUnit (tests)

**Storage**: MSSQL 2022 (ScormPackage, CourseAttempt tables via EF Core Code-First) + Valkey (live `cmi.*` session bags with 30-min TTL) + filesystem (extracted SCORM content under `wwwroot/scorm-content/`)

**Testing**: xUnit with `Microsoft.NET.Test.Sdk`; NetArchTest for architecture boundary enforcement

**Target Platform**: Linux server inside devcontainer (Docker Compose)

**Project Type**: Web application (modular monolith, one deployable process)

**Performance Goals**: Sub-3s SCORM content launch (SC-001); sub-500ms SCORM API call response (SC-002); sub-1s commit durability (SC-003)

**Constraints**: Modular monolith (Principle I), compiled module boundaries (Principle III), no MediatR/CQRS (Principle II), SCORM 1.2 simplified only, spec-driven workflow (Principle VII)

**Scale/Scope**: Learning project — small number of SCORM packages (5-10), single-user demo scenario. Session TTL 30 minutes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design below.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ Pass | Scorm module added to existing Host process, no new services |
| II. Clean Architecture (Simple) | ✅ Pass | EF Core directly, StackExchange.Redis directly, no extra abstractions |
| III. Compiled Boundaries | ✅ Pass | Scorm depends on Catalog.Contracts + new Enrollment.Contracts interface; ArchitectureTests extended |
| IV. Human-Legible Code | ✅ Pass | SCORM decisions documented in research.md and ADRs |
| V. Sandbox | ✅ Pass | All work inside devcontainer |
| VI. Polyglot Storage | ✅ Pass | MSSQL for durable records, Valkey for live session state (ADR-0003), filesystem for content |
| VII. Spec-Driven | ✅ Pass | Spec → Plan → Tasks → Implement workflow followed |

### Post-Design Re-Check (after Phase 1)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ Pass | Scorm module follows existing pattern |
| II. Clean Architecture (Simple) | ✅ Pass | Direct DbContext and Redis usage, no repository wrappers |
| III. Compiled Boundaries | ✅ Pass | New `IEnrollmentLookup` contract added; ArchitectureTests verify Scorm→Enrollment.Contracts only |
| IV. Human-Legible Code | ✅ Pass | research.md documents 8 technical decisions with rationale |
| V. Sandbox | ✅ Pass | Unchanged |
| VI. Polyglot Storage | ✅ Pass | Three stores used with clear rationale: MSSQL (durable), Valkey (ephemeral session), filesystem (static content) |
| VII. Spec-Driven | ✅ Pass | All phases complete |

## Project Structure

### Documentation (this feature)

```text
specs/002-scorm-launch-completion/
├── spec.md              # Feature specification (with clarifications)
├── plan.md              # This file
├── research.md          # Phase 0: 8 technical decisions documented
├── data-model.md        # Phase 1: 3 entities + 1 new contract interface
├── quickstart.md        # Phase 1: 8 validation scenarios
├── contracts/
│   └── api.md           # Phase 1: 8 endpoints + 1 cross-module contract
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
src/
├── Host/
│   ├── Program.cs                    # Add ScormDbContext, ScormModule, StackExchange.Redis
│   ├── Pages/
│   │   └── Scorm/
│   │       └── Launch.cshtml(.cs)    # SCORM wrapper page (injects API script + beforeunload)
│   └── wwwroot/
│       └── scorm-content/            # Extracted SCORM package content (created at runtime)
│
├── Modules/
│   ├── Enrollment.Contracts/
│   │   └── IEnrollmentLookup.cs      # NEW: IsEnrolledAsync(studentId, courseId)
│   │
│   ├── Enrollment/
│   │   └── Application/
│   │       └── EnrollmentLookup.cs   # NEW: Implements IEnrollmentLookup
│   │
│   ├── Scorm/
│   │   ├── Domain/
│   │   │   ├── ScormPackage.cs       # Package entity (extends Entity<Guid>)
│   │   │   └── CourseAttempt.cs      # Attempt entity (extends Entity<Guid>)
│   │   ├── Application/
│   │   │   ├── ScormSessionService.cs      # Session lifecycle (init, setValue, getValue, commit, finish)
│   │   │   ├── ScormPackageService.cs      # Upload, parse manifest, extract ZIP
│   │   │   └── ScormAttemptService.cs      # Attempt management, resume data
│   │   ├── Infrastructure/
│   │   │   ├── ScormDbContext.cs             # EF Core DbContext for ScormPackage + CourseAttempt
│   │   │   ├── ScormSessionStore.cs          # Valkey-backed session state (StackExchange.Redis)
│   │   │   ├── ManifestParser.cs             # Parse imsmanifest.xml via XDocument
│   │   │   └── ScormSeeder.cs                # Seed sample SCORM package for demo
│   │   ├── Endpoints/
│   │   │   ├── ScormEndpoints.cs             # POST /upload, POST /{id}/launch, GET /attempts/my
│   │   │   ├── ScormSessionEndpoints.cs      # setValue, getValue, commit, finish
│   │   │   ├── ScormApiScriptEndpoint.cs     # GET /session/{id}/api.js (SCORM API shim)
│   │   │   └── ScormModuleExtensions.cs      # IEndpointRouteBuilder.MapScormEndpoints()
│   │   ├── Scorm.csproj
│   │   └── ModuleMarker.cs
│   │
│   └── Scorm.Contracts/
│       ├── Scorm.Contracts.csproj
│       └── ModuleMarker.cs
```

**Structure Decision**: The Scorm module follows the same Domain → Application → Infrastructure → Endpoints pattern as Catalog and Enrollment. It introduces three new components:
1. **`IEnrollmentLookup`** in `Enrollment.Contracts` — a minimal new contract for cross-module enrollment validation (Principle III)
2. **`ScormSessionStore`** — Valkey-backed session state using StackExchange.Redis (ADR-0003)
3. **`wwwroot/scorm-content/`** — filesystem directory for extracted SCORM package content

The SCORM API shim is served as a JavaScript endpoint (`api.js`) rather than embedded in the Razor page, allowing the content to reference it via a standard `<script src="...">` tag. The wrapper page (`/scorm/launch/{courseId}`) injects the API script and the `beforeunload` auto-commit handler.

## Complexity Tracking

No constitution violations. The new `IEnrollmentLookup` contract in Enrollment.Contracts is a minimal, justified cross-module boundary that follows the existing `ICourseLookup` pattern. No complexity tracking needed.
