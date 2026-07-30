# Implementation Plan: Course Launch & Status Tracking

**Branch**: `story/007-course-launch-status` | **Date**: 2025-07-30 | **Spec**: [spec.md](./spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/007-course-launch-status/spec.md`

## Summary

Extend the "My Courses" and course detail pages to display SCORM 1.2 compliant status labels and percentage completion for enrolled courses. The core work involves: (1) mapping raw SCORM `cmi.core.lesson_status` values to human-readable display labels, (2) computing percentage completion from `cmi.core.score.raw`, and (3) ensuring consistency across the enrolled courses list, course detail page, and individual course row partials. The backend `CourseAttempt` domain and `ScormSessionService` already capture lesson_status and score — the primary gap is in the display layer and the status mapping between SCORM standard values and UI labels.

## Technical Context

**Language/Version**: C# / .NET 10 (GA), ASP.NET Core Razor Pages

**Primary Dependencies**: EF Core (MSSQL), StackExchange.Redis (Valkey), Razor Pages, HTMX for inline refresh

**Storage**: MSSQL for durable `CourseAttempt` records (status, score). Valkey for ephemeral session state during active SCORM sessions.

**Testing**: xUnit with `dotnet test` — existing `Scorm.Tests` and `Enrollment.Tests` projects

**Target Platform**: Linux server (containerized via devcontainer), web browser client

**Project Type**: Web application (modular monolith)

**Performance Goals**: Status/percentage visible within 1 second of session end (SC-005). Page loads within 3 seconds (SC-001).

**Constraints**: SCORM 1.2 simplified scope (no SCORM 2004, no sequencing). Module boundaries enforced through `*.Contracts` projects. No direct cross-module references to Domain/Application layers.

**Scale/Scope**: Single-tenant LMS. Student-facing views updated. No admin-facing changes required for this feature.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

### Pre-Design Evaluation

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | Changes stay within Host (UI) and Scorm module. No new modules. |
| II. Clean Architecture | ✅ PASS | No new abstraction layers. Display mapping is a simple utility function in the Host layer. |
| III. Module Boundaries | ✅ PASS | Host accesses Scorm module via existing `ScormAttemptService` and `ScormSessionService`. No new cross-module references needed. |
| IV. Human-Legible Code | ✅ PASS | Status mapping will be a simple, well-documented switch expression. |
| V. Sandbox | ✅ PASS | All work inside devcontainer. |
| VI. Polyglot Storage | ✅ PASS | No new storage introduced. Uses existing Valkey (session) + MSSQL (attempts). |
| VII. Spec-Driven | ✅ PASS | This plan follows from spec 007. |
| VIII. Branching | ✅ PASS | Branch: `story/007-course-launch-status`. |
| IX. No Ad-Hoc Fixes | ✅ PASS | This change is documented via SpecKit workflow. |

**Pre-Design Gate Result**: ALL PASS — Proceeded to Phase 0.

### Post-Design Re-Evaluation (after Phase 1)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | Confirmed: all changes within existing modules. No new modules created. |
| II. Clean Architecture | ✅ PASS | Confirmed: `ScormHelpers.GetDisplayLabel()` is a presentation utility. No domain contamination. |
| III. Module Boundaries | ✅ PASS | Confirmed: no new `*.Contracts` additions. Existing `ScormAttemptService` interface unchanged. |
| IV. Human-Legible Code | ✅ PASS | Confirmed: status mapping is a documented switch expression with a clear table. |
| V. Sandbox | ✅ PASS | N/A (runtime concern, not design). |
| VI. Polyglot Storage | ✅ PASS | Confirmed: no new storage introduced. No schema migrations needed. |
| VII. Spec-Driven | ✅ PASS | All design artifacts (data-model.md, contracts/, quickstart.md) derived from spec. |
| VIII. Branching | ✅ PASS | N/A (runtime concern). |
| IX. No Ad-Hoc Fixes | ✅ PASS | Design documented before implementation. |

**Post-Design Gate Result**: ALL PASS — Ready for `/speckit.tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/007-course-launch-status/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (status mapping contract)
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

```text
src/
├── Host/
│   ├── Pages/
│   │   ├── MyCourses/
│   │   │   └── Index.cshtml.cs        # Update: pass display status + percentage to rows
│   │   ├── Courses/
│   │   │   └── Detail.cshtml.cs       # Update: show status + percentage for enrolled courses
│   │   └── Shared/
│   │       ├── _MyCourseRow.cshtml    # Update: full SCORM status mapping + percentage display
│   │       └── _EnrollmentList.cshtml # Update (if needed): pass through new fields
│   └── ScormHelpers.cs                # Add: LessonStatus display mapping utility
├── Modules/
│   └── Scorm/
│       ├── Domain/
│       │   └── CourseAttempt.cs       # Update: ensure all SCORM status values are valid
│       └── Application/
│           ├── ScormSessionService.cs  # Update: fix score=0 not being saved on commit/finish
│           └── ScormAttemptService.cs  # No changes needed (already returns status + score)
tests/
├── Scorm.Tests/                       # Add: status mapping tests, percentage display tests
└── Enrollment.Tests/                  # Add: integration tests for enrollment + attempt display
```

**Structure Decision**: Changes are scoped to three areas:
1. **Host UI layer** (Razor Pages + partials) — the bulk of the work, adding status mapping and percentage display
2. **ScormHelpers.cs** — a new utility function for SCORM lesson_status → display label mapping
3. **ScormSessionService** — a small fix to save score=0 (currently skipped due to `score > 0` guard)

No new modules, contracts, or infrastructure are needed. The existing `ScormAttemptService` already returns `Status` and `ScoreRaw` per attempt.

## Complexity Tracking

Not applicable — no constitution violations.
