# Feature Specification: Clickable Brand Link to Home

**Feature Branch**: `story/029-clickable-home-brand`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2026-08-16

**Status**: Draft

**Input**: User description: "Need to have the Libre LMS to be clickable to home. I sometimes find myself testing this logged off and clicking the Login button just to be stuck in the Login page. Home should be Browse Courses by default."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Escape the Login Page (Priority: P1)

A visitor who is signed out lands on the Login page (e.g., after clicking the
Login link in the navbar, or after logging out). Today the Login page offers no
way back to the rest of the site except the browser's back button — the
"Libre LMS" brand in the navbar is plain text, not a link. The visitor can now
click the "Libre LMS" brand in the navbar and immediately land on the Home
page (Browse Courses), no matter which page they are on.

**Why this priority**: This is the direct pain point reported. A signed-out
user currently has a dead end: Login page with no outbound navigation. Making
the brand a link to Home removes the dead end on every page, signed out or in.

**Independent Test**: Sign out (or open the site in a fresh browser session),
navigate to the Login page, click the "Libre LMS" brand in the navbar, and
verify the user lands on Browse Courses. No login required at any step.

**Acceptance Scenarios**:

1. **Given** a signed-out user is on the Login page, **When** they click the
   "Libre LMS" brand in the navbar, **Then** they are taken to the Home page,
   which displays Browse Courses.
2. **Given** a signed-out user is on any other public page (e.g., a course
   detail page), **When** they click the "Libre LMS" brand, **Then** they are
   taken to the Home page displaying Browse Courses.
3. **Given** a signed-out user is on the Login page, **When** they click the
   "Libre LMS" brand, **Then** the navigation works without prompting for
   credentials or redirecting back to the Login page.

---

### User Story 2 - One-Click Return to Home from Any Page (Priority: P1)

A signed-in user (learner or admin) is anywhere in the app — My Courses, a
course detail page, or an admin page. Clicking the "Libre LMS" brand always
returns them to the Home page (Browse Courses). The brand is the universal
"home" control, independent of the user's role or their current role-view
toggle (Learner/Admin).

**Why this priority**: Same core capability as User Story 1 but for signed-in
users across all sections of the app. Together with US1, "brand = Home"
holds universally.

**Independent Test**: Sign in as a learner, visit My Courses, click the brand,
verify Browse Courses is shown. Repeat while signed in as an admin on the
admin Dashboard (with the admin role view active) and verify the brand still
goes to Browse Courses, not the admin Dashboard.

**Acceptance Scenarios**:

1. **Given** a signed-in learner is on the My Courses page, **When** they click
   the "Libre LMS" brand, **Then** they land on Browse Courses.
2. **Given** a signed-in admin with the Admin role view active is on the admin
   Dashboard, **When** they click the "Libre LMS" brand, **Then** they land on
   Browse Courses (the brand does not target the admin Dashboard).
3. **Given** a signed-in user is on any page of the application, **When** they
   click the "Libre LMS" brand, **Then** they land on Browse Courses within a
   normal page load, with no error and no loss of their signed-in state.

---

### User Story 3 - Home Is Browse Courses by Default (Priority: P2)

Visiting the site's root URL shows Browse Courses by default, whether the
visitor is signed in or not. This confirms the "Home" destination that the
brand points to is unambiguous: Home = Browse Courses.

**Why this priority**: Defines and locks the Home destination referenced by
US1 and US2. It is lower priority because the current root behavior already
does this — the value here is in pinning it as an explicit, tested requirement
so it cannot regress.

**Independent Test**: Open the site's root URL in a fresh browser session and
verify Browse Courses is displayed; sign in and repeat, verifying the same
page is shown.

**Acceptance Scenarios**:

1. **Given** an anonymous visitor opens the site's root URL, **When** the page
   loads, **Then** Browse Courses is displayed (no sign-in prompt, no separate
   landing page).
2. **Given** a signed-in user opens the site's root URL, **When** the page
   loads, **Then** Browse Courses is displayed.

---

### Edge Cases

- **Clicking the brand while already on Home**: the user remains on Browse
  Courses (the page may simply reload); no error, no redirect loop.
- **Mobile navigation**: on small screens the navbar collapses (hamburger
  layout); the brand is still visible in the collapsed bar and must still be
  clickable, navigating to Home, on both signed-out and signed-in mobile views.
- **Brand click does not disturb other navigation state**: clicking the brand
  must not leave the mobile hamburger menu open, the account dropdown open, or
  an admin/learner role toggle in a broken state after navigation completes.
- **Access-denied variant of the login page**: the Login page also renders an
  "Access denied" variant for signed-in users without permission for a target
  page; the brand link must still be present and navigate to Home there.
- **No authenticated-only behavior**: the brand link and the Home page it
  targets must both be reachable by anonymous visitors; no role or account is
  required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The "Libre LMS" brand shown in the navbar MUST be rendered as a
  link that navigates to the Home page (the site root) on every page of the
  application, for both signed-out and signed-in users.
- **FR-002**: The Home page MUST display Browse Courses as its default content
  for both anonymous visitors and signed-in users.
- **FR-003**: The brand link MUST be reachable by anonymous visitors; no
  account, role, or credential is required to click it or to view the page it
  targets.
- **FR-004**: The brand link MUST be visually identifiable as interactive
  (indicated consistently with the site's existing link styling) so users
  know it is clickable.
- **FR-005**: The brand link MUST work in the mobile/collapsed navigation as
  well as the desktop navigation, for both signed-out and signed-in users.
- **FR-006**: The brand link MUST always target the Home page (Browse
  Courses), regardless of the user's role or active role-view (Learner or
  Admin); it MUST NOT target the admin Dashboard or any role-specific page.
- **FR-007**: Existing navigation behavior MUST be preserved: active-link
  highlighting, the account menu, the role-view toggle, and the Login link
  (for signed-out users) all continue to work exactly as before, and clicking
  the brand MUST NOT leave the mobile menu or account dropdown in an open
  state.
- **FR-008**: The Login page MUST no longer be a navigation dead end for
  signed-out users: at least one in-page control (the brand link) MUST be
  available to return to Home.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A signed-out user on the Login page can reach Browse Courses in
  exactly one click (clicking the brand), with zero intermediate steps or
  prompts.
- **SC-002**: 100% of pages (including Login, Signup, course pages, My
  Courses, and all admin pages) render the "Libre LMS" brand as a working
  link to Home, verified for both signed-out and signed-in states.
- **SC-003**: Opening the site's root URL displays Browse Courses for 100% of
  visits, signed in or out.
- **SC-004**: Zero pages exist on which a signed-out user has no in-page means
  of navigating to Home (i.e., no navigation dead ends remain).
- **SC-005**: No regressions in existing navigation: signed-in users can still
  reach all nav links, the account menu, and the role-view toggle, and
  previously passing UI tests for navigation continue to pass.

## Assumptions

- The site's root URL currently resolves to Browse Courses (redirect or
  direct); this feature preserves and pins that behavior rather than
  introducing a new landing page.
- "Home" means the Browse Courses listing for everyone, including admins —
  even users with the Admin role view active. The brand is a universal home
  control, not a role-aware one.
- The logout flow continues to land users on the Login page (existing
  behavior is unchanged); the brand link is the new escape route from there.
- No changes to the Login form, Signup, Forgot Password, or other account
  flows are in scope; only the navbar brand and the Home destination are
  affected.
- The desktop and mobile presentations share the same navbar, so a single
  brand link serves both; no separate mobile-specific link is required.
- This is an enhancement (new navigation capability), so the work branch is a
  `story/` branch per Constitution Principle VIII.
