# Implementation Plan: Navigation Bar and Account Menu

**Branch**: `story/020-nav-account-menu` | **Date**: 2025-01-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/020-nav-account-menu/spec.md`

## Summary

Replace the current orange-brand navbar with a dark warm-neutral nav (#201e1d) featuring an integrated account control that combines a role toggle pill (always visible for all users), static user name display, and a chevron-toggleable dropdown (View Profile, Settings). The role switcher becomes available to all users (not admin-gated). Mobile (≤760px) collapses nav links into a hamburger dropdown that includes the role toggle pill and full-width page links. The Settings page already has Logout as its last row — no nav changes needed there. Page-level responsive adjustments: heading scales to 28px, padding reduces, toolbars/hero rows stack vertically on mobile.

Two files are the primary targets: `_Layout.cshtml` (nav markup + inline JS) and `site.css` (complete nav restyle + mobile breakpoint). The Settings page (`Settings.cshtml`) already has the required Logout row — verify and confirm, no edit needed.

## Technical Context

**Language/Version**: C# / ASP.NET Core Razor Pages (.NET 10), HTML/CSHTML, CSS, vanilla JavaScript

**Primary Dependencies**: Lucide icons (CDN: `unpkg.com/lucide@latest`), HTMX 2.0.4 (CDN — already loaded), Caprasimo + Figtree web fonts (self-hosted)

**Storage**: N/A — no data model changes; role switcher is client-side state only (localStorage)

**Testing**: Visual inspection + browser devtools at 375px, 760px, 761px, 1280px, 1920px viewports; CSS audit for hardcoded hex values in nav styles; ArchitectureTests pass

**Target Platform**: Web browser (desktop and mobile), served via ASP.NET Core Razor Pages

**Project Type**: Web application (ASP.NET Core Razor Pages, modular monolith)

**Performance Goals**: Account dropdown opens within 100ms; zero layout shift on role toggle; nav renders with no FOUC

**Constraints**: No new npm packages or build steps; no server-side code changes; no module boundary changes; colors specified as explicit hex values from the spec; account control must be `<div role=button tabindex=0>` not `<button>`

**Scale/Scope**: 2 files modified (`_Layout.cshtml`, `site.css`); 1 file verified (`Settings.cshtml`); complete nav visual and behavioral overhaul

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-evaluate after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | N/A | No module changes — pure presentation in Host |
| II. Clean Architecture | PASS | No new abstractions; existing layout + CSS modified |
| III. Module Boundaries Compiled | N/A | No cross-module references |
| IV. Human-Legible Code | PASS | Explicit inline JS for dropdown + role toggle; straightforward CSS |
| V. Sandbox Not Optional | N/A | No host filesystem access beyond repo |
| VI. Polyglot Storage | N/A | No storage changes |
| VII. Spec-Driven, Sliced Thin | PASS | Single vertical slice: complete nav + account menu redesign |
| VIII. Branching Discipline | PASS | Will create `story/020-nav-account-menu` branch |
| IX. Plan On Master Only | PASS | Currently on `master` |
| X. No Ad-Hoc Fixes | PASS | Spec exists (020-nav-account-menu) |
| XI. Parallel Implementation | N/A | Evaluated during tasks phase |
| XII. Return to Master | N/A | Evaluated at end of implementation |

## Project Structure

### Documentation (this feature)

```text
specs/020-nav-account-menu/
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
│   └── _Layout.cshtml   # Complete nav rewrite: account control with role pill,
│                        # hamburger menu, dropdown, inline JS handlers
├── Pages/Account/
│   └── Settings.cshtml  # Verify Logout row exists (no edit expected)
└── wwwroot/css/
    └── site.css         # Complete nav restyle: dark nav colors, account control
                         # styles, dropdown card, mobile hamburger + dropdown,
                         # page responsive adjustments at 760px
