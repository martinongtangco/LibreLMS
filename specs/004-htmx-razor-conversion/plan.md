# Implementation Plan: HTMX + Razor Modern UI

**Branch**: `story/004-htmx-razor-conversion` | **Date**: 2025-07-28 | **Spec**: [spec.md](./spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features.

**Input**: Feature specification from `/specs/004-htmx-razor-conversion/spec.md`

## Summary

Add HTMX (via CDN) to the existing Razor Pages web portal to enable SPA-like interactivity: partial page swaps for course browsing/filtering, inline enrollment feedback, course detail navigation, and live SCORM status updates. No new data entities, no changes to existing Minimal API endpoints. HTMX requests render Razor partial views backed by the same module application services used by the APIs.

## Technical Context

**Language/Version**: C#, .NET 10 (GA), ASP.NET Core Razor Pages

**Primary Dependencies**: HTMX 2.x (CDN only, no NuGet package), existing module services (CourseCatalogService, EnrollmentService, ScormAttemptService)

**Storage**: No new storage. Reuses existing MSSQL (via EF Core contexts) and Valkey (SCORM session state)

**Testing**: xUnit (existing), ArchitectureTests (NetArchTest), manual browser validation for HTMX swaps

**Target Platform**: Web browser (any modern browser with JavaScript enabled)

**Project Type**: Web application (modular monolith with Razor Pages presentation layer)

**Performance Goals**: Filter updates appear within 1 second of user input (SC-001); enrollment feedback within 2 seconds (SC-002)

**Constraints**: No breaking changes to existing API surface; graceful degradation when JS is disabled; layout stability during swaps (no layout shift)

**Scale/Scope**: Single web portal, same user base as existing application. HTMX adds ~20KB gzipped from CDN.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | HTMX is presentation-layer only; no module boundary changes |
| II. Clean Architecture | ✅ PASS | HTMX attributes live in .cshtml files (presentation); services remain in Application layer |
| III. Module Boundaries Compiled | ✅ PASS | No cross-module references; Host references modules only via existing project references |
| IV. Human-Legible Code | ✅ PASS | HTMX is declarative HTML attributes — more explicit than inline JS |
| V. Sandbox Not Optional | N/A | Development environment concern, not code concern |
| VI. Polyglot Storage | N/A | No new storage introduced |
| VII. Spec-Driven | ✅ PASS | Following specify → plan → tasks → implement flow |
| VIII. Branching Discipline | ✅ PASS | Branch: `story/004-htmx-razor-conversion` |
| Web portal constraint | ✅ PASS | Constitution permits "Razor Pages or Blazor Server" — choosing Razor Pages + HTMX |

**Post-Phase 1 Re-evaluation**: All gates still pass. No unjustified complexity added.

## Project Structure

### Documentation (this feature)

```text
specs/004-htmx-razor-conversion/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── contracts/           # Phase 1 output (HTMX partial view contracts)
```

### Source Code Changes

```text
src/Host/
├── Host.csproj                    # No package changes (HTMX via CDN only)
├── Pages/
│   ├── Shared/
│   │   ├── _Layout.cshtml         # Add HTMX CDN script tag
│   │   ├── _CourseCard.cshtml     # NEW: Reusable course card partial
│   │   ├── _CourseList.cshtml     # NEW: Course list container partial (HTMX target)
│   │   ├── _EnrollmentResult.cshtml  # NEW: Inline enrollment feedback partial
│   │   └── _MyCourseRow.cshtml    # NEW: Individual enrollment row partial
│   ├── Courses/
│   │   ├── Index.cshtml           # MODIFIED: Add HTMX attributes, use partials
│   │   ├── Index.cshtml.cs        # MODIFIED: Add OnGetCourseListAsync handler
│   │   ├── Detail.cshtml          # MODIFIED: HTMX enrollment, remove inline JS
│   │   └── Detail.cshtml.cs       # MODIFIED: Add enrollment HTMX handler
│   ├── MyCourses/
│   │   ├── Index.cshtml           # MODIFIED: HTMX refresh, use partials
│   │   └── Index.cshtml.cs        # MODIFIED: Add OnGetEnrollmentsAsync handler
│   ├── Admin/
│   │   └── Upload.cshtml          # MODIFIED: HTMX file upload with progress
│   └── _ViewImports.cshtml        # (if needed) Add shared using directives
└── wwwroot/
    └── (no changes — HTMX loaded from CDN)
```

**Structure Decision**: All HTMX changes are confined to `src/Host/Pages/` — the presentation layer. No new modules, no new projects. Partial views live under `Pages/Shared/` following Razor Pages conventions. Existing Minimal API endpoints (`/api/*`) are untouched. New Razor Page handler methods (`OnGet...Async`, `OnPost...Async`) serve HTML fragments for HTMX swaps, using the same application services directly instead of going through `HttpClient` to the API endpoints.

## Complexity Tracking

No constitution violations — no entries needed.
