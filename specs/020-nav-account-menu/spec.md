# Feature Specification: Navigation Bar and Account Menu

**Feature Branch**: `story/020-nav-account-menu`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2025-01-31

**Status**: Complete (merged 2026-08-05)

**Input**: User description: "Implement the top navigation bar and account menu for Libre LMS exactly to this spec. Build a single reusable nav/account component with role toggle pill, static identity display, dropdown menu, mobile hamburger menu, and responsive layouts. Logout action belongs only on the Settings page — not in the top nav."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate Between Pages via Top Bar (Priority: P1)

As a logged-in user (learner or admin), I want to see a persistent navigation bar at the top of every page with links to My Courses, Browse Courses, and (if admin) Dashboard, so I can quickly move between the main sections of the LMS.

**Why this priority**: Navigation is the primary way users move through the application. Without it, no other feature is discoverable or usable.

**Independent Test**: Can be fully tested by logging in as a learner and verifying that My Courses and Browse Courses links are visible, clickable, and route to the correct pages. Testing as admin additionally confirms the Dashboard link appears.

**Acceptance Scenarios**:

1. **Given** I am logged in as a learner, **When** I view any page, **Then** I see the navigation bar with the brand wordmark, My Courses, and Browse Courses links
2. **Given** I am logged in as an admin, **When** I view any page, **Then** I see the navigation bar with My Courses, Browse Courses, and Dashboard links
3. **Given** I am on the My Courses page, **When** I look at the nav links, **Then** My Courses is highlighted as the active link
4. **Given** I hover over a nav link, **When** I observe the visual state, **Then** the link text changes to the hover color
5. **Given** I click a nav link, **When** the page loads, **Then** I am taken to the corresponding page

---

### User Story 2 - Toggle Between Roles and Access Account Menu (Priority: P1)

As a logged-in user, I want to see a role toggle (Learner/Admin) and my account name in the top-right of the navigation bar, so I can switch my viewing role and access account-related actions without changing my identity.

**Why this priority**: Role switching is a core interaction pattern — the same person needs to see both learner and admin perspectives. The account menu provides access to profile and settings.

**Independent Test**: Can be fully tested by clicking the Learner/Admin toggle buttons and verifying that navigation options change (Dashboard appears/disappears) while the displayed name remains constant. Clicking the account control opens/closes the dropdown with View Profile and Settings options.

**Acceptance Scenarios**:

1. **Given** I am logged in, **When** I see the account control on the right side of the nav, **Then** I see a role toggle pill with Learner and Admin options, my static name, and a chevron icon
2. **Given** the role toggle shows Learner as active, **When** I click the Admin button in the pill, **Then** Admin becomes highlighted, Learner becomes unhighlighted, and the Dashboard nav link appears
3. **Given** the role toggle shows Admin as active, **When** I click the Learner button in the pill, **Then** Learner becomes highlighted, Admin becomes unhighlighted, and the Dashboard nav link disappears
4. **Given** I am viewing the account control, **When** I toggle between Learner and Admin roles, **Then** my displayed name remains "Alice Johnson" and does not change
5. **Given** the dropdown is closed, **When** I click the account control (outside the role pill), **Then** the dropdown menu opens showing View Profile and Settings options
6. **Given** the dropdown is open, **When** I click the account control again, **Then** the dropdown menu closes
7. **Given** the dropdown is open, **When** I click a role toggle button inside the account control, **Then** the role changes but the dropdown does not close (click is contained by stopPropagation)

---

### User Story 3 - Access Profile and Settings from Account Dropdown (Priority: P2)

As a logged-in user, I want to navigate to my profile and settings pages from the account dropdown, so I can manage my preferences and account details.

**Why this priority**: Profile and settings are secondary but essential account management functions. The dropdown provides the only entry point to these pages from the top navigation.

**Independent Test**: Can be fully tested by opening the account dropdown and clicking View Profile or Settings, verifying navigation to the correct pages.

**Acceptance Scenarios**:

1. **Given** I have opened the account dropdown, **When** I click "View Profile", **Then** I am navigated to the profile page
2. **Given** I have opened the account dropdown, **When** I click "Settings", **Then** I am navigated to the settings page
3. **Given** I am viewing the account dropdown, **When** I inspect the menu items, **Then** I see only View Profile and Settings (no Logout option)

---

### User Story 4 - Logout from Settings Page (Priority: P2)

As a logged-in user, I want to find the Logout action on the Settings page as the last item in a preferences list, so I can end my session without confusion from multiple logout locations.

**Why this priority**: Having a single, consistent logout location reduces confusion. The Settings page is the logical place for session management.

**Independent Test**: Can be fully tested by navigating to Settings and verifying that Logout appears as the last row after Email notifications and Theme options.

**Acceptance Scenarios**:

1. **Given** I am on the Settings page, **When** I scroll to the preferences list, **Then** I see Email notifications, Theme, and Logout as the last row
2. **Given** I am viewing the top navigation bar, **When** I inspect all visible elements including the account dropdown, **Then** I do not see any Logout option in the top nav
3. **Given** I am on the Settings page, **When** I click the Logout row, **Then** I am logged out and redirected to the login page

---

### User Story 5 - Mobile Navigation Experience (Priority: P3)

As a user on a small screen device, I want the navigation bar to collapse into a hamburger menu that provides access to all pages, role switching, and account options, so I have a usable experience on mobile devices.

**Why this priority**: Mobile access is important but secondary to the desktop experience. The responsive layout ensures usability on smaller screens without compromising the primary navigation flow.

**Independent Test**: Can be fully tested by resizing the browser to 760px or less and verifying the hamburger menu opens, contains role toggle and page links, and that the account name is hidden.

**Acceptance Scenarios**:

