# Implementation Plan: Responsive Mobile UI

**Branch**: `story/015-responsive-mobile-ui` | **Date**: 2025-08-03 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/015-responsive-mobile-ui/spec.md`

## Summary

Make the Libre LMS web portal fully responsive across mobile (≤ 480px), tablet (481–768px), and desktop (≥ 769px) viewports. All current styling lives inline in `_Layout.cshtml` (`<style>` block) and as `style=""` attributes on Razor Page elements. The approach is: extract styles into a single `wwwroot/css/site.css` stylesheet with CSS custom properties for the design tokens, add media queries for the three breakpoints, convert the navbar to a hamburger menu on mobile, and replace inline styles with semantic class names across all pages and partials.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (SDK 10.0.103), ASP.NET Core Minimal APIs + Razor Pages

**Primary Dependencies**: HTMX 2.0.4 (already loaded), Razor Pages, no CSS framework (pure custom CSS)

**Storage**: N/A — no new data entities. This is a presentation-layer change only.

**Testing**: xUnit (existing test projects). No new unit tests needed; validation is visual/manual via the quickstart guide.

**Target Platform**: Any modern browser on mobile (Chrome, Safari, Firefox), tablet, and desktop. Minimum viewport: 320px.

**Project Type**: Web application (Razor Pages server-rendered, HTMX for AJAX partial updates)

**Performance Goals**: No measurable impact on server response time. CSS file should be < 20KB uncompressed. No additional JS dependencies.

**Constraints**: Must not change desktop experience (≥ 1024px) — pixel-identical to current layout. Must not introduce new NuGet packages. Must not change HTMX behavior.

**Scale/Scope**: ~30 Razor Pages and partials across `src/Host/Pages/`. One layout file (`_Layout.cshtml`) controls the global `<style>` block.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | Changes are entirely within `Host` project — no module boundaries crossed |
| II. Clean Architecture | ✅ PASS | Presentation-layer only; no domain, application, or infrastructure changes |
| III. Module Boundaries | ✅ PASS | No cross-module references added |
| IV. Human-Legible Code | ✅ PASS | CSS with BEM-style class names and comments. ADR to be created for breakpoint decisions |
| V. Sandbox | ✅ PASS | N/A — not a sandboxing concern |
| VI. Polyglot Storage | ✅ PASS | N/A — no storage changes |
| VII. Spec-Driven | ✅ PASS | Spec exists at `spec.md` |
| VIII. Branching Discipline | ✅ PASS | Will use `story/015-responsive-mobile-ui` |
| IX. Plan On Master | ✅ PASS | Currently on `master` |
| X. No Ad-Hoc Fixes | ✅ PASS | This change has a spec |
| XI. Subagent Parallelism | N/A | Applicable during `/speckit.implement` |
| XII. Return to Master | N/A | Post-implementation |

**No violations.** No complexity tracking needed.

## Project Structure

### Documentation (this feature)

```text
specs/015-responsive-mobile-ui/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (minimal — no new entities)
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code Changes (Host project only)

```text
src/Host/
├── wwwroot/
│   └── css/
│       └── site.css                 # NEW: extracted styles + responsive breakpoints
├── Pages/
│   ├── Shared/
│   │   ├── _Layout.cshtml           # MODIFIED: remove inline <style>, add <link>, hamburger nav markup + JS
│   │   ├── _CourseCard.cshtml       # MODIFIED: inline styles → class names
│   │   ├── _CourseList.cshtml       # MODIFIED: inline styles → class names
│   │   ├── _EnrollmentList.cshtml   # MODIFIED: inline styles → class names
│   │   ├── _MyCourseRow.cshtml      # MODIFIED: inline styles → class names
│   │   ├── _OrgBreadcrumb.cshtml    # MODIFIED: inline styles → class names
│   │   ├── _OrgContextMenu.cshtml   # MODIFIED: inline styles → class names
│   │   ├── _OrgNode.cshtml          # MODIFIED: inline styles → class names
│   │   └── _ErrorPartial.cshtml     # MODIFIED: inline styles → class names
│   ├── Courses/
│   │   ├── Index.cshtml             # MODIFIED: inline styles → class names
│   │   └── Detail.cshtml            # MODIFIED: inline styles → class names
│   ├── MyCourses/
│   │   └── Index.cshtml             # MODIFIED: inline styles → class names
│   ├── Account/
│   │   ├── Login.cshtml             # MODIFIED: inline styles → class names
│   │   └── Logout.cshtml            # MODIFIED: inline styles → class names
│   ├── Admin/
│   │   ├── Dashboard/Index.cshtml   # MODIFIED: inline styles → class names
│   │   ├── Learners/Index.cshtml    # MODIFIED: inline styles → class names
│   │   ├── Learners/Create.cshtml   # MODIFIED: inline styles → class names
│   │   ├── Learners/Edit.cshtml     # MODIFIED: inline styles → class names
│   │   ├── Organizations/Index.cshtml  # MODIFIED: inline styles → class names
│   │   ├── Organizations/Chart.cshtml  # MODIFIED: inline styles → class names
│   │   ├── Organizations/Create.cshtml # MODIFIED: inline styles → class names
│   │   ├── Organizations/Edit.cshtml   # MODIFIED: inline styles → class names
│   │   ├── Organizations/_AddUserDialog.cshtml   # MODIFIED
│   │   ├── Organizations/_AssignCourseDialog.cshtml # MODIFIED
│   │   ├── Organizations/_AssignUserDialog.cshtml  # MODIFIED
│   │   ├── Organizations/_CreateChildDialog.cshtml # MODIFIED
│   │   ├── Organizations/_EditDialog.cshtml        # MODIFIED
│   │   ├── Courses/Index.cshtml          # MODIFIED
│   │   ├── Courses/Create.cshtml         # MODIFIED
│   │   ├── Enrollments/Index.cshtml      # MODIFIED
│   │   ├── Enrollments/BulkEnroll.cshtml # MODIFIED
│   │   └── Upload.cshtml                 # MODIFIED
│   ├── Scorm/
│   │   └── Launch.cshtml                # MODIFIED
│   └── Error.cshtml                     # MODIFIED
```

**Structure Decision**: All changes are within `src/Host/` — the single ASP.NET Core web project. One new file (`wwwroot/css/site.css`) extracts and enhances the current inline `<style>` block. All Razor Pages and partials are modified to replace `style=""` attributes with semantic CSS class names. The `_Layout.cshtml` gains a hamburger menu with a minimal vanilla JS toggle (no framework, per Constitution Principle II simplicity).

## Complexity Tracking

Not applicable — no constitution violations.
