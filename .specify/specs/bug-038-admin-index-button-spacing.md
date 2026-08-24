# Bug 038: Admin index action buttons sit flush against the content below (Learners, Enrollments)

## Problem
The same spacing defect fixed for the Organizations page in bug-037 also affects two
other admin index pages:

- **Learner Management** (`/Admin/Learners`): the "Create Learner" button renders with
  no gap above the search/filter bar.
- **Enrollment Management** (`/Admin/Enrollments`): the "Bulk Enroll" button renders
  with no gap above the alerts/filter bar.

## Root Cause
Identical to bug-037: the action button sits in a bare `<p>` element with no spacing
class, and the site stylesheet's global reset
(`*, *::before, *::after { margin: 0; padding: 0; }` in `src/Host/wwwroot/css/site.css`)
removes the browser's default paragraph margin. Both pages:

- `src/Host/Pages/Admin/Learners/Index.cshtml`
- `src/Host/Pages/Admin/Enrollments/Index.cshtml`

A repo-wide scan for bare `<p>` wrappers around `.btn` links confirms these are the only
two remaining instances (the Organizations instance was fixed in bug-037).

## Fix
Add the existing `mb-lg` utility class (1.5rem, matching bug-037 and the spacing used
under other action rows such as `.filters`) to the `<p>` wrapping the action button on
each page. No CSS changes.

E2E coverage: new Playwright tests in
`tests/Playwright.Tests/tests/05-admin-learners.spec.ts` and
`tests/Playwright.Tests/tests/07-admin-enrollments.spec.ts` assert each action-button
paragraph has a computed bottom margin of at least 16px, so the regression is caught if
the spacing class is removed again.

## Constitution Principles
- **IV. Human-Legible AI-Authored Code** — reuses the existing `mb-lg` design-token
  utility instead of introducing new CSS or inline styles.
- **X. No Ad-Hoc Fixes** — documented here before the code edit; branch
  `bug/038-admin-index-button-spacing`.
- **XIII. Verification Before Claim** — rebuild/restart + Playwright evidence required
  before claiming the fix.