1. **Given** my viewport is 760px wide or less, **When** I view the navigation bar, **Then** I see a hamburger icon button instead of the nav links and role pill
2. **Given** I am on mobile view, **When** I click the hamburger button, **Then** a dropdown opens showing the role toggle pill and page links stacked vertically
3. **Given** I am on mobile view, **When** I look at the account control area, **Then** I see the role pill and chevron but not my account name
4. **Given** I am on mobile view and a page link is active, **When** I view the hamburger menu, **Then** the active link has a tinted accent background and accent-colored text
5. **Given** my viewport is 760px wide or less, **When** I view a page, **Then** the page heading is 28px (not 36px), page padding is reduced, and toolbars/hero rows stack vertically

---

### Edge Cases

- What happens when the user clicks outside the account dropdown while it is open? The dropdown should close.
- What happens when the user clicks a role toggle button that is already active? No state change should occur.
- What happens when the viewport is exactly 760px? The mobile layout should be active (≤760px breakpoint).
- What happens when the user navigates to a new page while the dropdown is open? The dropdown should close.
- What happens when the user resizes from desktop to mobile while the hamburger menu is closed? The nav should reflow to the mobile layout without state loss.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST display a persistent navigation bar at the top of every authenticated page with a brand wordmark, navigation links, and an account control on the right side
- **FR-002**: The navigation bar MUST show My Courses and Browse Courses links for all authenticated users
- **FR-003**: The navigation bar MUST show a Dashboard link only when the user's active role is Admin
- **FR-004**: The account control MUST contain a role toggle pill with Learner and Admin buttons that are always visible regardless of current role
- **FR-005**: Clicking a role toggle button MUST change the active role state and update which navigation links are visible, without changing the displayed user name
- **FR-006**: The displayed user name in the account control MUST remain static ("Alice Johnson") regardless of role changes
- **FR-007**: The account control MUST be a `<div role=button tabindex=0>` element — not a `<button>` — because it contains nested interactive buttons
- **FR-008**: Clicking the role toggle pill MUST call `event.stopPropagation()` to prevent the account dropdown from opening simultaneously
- **FR-009**: Clicking the account control (outside the role pill) MUST open or close a dropdown menu with View Profile and Settings options
- **FR-010**: The account dropdown MUST contain only two rows: View Profile and Settings (no Logout)
- **FR-011**: The Settings page MUST contain a Logout action as the last row of a preferences list (after Email notifications and Theme)
- **FR-012**: Logout MUST NOT appear anywhere in the top navigation bar or account dropdown
- **FR-013**: When the viewport is 760px wide or less, the navigation bar MUST hide nav links and show a hamburger icon button instead
- **FR-014**: The hamburger menu MUST contain the role toggle pill (centered) and page links stacked as full-width rows
- **FR-015**: On mobile (≤760px), the account name label MUST be hidden in the collapsed bar
- **FR-016**: On mobile (≤760px), the page heading MUST scale to 28px, page padding MUST reduce to 24px 16px 32px, and toolbars/hero rows MUST switch to column layout
- **FR-017**: Active navigation links MUST be visually distinguished from inactive links (different color)
- **FR-018**: Hover states MUST be provided for nav links, account control, dropdown rows, and the hamburger button
- **FR-019**: The navigation component MUST be a single reusable component used across all authenticated pages
- **FR-020**: The color scheme MUST use the specified hex colors: nav background #201e1d, hover states #3a3634, page background #f5ead8, primary accent #c67139, accent text #8a4f26, nav link default #c9c2ba, border #e4d9c8

### Non-Functional Requirements

- **NFR-001**: The navigation bar layout MUST be responsive and adapt at the 760px breakpoint
- **NFR-002**: The role toggle interaction MUST be keyboard accessible (the account control has `tabindex=0`)
- **NFR-003**: Dropdown menus MUST close when the user clicks outside them
- **NFR-004**: The navigation component MUST not cause layout shifts or flicker when role state changes

### Key Entities

- **Navigation State**: Tracks the current active role (Learner/Admin), active page, and dropdown open/closed state
- **User Identity**: Static display properties (name "Alice Johnson") that remain constant across role changes
- **Navigation Links**: Dynamic set of page links determined by active role (My Courses, Browse Courses always; Dashboard conditionally for Admin)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can navigate between My Courses, Browse Courses, and (if admin) Dashboard by clicking a single nav link, completing the navigation within one click
- **SC-002**: Users can switch between Learner and Admin roles by clicking a single button in the role toggle pill, with the navigation updating immediately
- **SC-003**: The displayed user name remains constant across all role toggle interactions (100% of toggles preserve the name)
- **SC-004**: The account dropdown opens and closes reliably with a single click on the account control
- **SC-005**: Users on mobile devices (≤760px viewport) can access all navigation functions through the hamburger menu
- **SC-006**: The Logout action is accessible from exactly one location (Settings page) — zero instances in the top nav
- **SC-007**: All interactive elements in the navigation (links, toggle buttons, dropdown, hamburger) have visible hover states
- **SC-008**: The navigation layout renders correctly at viewports from 320px to 1920px without horizontal overflow

## Assumptions

- The existing authentication system already provides a logged-in user context with a name ("Alice Johnson")
- The existing routing system supports navigation to My Courses, Browse Courses, Dashboard, Profile, and Settings pages
- The application already has a role system (Learner/Admin) that can be toggled in application state
- The Settings page already exists or will be created as part of this or a parallel slice with Email notifications and Theme options
- The Logout functionality (session termination and redirect) already exists and can be reused on the Settings page
- The specified hex colors are the final design tokens and do not conflict with any existing design system
- The font stack includes a serif/display face available for the brand wordmark
- Icon assets (chevron, hamburger, user, settings) are available as SVG or inline components with configurable size and stroke-width
