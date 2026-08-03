# Implementation Plan: Nav & Header Design Alignment

**Branch**: `story/018-nav-design-alignment` | **Date**: 2025-08-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/018-nav-design-alignment/spec.md`

## Summary

Replace emoji icons in the nav with Lucide SVG icons (stroke-width 2.75), add a client-side Learner/Admin role switcher (pill-shaped segmented control) that toggles visible nav links, remove the standalone Logout link from the top nav, and fix the mobile nav to collapse behind a hamburger at ≤760px (nav-only). All changes are purely presentational — no domain, application, or infrastructure code is touched. The two files affected are `_Layout.cshtml` (nav markup + inline JS) and `site.css` (nav styling + mobile breakpoint).

## Technical Context

**Language/Version**: C# / ASP.NET Core Razor Pages (.NET 10), HTML/CSHTML, CSS, vanilla JavaScript

**Primary Dependencies**: Lucide icons (CDN: `unpkg.com/lucide@latest` or inlined SVGs), existing HTMX (already loaded in layout)

**Storage**: N/A — no data model changes; role switcher is client-side state only

**Testing**: Visual inspection + browser devtools at 375px, 768px, 1280px viewports; static CSS audit for hardcoded values

**Target Platform**: Web browser (desktop and mobile), served via ASP.NET Core Razor Pages

**Project Type**: Web application (ASP.NET Core Razor Pages)

**Performance Goals**: Profile dropdown opens within 100ms; zero layout shift on role switcher toggle

**Constraints**: No raw hex colors or pixel values outside `:root` in nav CSS; no new npm packages or build steps; no server-side changes

**Scale/Scope**: 2 files modified (`_Layout.cshtml`, `site.css`); 15 nav links to update with SVG icons; 1 new role switcher component; mobile breakpoint change for nav

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-evaluate after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | N/A | No module changes — pure presentation |
| II. Clean Architecture | PASS | No new abstractions; existing layout file modified |
| III. Module Boundaries Compiled | N/A | No cross-module references |
| IV. Human-Legible Code | PASS | Explicit, straightforward JS toggle and SVG markup |
| V. Sandbox Not Optional | N/A | No host filesystem access |
| VI. Polyglot Storage | N/A | No storage changes |
| VII. Spec-Driven, Sliced Thin | PASS | Single vertical slice: nav visual alignment |
| VIII. Branching Discipline | PASS | Will create `story/018-nav-design-alignment` branch |
| IX. Plan On Master Only | PASS | Currently on `master` |
| X. No Ad-Hoc Fixes | PASS | Spec exists (018-nav-design-alignment) |
| XI. Parallel Implementation | N/A | Evaluated during tasks phase |
| XII. Return to Master | N/A | Evaluated at end of implementation |

## Project Structure

### Documentation (this feature)

```text
specs/018-nav-design-alignment/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (N/A — no model changes)
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (files affected)

```text
src/Host/
├── Pages/Shared/
│   └── _Layout.cshtml   # Nav markup, role switcher, JS handlers
└── wwwroot/css/
    └── site.css         # Nav styling, Lucide icon styles, mobile breakpoint
```

**Structure Decision**: Two-file change. `_Layout.cshtml` provides the shared nav shell rendered on every page. `site.css` provides all visual styling. No new files, components, or build steps — consistent with Constitution Principle II (don't add a layer the codebase doesn't need) and the "fewest moving parts" guidance.

## Phase 0: Research Findings

### Lucide Icon Inclusion Strategy

**Decision**: Include Lucide via CDN `<script>` tag (`unpkg.com/lucide@latest`) with `lucide.createIcons()` initialization, using `<i data-lucide="icon-name"></i>` markup in CSHTML.

**Rationale**: 
- Minimal overhead: one CDN script tag (same pattern as existing HTMX CDN tag in layout)
- No build step: icons render via DOM API, no compilation needed
- Stroke-width control: `lucide.createIcons({ attributes: { 'stroke-width': 2.75 } })` sets the global stroke-width in one call
- Icon names map directly to Lucide's catalog (book-open, graduation-cap, layout-dashboard, etc.)

**Alternatives considered**:
- Inline raw SVGs: rejected — 15 icons × 30+ chars each = bloated markup; Lucide CDN is cleaner
- Iconify CDN: rejected — adds an extra dependency layer for no benefit over Lucide's own CDN

### Icon Mapping (emoji → Lucide)

| Current (HTML entity) | Lucide icon | Nav Link |
|-----------------------|-------------|----------|
| `&#128218;` (📚) | `book-open` | Browse Courses |
| `&#127891;` (🎓) | `graduation-cap` | My Courses |
| `&#128202;` (📊) | `layout-dashboard` | Dashboard |
| `&#127971;` (🏢) | `building-2` | Organizations |
| `&#127795;` (🌳) | `network` | Org Chart |
| `&#128101;` (👥) | `users` | Learners |
| `&#128203;` (📕) | `book` | Courses |
| `&#9997;` (📋) | `clipboard-list` | Enrollments |
| `&#10133;` (➕) | `plus-circle` | Create Course |
| `&#128237;` (📤) | `upload` | Upload SCORM |
| `&#128273;` (🗃️) | `log-in` | Login |
| `&#9776;` (☰) | `menu` | Hamburger (closed) |
| `&#10006;` (✕) | `x` | Hamburger (open) |

### Role Switcher Implementation

**Decision**: Client-side segmented control with two segments ("Learner", "Admin"), toggling a CSS class on the nav that shows/hides admin links via CSS display rules. Active state stored in `localStorage` to persist across page navigations.

**Rationale**: Purely presentational — no server call needed. `localStorage` ensures the user's choice persists when they navigate between pages. CSS `display:none` on admin links when "Learner" is active keeps the DOM intact (no JS removal/re-creation).

### Mobile Breakpoint (Nav-Only 760px)

**Decision**: Add a new `@media (max-width: 760px)` block specifically for nav collapse. Page-level breakpoints (480px for cards/tables) remain unchanged. The nav media query uses `max-width: 760px` while desktop nav uses `min-width: 761px`.

**Rationale**: The user explicitly specified ≤760px for mobile nav. Keeping the page-level breakpoint at 480px avoids regressions on existing page layouts. A small gap (761px desktop, 760px mobile) ensures no flicker at the boundary.

## Phase 1: Design Artifacts

### Data Model

No data model changes. The role switcher is client-side state only (localStorage). No new entities, fields, or database changes.

### Contracts

No new contracts. No new module boundaries or cross-module interfaces. This is a presentation-layer change within the existing `Host` project.

## Re-evaluated Constitution Check (Post-Design)

All gates still pass. No new complexity introduced beyond the two-file scope identified above.
