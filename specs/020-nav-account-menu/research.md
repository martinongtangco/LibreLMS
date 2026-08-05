# Research: Navigation Bar and Account Menu

**Feature**: 020-nav-account-menu
**Date**: 2025-01-31

## 1. Color System Migration

**Decision**: Replace the current `--color-brand` (#c45d3e) nav background with explicit spec colors. Introduce new CSS custom properties scoped to the nav palette.

| Spec Token | Hex Value | CSS Variable |
|---|---|---|
| Nav background | #201e1d | `--nav-bg` |
| Nav hover states | #3a3634 | `--nav-hover` |
| Page background | #f5ead8 | `--page-bg` |
| Primary accent | #c67139 | `--accent` |
| Accent text (light bg) | #8a4f26 | `--accent-text` |
| Nav link default | #c9c2ba | `--nav-text` |
| Border/divider (light) | #e4d9c8 | `--border-light` |
| Mobile active link bg | #f3ddc9 | `--mobile-active-bg` |

**Rationale**: Spec provides exact hex values. CSS custom properties keep them in one place. Existing `--color-brand` preserved for non-nav uses.

**Alternatives considered**:
- Direct hex in selectors: rejected — violates DRY, harder to maintain
- Full design system overhaul: rejected — out of scope, nav-only change

## 2. Account Control Architecture

**Decision**: Single `<div role=button tabindex=0>` containing role toggle pill → static name → chevron. Role pill uses two `<button>` elements. Click on outer div (not pill) toggles dropdown. Pill click calls `event.stopPropagation()`.

**Rationale**: Spec explicitly requires `<div role=button>` to avoid invalid nested `<button>`. This is valid HTML and accessible (has role, tabindex, aria-label).

**Alternatives considered**:
- `<summary>`/`<details>`: rejected — no nested interactive elements
- Custom web component: rejected — unnecessary complexity

## 3. Role Toggle Visibility

**Decision**: Always visible for all authenticated users. Removes `@if (User.IsInRole(...))` guard. Client-side view preference, not server authorization.

**Rationale**: Spec: "Role toggle pill (ALWAYS visible, not conditional on role)".

**Alternatives considered**:
- Admin-only (current): rejected — contradicts spec
- Conditional on user having admin permission: rejected — spec says always visible

## 4. Static Identity Display

**Decision**: `User.Identity.Name` rendered once, never changes with role toggle. Only nav links change.

**Rationale**: Spec: "The name is STATIC ('Alice Johnson')".

## 5. Mobile Hamburger Menu

**Decision**: At ≤760px: hamburger button opens dropdown with role toggle pill (centered, margin 8px) + page links as full-width rows. Account name hides. Active links get `#f3ddc9` bg + `#8a4f26` text.

**Rationale**: Spec defines specific mobile layout. Consistent with 018-nav-design-alignment breakpoint.

**Alternatives considered**:
- Separate mobile nav component: rejected — same component with CSS media queries
- Bottom sheet pattern: rejected — spec says dropdown

## 6. Page-Level Responsive Adjustments

**Decision**: CSS media queries at ≤760px for: h1 36→28px, container padding 24px 16px 32px, flex-row→column for toolbars/hero rows.

**Rationale**: Spec requirement. Applied via existing utility classes.

## 7. Settings Page Logout

**Decision**: Verified — `Settings.cshtml` already has Logout as last row after Email notifications and Theme. No changes needed.

**Rationale**: Existing code matches spec requirement.

## 8. Dropdown Close Behavior

**Decision**: Closes on: re-click account control, outside document click, page navigation (reload), Escape key.

**Rationale**: Standard UX pattern. Spec mentions outside-click as edge case.

## 9. Active Link Detection

**Decision**: Compare `Context.Request.Path` with each link target URL. Apply `active` CSS class when they match.

**Rationale**: Simple, no framework overhead. Razor Pages provides path access.

**Alternatives considered**:
- CSS `:focus-within`: rejected — doesn't work for URL-based active state
- JavaScript URL parsing: rejected — Razor already has the path server-side

## Outstanding NEEDS CLARIFICATION

None. All decisions resolved.
