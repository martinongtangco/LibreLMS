# Feature Specification: Nav & Header Design Alignment

**Feature Branch**: `story/018-nav-design-alignment`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/018-nav-design-alignment`.

**Created**: 2025-08-04

**Status**: Draft

**Input**: User description: "The current implementation only partially applied the Libre LMS redesign. Please bring it in line with the approved design (see Design Handoff.pdf / Libre LMS.dc.html): 1. Header/nav bar — Replace emoji icons with Lucide SVG icons, replace plain username+Logout with avatar+name profile control with dropdown, remove standalone Logout from nav, add Learner/Admin role switcher. 2. Mobile nav (≤760px) — collapse nav links and role switcher behind hamburger, keep brand+hamburger+avatar visible. 3. General — use Organic design system tokens throughout, verify no raw hex/px values hardcoded."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Authenticated user sees the correct nav with SVG icons, role switcher, and profile dropdown (Priority: P1)

An authenticated user opens any page and sees a navigation bar with: the Libre LMS brand wordmark on the left; page links (Browse Courses, My Courses, and admin links when applicable) with Lucide SVG icons (stroke-width 2.75) instead of emoji characters; a pill-shaped Learner/Admin segmented-control switcher positioned between the page links and the profile control; and a circular initials avatar next to the user's name on the right that opens a dropdown with exactly "View Profile" and "Settings" entries. There is no standalone Logout link in the top nav — Logout lives inside the Settings page as its last row.

**Why this priority**: This is the primary surface of the feature — every page load shows the nav, and the emoji icons, missing role switcher, and misplaced Logout are the most visible deviations from the approved design.

**Independent Test**: Can be fully tested by logging in as any user and verifying: (1) nav links show SVG icons not emoji, (2) the role switcher appears between links and avatar, (3) clicking the avatar opens a dropdown with "View Profile" and "Settings", (4) no Logout link appears in the top nav bar.

**Acceptance Scenarios**:

1. **Given** an authenticated learner on any page, **When** the nav renders, **Then** each page link (Browse Courses, My Courses) displays a Lucide SVG icon before its text label — no emoji characters appear anywhere in the nav
2. **Given** an authenticated admin on any page, **When** the nav renders, **Then** all admin page links (Dashboard, Organizations, Org Chart, Learners, Courses, Enrollments, Create Course, Upload SCORM) display Lucide SVG icons before their text labels
3. **Given** an authenticated user on any page, **When** the nav renders, **Then** a pill-shaped segmented control with "Learner" and "Admin" segments appears between the page links and the profile avatar
4. **Given** an authenticated user, **When** they click the circular avatar or their name, **Then** a dropdown opens showing exactly two entries: "View Profile" and "Settings" — no "Logout" entry
5. **Given** an authenticated user, **When** the nav renders, **Then** no standalone "Logout" link appears in the top-level navigation bar

---

### User Story 2 — Mobile nav collapses behind hamburger at ≤ 760px (Priority: P2)

A user on a mobile viewport (≤ 760px) sees only the Libre LMS brand wordmark, a hamburger icon button, and the circular avatar in the top bar. Tapping the hamburger opens a dropdown panel containing the role switcher and all page links for the current role. The user's name label is hidden on narrow screens (avatar only). Tapping outside the dropdown or tapping the hamburger again closes it.

**Why this priority**: Mobile is the second-largest viewport class and the current behavior (all items stacking in place) is explicitly not the approved mobile pattern. This is a structural fix, not just cosmetic.

**Independent Test**: Can be fully tested by loading any page at a 375px viewport, verifying only brand + hamburger + avatar are visible, tapping the hamburger to see the role switcher and links appear in a dropdown, and confirming the name label is hidden.

**Acceptance Scenarios**:

1. **Given** a user on a ≤ 760px viewport, **When** the page loads, **Then** only the brand wordmark, the hamburger button, and the circular avatar are visible in the top nav bar — no page links, no role switcher, no user name label
2. **Given** a user on a ≤ 760px viewport, **When** they tap the hamburger button, **Then** a dropdown panel opens showing the role switcher followed by the page links appropriate for their role
3. **Given** the mobile dropdown is open, **When** the user taps outside the dropdown or taps the hamburger again, **Then** the dropdown closes
4. **Given** a user on a ≤ 760px viewport, **When** the page loads, **Then** the user's name label is hidden next to the avatar (only the avatar circle is visible)

---

### User Story 3 — All nav styling uses Organic design tokens with no hardcoded values (Priority: P3)

A developer or designer inspects the nav CSS and confirms that all visual properties (colors, fonts, sizes, radii, shadows) are expressed via CSS custom properties (design tokens) defined in `:root`, not raw hex codes or pixel values. The nav uses Caprasimo for the brand wordmark, Figtree for body text, terracotta/sage accent colors, and 16px/pill border radii from the token set.

**Why this priority**: Hardcoded values undermine the design system's maintainability and create drift from the approved tokens. This is a structural quality requirement rather than a visible user-facing change, so it's P3.

**Independent Test**: Can be fully tested by grepping the nav-related CSS rules and confirming zero raw `#hex` color values and zero raw `px` values appear outside the `:root` token definitions.

**Acceptance Scenarios**:

1. **Given** the nav CSS rules (outside `:root`), **When** inspected for raw hex color values, **Then** none are found — all colors use `var(--color-...)` tokens
2. **Given** the nav CSS rules (outside `:root`), **When** inspected for raw pixel values, **Then** none are found — all sizes use `var(--spacing-...)`, `var(--border-radius)`, `var(--font-size-...)`, or `var(--radius-...)` tokens
3. **Given** the nav renders, **When** inspected visually, **Then** the brand wordmark uses the Caprasimo heading font and nav text uses the Figtree body font

