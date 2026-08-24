# Bug 037: Org Chart / Create Organization buttons sit flush against the Root Organization tree

## Problem
On the Organization Management page (`/Admin/Organizations`), the two action buttons
("Org Chart View" and "Create Organization") render with no gap between them and the
"Root Organization" tree directly below.

## Root Cause
The action buttons live in a bare `<p>` element in
`src/Host/Pages/Admin/Organizations/Index.cshtml`. The site stylesheet's global reset
(`*, *::before, *::after { margin: 0; padding: 0; }` in `src/Host/wwwroot/css/site.css`)
zeros out the browser's default paragraph margin, and the `<p>` carries no spacing
class — so the button row and the org tree render flush. Other admin action rows avoid
this explicitly (e.g. the Courses index uses an action bar with
`margin-bottom: var(--spacing-md)`; `.filters` uses `var(--spacing-lg)`).

## Fix
Add the existing `mb-lg` utility class (1.5rem, matching the spacing used under other
action rows such as `.filters`) to the `<p>` wrapping the two action buttons on the
Organizations index page. No CSS changes; no layout changes beyond the intended gap.

E2E coverage: new Playwright test in `tests/Playwright.Tests/tests/06-admin-organizations.spec.ts`
asserts the action-buttons paragraph has a computed bottom margin of at least 16px, so
the regression is caught if the spacing class is removed again.

## Constitution Principles
- **IV. Human-Legible AI-Authored Code** — reuses the existing `mb-lg` design-token
  utility instead of introducing new CSS or inline styles.
- **X. No Ad-Hoc Fixes** — documented here before the code edit; branch
  `bug/037-org-action-buttons-spacing`.
- **XIII. Verification Before Claim** — rebuild/restart + Playwright evidence required
  before claiming the fix.
