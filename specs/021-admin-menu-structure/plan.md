# Implementation Plan: Admin Menu Structure

**Branch**: `story/021-admin-menu-structure` | **Date**: 2025-07-31 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/021-admin-menu-structure/spec.md`

## Summary

Add navigation entries for all 6 admin management sections (Dashboard, Courses, Enrollments, Learners, Organizations, Upload) to the main navbar. Entries appear only when the user has an admin role (SuperUser or OrgAdmin) AND the role toggle is set to "Admin". No backend changes required — this is entirely a Razor Pages layout and CSS update.

## Technical Context

**Language/Version**: C# / .NET 10 (ASP.NET Core)

**Primary Dependencies**: Razor Pages, Lucide icons (JS)

**Storage**: N/A (UI-only change, no data model changes)

**Testing**: dotnet test (ArchitectureTests must still pass)

**Target Platform**: Web browser (desktop + mobile)

**Project Type**: Web application (Razor Pages)

**Performance Goals**: N/A (no server-side logic changes)

**Constraints**: Must follow existing mobile-first responsive CSS patterns; must reuse existing role toggle mechanism and `[data-role-link="admin"]` CSS selectors

**Scale/Scope**: Single component — `_Layout.cshtml` navbar + `site.css` styles + JS active-link detection

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | Only touches Host module (UI layer) |
| II. Clean Architecture | ✅ PASS | Presentation layer only, no domain/application changes |
| III. Module Boundaries | ✅ PASS | No cross-module references |
| IV. Human-Legible Code | ✅ PASS | Simple nav links following existing patterns |
| V. Sandbox | ✅ PASS | Within devcontainer |
| VII. Spec-Driven | ✅ PASS | Spec exists at `specs/021-admin-menu-structure/spec.md` |
| VIII. Branching | ✅ PASS | Will use `story/021-admin-menu-structure` |

## Project Structure

### Documentation (this feature)

```text
specs/021-admin-menu-structure/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (N/A - no data changes)
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Host/
├── Pages/
│   └── Shared/
│       └── _Layout.cshtml     # Add admin nav links, update JS active-link map
└── wwwroot/
    └── css/
        └── site.css           # CSS for new admin nav link visibility in mobile dropdown
```

**Structure Decision**: Single-file edits to the existing layout and stylesheet. No new files or directories needed.