---

### Edge Cases

- An unauthenticated visitor: the nav shows only the brand wordmark and a "Login" link — no role switcher, no avatar, no profile dropdown.
- A user with a single-character display name: the avatar shows only that character (not "?").
- A user with no display name at all: the avatar shows "?" as a fallback without crashing.
- An admin role switcher that does NOT actually change the user's role: it is purely a UI control that toggles which set of nav links are displayed (matching the approved design), while the user's actual server-side role remains unchanged.
- Viewport at exactly 760px: the mobile breakpoint boundary must be handled consistently (mobile layout at 760px and below, desktop above).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST replace all emoji characters in nav links (📊, 🎓, 📚, 📈, 🏢, 🌳, 👥, 📕, 📋, ➕, 📤, 🗃️, etc.) with Lucide SVG icons at stroke-width 2.75, matching the approved design system spec.
- **FR-002**: System MUST replace the current plain "Logout" link in the nav with a circular initials avatar next to the user's display name, forming a clickable profile control.
- **FR-003**: The profile control dropdown MUST contain exactly two entries: "View Profile" (navigating to the existing Profile page) and "Settings" (navigating to the existing Settings page).
- **FR-004**: System MUST NOT display a "Logout" link in the top-level navigation bar. Logout MUST live as the last row inside the Settings page (already implemented in prior spec 017).
- **FR-005**: System MUST add a pill-shaped segmented-control role switcher with "Learner" and "Admin" segments, positioned between the page links and the profile control in the desktop nav.
- **FR-006**: The role switcher MUST toggle the visible set of nav links — showing learner links (Browse Courses, My Courses) when "Learner" is active, and admin links (Dashboard, Organizations, Org Chart, Learners, Courses, Enrollments, Create Course, Upload SCORM) when "Admin" is active — regardless of the user's actual server-side role.
- **FR-007**: On viewports ≤ 760px, the nav MUST collapse all page links and the role switcher behind a hamburger icon button. Only the brand wordmark, hamburger button, and circular avatar remain visible in the top bar.
- **FR-008**: On viewports ≤ 760px, tapping the hamburger MUST open a dropdown panel containing the role switcher followed by the role-appropriate page links.
- **FR-009**: On viewports ≤ 760px, the user's name label next to the avatar MUST be hidden (avatar circle only visible).
- **FR-010**: All nav styling MUST use CSS custom properties (design tokens) — no raw hex color values or raw pixel values outside `:root` definitions in the nav rules.
- **FR-011**: Nav brand wordmark MUST use the Caprasimo heading font; nav text and link labels MUST use the Figtree body font.
- **FR-012**: Nav links MUST use the Organic design system's pill border radius (var(--radius-pill)) and terracotta/sage accent colors.
- **FR-013**: The hamburger icon button MUST use a Lucide hamburger SVG icon (not the `&#9776;` Unicode character).
- **FR-014**: An unauthenticated visitor's nav MUST continue to show only the brand wordmark and a "Login" link with no avatar, role switcher, or profile control.
- **FR-015**: The profile control dropdown MUST close when clicking outside the dropdown or pressing Escape.

### Key Entities

No new data entities are introduced by this change. The nav is purely a presentational component operating on existing user identity data (display name, role) from the authentication system.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero emoji characters appear in the navigation bar across all pages and roles.
- **SC-002**: An authenticated user can reach Logout in exactly 3 interactions: avatar → Settings → Logout button, with no Logout visible in the top nav.
- **SC-003**: The mobile nav at 375px shows exactly 3 visible elements in the top bar: brand wordmark, hamburger button, and avatar circle — no links, no role switcher, no name label.
- **SC-004**: All nav CSS rules (outside `:root`) contain zero raw hex color values and zero raw pixel values when inspected via static analysis.
- **SC-005**: The role switcher toggles the visible nav link set instantly (client-side, no page reload) with the correct links for each segment.
- **SC-006**: The profile dropdown opens within 100ms of clicking the avatar or name, and closes on outside-click or Escape key.

## Assumptions

- **Lucide icons are available**: Lucide SVG icons can be included either via CDN (`@iconify` or Lucide CDN) or inlined as SVG elements. The choice of inclusion method is an implementation detail determined at planning time.
- **Role switcher is client-side only**: The Learner/Admin segmented control toggles nav link visibility client-side. It does not change the user's actual server-side role or permissions. This matches the approved design's intent — the switcher is a UI affordance for demoing different nav states, as noted in spec 017's assumptions.
- **Existing Profile and Settings pages are unchanged**: The Profile and Settings pages already exist from spec 017. This spec only changes how the user navigates to them (via the avatar dropdown instead of a nav Logout link).
- **Mobile breakpoint for nav is 760px (nav-only)**: The nav component's mobile breakpoint changes from 480px to 760px per the approved design. Page-level responsive breakpoints (card grids, tables, filters) retain the existing 480px threshold — only the nav collapses earlier.
- **Logout in Settings already exists**: Spec 017 (Organic UI Redesign) already moved Logout into the Settings page as its last row. This spec only removes the top-level nav Logout — it does not need to implement Settings logout.
- **No emoji anywhere in the UI**: The requirement states "no emoji anywhere in the UI." This spec focuses on the nav (the explicit scope), but the principle applies: any emoji discovered in nav-adjacent components during implementation should be replaced with SVG icons.
