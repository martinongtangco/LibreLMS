# Implementation Plan: Fix Course View Details Navigation

**Branch**: `bug/005-fix-view-details-navigation` | **Date**: 2025-07-30 | **Spec**: [spec.md](./spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `bug/005-fix-view-details-navigation`.

**Input**: Feature specification from `/specs/005-fix-view-details-navigation/spec.md`

## Summary

Fix the "View Details" button on course catalog cards so it reliably navigates to the full course detail page. The current implementation mixes HTMX inline-swap attributes (`hx-get`, `hx-target`, `hx-push-url`) with full-page navigation (`asp-page` tag helper) on the same `<a>` element. When HTMX loads, it intercepts the click and swaps a partial view into `#main-content`, but `hx-push-url` pushes a handler-specific URL (`?handler=Detail`) that renders a broken partial-only page on browser refresh. The fix separates full-page navigation (baseline) from HTMX inline swap (optional enhancement), ensuring bookmarkable URLs and graceful degradation.

## Technical Context

**Language/Version**: C# / .NET 10 (ASP.NET Core Razor Pages)

**Primary Dependencies**: HTMX 2.0.4 (CDN), Microsoft.AspNetCore.Mvc.TagHelpers

**Storage**: None — this is a frontend navigation fix, no data layer changes

**Testing**: Manual validation via browser (catalog → detail → refresh → back). Existing test projects (`Catalog.Tests`, `Enrollment.Tests`, `Scorm.Tests`) are unaffected.

**Target Platform**: Web browser (any modern browser)

**Project Type**: Web application (Razor Pages)

**Performance Goals**: Course detail page loads with full layout within 2 seconds (SC-002). No performance change expected — same server-side data fetching, different rendering path.

**Constraints**: Must work with JavaScript disabled (graceful degradation to `href` navigation). Must not break existing HTMX catalog filtering (spec 004 dependency).

**Scale/Scope**: 3 files modified: `_CourseCard.cshtml` (links), `Detail.cshtml` (page structure), `Detail.cshtml.cs` (remove broken handler URL from push-url). No new files, no new endpoints.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | No module boundary changes; all changes in Host project |
| II. Clean Architecture | ✅ PASS | View-layer only; no domain/application changes |
| III. Compiled Module Boundaries | ✅ PASS | No cross-module references added |
| IV. Human-Legible Code | ✅ PASS | Changes are straightforward Razor/HTML fixes |
| V. Sandbox Not Optional | ✅ PASS | N/A — no agent sandbox implications |
| VI. Polyglot Storage | ✅ PASS | No storage changes |
| VII. Spec-Driven, Sliced Thin | ✅ PASS | This slice is one vertical fix: navigation from catalog → detail |
| VIII. Branching Discipline | ✅ PASS | Branch: `bug/005-fix-view-details-navigation` |

**Result**: All gates pass. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/005-fix-view-details-navigation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (existing entities only)
├── quickstart.md        # Phase 1 output (validation scenarios)
├── contracts/           # N/A — no new contracts (frontend-only fix)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (files affected)

```text
src/Host/Pages/
├── Shared/
│   └── _CourseCard.cshtml      # FIX: Separate full-page nav from HTMX swap
├── Courses/
│   ├── Detail.cshtml            # FIX: Ensure consistent rendering from all entry points
│   └── Detail.cshtml.cs         # FIX: Remove handler-specific push-url or fix URL routing
└── Shared/
    └── _CourseDetail.cshtml     # REVIEW: Ensure partial renders correctly with layout reference
```

**Structure Decision**: Single-project web application (Razor Pages). All changes live in `src/Host/Pages/`. No new modules, no new projects, no new files beyond plan artifacts. This is a targeted frontend fix within the existing Host project.

## Complexity Tracking

No constitution violations — this is a straightforward frontend navigation fix with no added complexity.
