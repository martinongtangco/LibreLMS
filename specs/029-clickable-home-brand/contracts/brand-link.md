# UI Contract: Navbar Brand Link

**Feature**: [spec.md](../spec.md) | **Date**: 2026-08-16

Behavior contract for the "Libre LMS" brand in the shared navbar
(`src/Host/Pages/Shared/_Layout.cshtml`). Markup/styling follows the existing
navbar patterns; this document fixes the observable behavior so implementation
and E2E tests agree on it.

## Element

| | |
|---|---|
| Element | `<a>` anchor (was `<span>`), class `brand`, visible text `Libre LMS` |
| Target | `/` (site root — currently 302-redirects to `/Courses`, i.e., Browse Courses) |
| Rendered by | Shared navbar in `_Layout.cshtml`, outside the authenticated/anonymous `@if` split |
| Visibility | **Every page**, for anonymous and signed-in users, all roles, both desktop and mobile/collapsed navigation |
| Role awareness | None — the brand always targets Home (Browse Courses), never the admin Dashboard, regardless of the active Learner/Admin role view |

## Behavior

- **Navigation**: full page navigation (no HTMX/SPA behavior). Clicking the
  brand from any page lands the user on the Home page showing Browse Courses.
- **Idempotent on Home**: clicking the brand while already on Home simply
  (re)loads Browse Courses — no error, no redirect loop.
- **State reset**: because navigation is a full page load, no client-side nav
  state (hamburger open, account dropdown open, role toggle) can survive the
  click.
- **Dead-end removal**: on the Login page (including its "Access denied"
  variant), the brand link is the in-page means of returning to Home without
  logging in or using the browser back button.

## Styling Contract

- Keeps the current wordmark look: heading font, 20px, color `var(--page-bg)`,
  `white-space: nowrap`.
- No underline in normal state (`text-decoration: none` on the anchor).
- Hover affordance consistent with existing nav links (color shifts to white,
  matching `.navbar .nav-links a.nav-link:hover`).

## Out of Contract (unchanged)

- Login, Signup, Forgot Password, and all other account flows.
- Root route behavior (`GET /` → Browse Courses) — pinned, not modified.
- Active-link highlighting, account menu, role-view toggle behavior.
