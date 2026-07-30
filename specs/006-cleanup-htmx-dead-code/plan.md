# Implementation Plan: Clean Up Orphaned HTMX Handler and Update Spec 005 Artifacts

**Branch**: `bug/006-cleanup-htmx-dead-code` | **Date**: 2025-07-30 | **Spec**: [spec.md](./spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features.

**Input**: Feature specification from `/specs/006-cleanup-htmx-dead-code/spec.md`

## Summary

Remove the orphaned `OnGetDetailAsync` HTMX handler from `Detail.cshtml.cs` (no view calls it after spec 005 removed HTMX from `_CourseCard.cshtml`), and update spec 005's `tasks.md` and `spec.md` to accurately reflect that HTMX inline swap was abandoned in favor of full-page navigation.

## Technical Context

**Language/Version**: C# / .NET 10 (ASP.NET Core Razor Pages)

**Primary Dependencies**: None — no new dependencies; only removal of existing dead code

**Storage**: N/A — no data layer changes

**Testing**: Manual verification (grep for references, build check, browser navigation test)

**Target Platform**: Web browser (any modern browser)

**Project Type**: Documentation update + dead code removal (not a feature)

**Performance Goals**: No performance impact — removing dead code only

**Constraints**: Must not break compilation or runtime behavior. Full-page navigation must continue working.

**Scale/Scope**: 3 files modified: `Detail.cshtml.cs` (1 method removed), `specs/005-fix-view-details-navigation/tasks.md` (annotations), `specs/005-fix-view-details-navigation/spec.md` (annotations). 1 file created: this plan's research artifacts.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | No module boundary changes; changes in Host project only |
| II. Clean Architecture | ✅ PASS | Dead code removal; no architecture changes |
| III. Compiled Module Boundaries | ✅ PASS | No cross-module references added or removed |
| IV. Human-Legible Code | ✅ PASS | Removing confusing dead code improves readability |
| V. Sandbox Not Optional | ✅ PASS | N/A — no agent sandbox implications |
| VI. Polyglot Storage | ✅ PASS | No storage changes |
| VII. Spec-Driven, Sliced Thin | ✅ PASS | This slice is one vertical cleanup: dead handler + doc updates |
| VIII. Branching Discipline | ✅ PASS | Branch: `bug/006-cleanup-htmx-dead-code` |
| IX. No Ad-Hoc Fixes | ✅ PASS | Full SpecKit workflow followed (spec → plan → tasks → implement) |

**Result**: All gates pass. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/006-cleanup-htmx-dead-code/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (N/A — no data changes)
├── quickstart.md        # Phase 1 output (validation scenarios)
└── contracts/           # N/A — no new contracts
```

### Source Code (files affected)

```text
src/Host/Pages/Courses/
└── Detail.cshtml.cs         # REMOVE: OnGetDetailAsync method

specs/005-fix-view-details-navigation/
├── tasks.md                 # UPDATE: T004/T005 descriptions, annotate T008-T010
└── spec.md                  # UPDATE: Annotate US4 as abandoned, update FR-006
```

**Structure Decision**: Single-project web application (Razor Pages). All changes live in `src/Host/Pages/` and `specs/`. No new modules, no new projects. This is a targeted cleanup within existing artifacts.

## Complexity Tracking

No constitution violations — this is a straightforward dead-code removal and documentation update with no added complexity.