```

**Structure Decision**: Two-file change with one verification. `_Layout.cshtml` provides the shared nav shell rendered on every page. `site.css` provides all visual styling. The Settings page is verified for the Logout row. No new files, components, or build steps — consistent with Constitution Principle II and the precedent set by 018-nav-design-alignment.

## Phase 0: Research Findings

### 1. Color System Migration

**Decision**: Replace the current `--color-brand` (#c45d3e) nav background with explicit spec colors. Introduce new CSS custom properties for the nav palette and use them throughout nav styles.

| Spec Token | Hex Value | CSS Variable |
|------------|-----------|--------------|
| Nav background | #201e1d | `--nav-bg` |
| Nav hover states | #3a3634 | `--nav-hover` |
| Page background | #f5ead8 | `--page-bg` |
| Primary accent | #c67139 | `--accent` |
| Accent text (light bg) | #8a4f26 | `--accent-text` |
| Nav link default | #c9c2ba | `--nav-text` |
| Border/divider (light) | #e4d9c8 | `--border-light` |
| Mobile active link bg | #f3ddc9 | `--mobile-active-bg` |

The existing `--color-brand` token can be preserved for non-nav uses (buttons, etc.) but the nav will use the new variables.

**Rationale**: The spec provides exact hex values. CSS custom properties keep the values in one place and make the nav styles maintainable. This avoids hardcoding hex values in selectors.

### 2. Account Control Architecture

**Decision**: Replace the current avatar + profile dropdown with a single integrated account control: `<div role=button tabindex=0>` containing (left to right) role toggle pill → static name → chevron icon. The role toggle pill contains two `<button>` elements (Learner/Admin). Clicking the outer div (outside the pill) toggles the dropdown.

**Rationale**: The spec explicitly requires a `<div role=button>` to avoid invalid nested `<button>` elements. The role toggle must call `event.stopPropagation()` to prevent the dropdown from opening when toggling roles.

**Alternatives considered**:
- Using `<summary>`/`<details>`: rejected — doesn't support the nested interactive elements cleanly
- Custom element: rejected — adds complexity for a single-page layout
- Separate components: rejected — spec requires a single reusable control

### 3. Role Toggle Visibility

**Decision**: The role toggle pill is ALWAYS visible in the account control for ALL authenticated users, regardless of their actual roles. This changes from the current admin-gated behavior.

**Rationale**: The spec states "Role toggle pill (ALWAYS visible, not conditional on role)". The user can always switch between Learner and Admin views.

**Impact**: Removes the `@if (User.IsInRole(...))` guard around the role switcher. The role toggle becomes a client-side view preference, not a server-side authorization check.

### 4. Static Identity Display

**Decision**: The user name "Alice Johnson" (from `User.Identity.Name`) is displayed statically and does NOT change when the role toggle switches. Only the nav links change (Dashboard appears/disappears).

**Rationale**: The spec states "The name is STATIC ('Alice Johnson') — it does NOT change when the role toggle switches to Admin."

### 5. Mobile Hamburger Menu Redesign

**Decision**: At ≤760px, hide all nav links and the account control's name. Show a hamburger button that opens a dropdown containing: the role toggle pill (centered), then page links as full-width rows with active-state tinting (#f3ddc9 bg, #8a4f26 text). The account control area (outside hamburger) shows only the role pill + chevron.

**Rationale**: The spec defines a specific mobile layout with hamburger → dropdown → role pill + page links. The account name hides on mobile.

**Breakpoint**: 760px (≤760px = mobile), consistent with 018-nav-design-alignment.

### 6. Page-Level Responsive Adjustments

**Decision**: At ≤760px:
- Page heading: 36px → 28px
- Page padding: `24px 16px 32px`
- Toolbars and hero rows: row → column layout

These are applied via CSS media queries in `site.css` targeting the existing `.container`, `.filters`, `.flex-row`, `.flex-between` utility classes.

**Rationale**: Spec requirement for mobile page layout.

### 7. Settings Page Logout Verification

**Decision**: The Settings page already has Logout as the last row (after Email notifications and Theme). No changes needed.

**Rationale**: Reviewed `Settings.cshtml` — it already has the required structure with Logout in a separate card as the last row.

### 8. Dropdown Close Behavior

**Decision**: The account dropdown closes when:
1. User clicks the account control again (toggle)
2. User clicks outside the dropdown (document click handler)
3. User navigates to a new page (page reload resets state)
4. User presses Escape key

**Rationale**: Standard dropdown behavior. The spec mentions outside-click close as an edge case.

### 9. Active Link Detection

**Decision**: Use Razor `@ViewData` or URL path comparison to determine the active nav link. Each nav link checks if the current page URL matches its target and applies an `active` CSS class.

**Rationale**: Razor Pages provides `Context.Request.Path` for URL comparison. A simple helper approach avoids framework overhead.

## Phase 1: Design & Contracts

### Data Model

No data model changes. The navigation state (role view, dropdown open/closed) is entirely client-side.

### Contracts

No new API contracts. This is a purely presentational change.

### Quickstart Validation

See `quickstart.md` for end-to-end validation steps.
