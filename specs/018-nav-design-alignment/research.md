# Research: Nav & Header Design Alignment

**Feature**: 018-nav-design-alignment
**Date**: 2025-08-04

## 1. Lucide Icon Inclusion Strategy

**Decision**: Include Lucide via CDN `<script>` tag with `lucide.createIcons()` initialization, using `<i data-lucide="icon-name"></i>` markup in CSHTML.

**Rationale**: Minimal overhead (one CDN script tag, matching existing HTMX pattern), no build step, global stroke-width control via `createIcons({ attributes: { 'stroke-width': 2.75 } })`.

**Alternatives considered**:
- Inline raw SVGs: rejected — 15+ icons would bloat the layout markup
- Iconify CDN: rejected — extra layer for no benefit
- Self-host Lucide JS: rejected — adds static file management for a single script

## 2. Icon Mapping (emoji → Lucide)

| Nav Link | Lucide Icon |
|----------|-------------|
| Browse Courses | `book-open` |
| My Courses | `graduation-cap` |
| Dashboard | `layout-dashboard` |
| Organizations | `building-2` |
| Org Chart | `network` |
| Learners | `users` |
| Courses | `book` |
| Enrollments | `clipboard-list` |
| Create Course | `plus-circle` |
| Upload SCORM | `upload` |
| Login | `log-in` |
| Hamburger (closed) | `menu` |
| Hamburger (open) | `x` |
| Profile dropdown arrow | `chevron-down` |
| View Profile (dropdown) | `user` |
| Settings (dropdown) | `settings` |

## 3. Role Switcher Implementation

**Decision**: Client-side segmented control with `localStorage` persistence. CSS class toggle on nav element controls admin link visibility.

**Rationale**: No server changes needed. Persists across page navigations. Pure CSS show/hide keeps DOM intact.

**Alternatives considered**:
- URL-based toggle (`?role=admin`): rejected — pollutes URLs, unnecessary for a purely visual toggle
- Session-only (no persistence): rejected — user would lose their choice on every navigation
- Server-side role change: rejected — explicitly out of scope per clarification Q1

## 4. Mobile Breakpoint Scope

**Decision**: Nav-only breakpoint at 760px. Page-level breakpoints retain 480px.

**Rationale**: User specified ≤760px for mobile nav. Avoiding global breakpoint changes prevents regressions on existing page layouts.

**Alternatives considered**:
- Global 760px: rejected — affects all page layouts, larger regression risk
- Three-tier (480/760/1024): rejected — adds complexity for a single feature slice

## 5. Profile Dropdown Icon Enhancement

**Decision**: Add Lucide icons (user, settings) to the profile dropdown menu entries for consistency with the nav icon treatment.

**Rationale**: Maintains visual consistency — all nav elements use Lucide icons. No behavioral change.

## Outstanding NEEDS CLARIFICATION

None. All decisions resolved.
