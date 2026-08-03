# Feature Specification: Responsive Mobile UI

**Feature Branch**: `story/015-responsive-mobile-ui`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2025-08-03

**Status**: Draft

**Input**: User description: "we need to be able to provide a great mobile experience. lets enhance the UI to be responsive UI"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse and enroll in courses on mobile (Priority: P1)

A student opens Libre LMS on their phone and wants to browse the course catalog, search for courses, view course details, and enroll — all on a small screen. The layout should adapt gracefully: the navigation should be accessible without horizontal scrolling, course cards should stack in a single column, and buttons should be large enough to tap comfortably.

**Why this priority**: This is the core user journey. If students can't browse and enroll on mobile, the LMS is effectively unusable for them. This covers the most frequent user activity.

**Independent Test**: Can be fully tested by loading the Browse Courses and Course Detail pages on a 375px-wide viewport and completing: search, filter by category, view a course detail, and enroll — all without horizontal scrolling or tap-target issues.

**Acceptance Scenarios**:

1. **Given** I am viewing the course catalog on a phone (≤ 480px), **When** I scroll through the course list, **Then** each course card displays in a single-column layout with full title, description, and enroll button visible without horizontal scrolling
2. **Given** I am viewing the navigation bar on a phone, **When** I interact with the navigation, **Then** I can access all navigation links through a collapsible menu without them overflowing off-screen
3. **Given** I am on the course detail page on a phone, **When** I tap the enroll button, **Then** the tap target is at least 44x44 pixels and the enrollment action completes successfully
4. **Given** I am searching for courses on a phone, **When** I type in the search field, **Then** the input is full-width and easily tappable

---

### User Story 2 - Manage learners and organizations on tablet/desktop admin views (Priority: P2)

An administrator opens the admin dashboard, learner management, and organization pages on a tablet or smaller laptop. Data tables should remain readable, filter controls should wrap gracefully, and action buttons should be accessible without zooming or horizontal scrolling.

**Why this priority**: Admin operations are less frequent than student browsing but critical for system management. A tablet-friendly admin interface enables on-the-go administration.

**Independent Test**: Can be fully tested by loading the admin dashboard, learner list, and organization list on a 768px-wide viewport and verifying: metric cards reflow into a 2-column grid, tables are horizontally scrollable, and filter controls wrap to multiple rows.

**Acceptance Scenarios**:

1. **Given** I am viewing the admin dashboard on a tablet (768px), **When** I view the metric cards, **Then** they reflow into a 2-column grid layout instead of a single row
2. **Given** I am viewing the learner management table on a tablet, **When** the table is wider than the viewport, **Then** the table scrolls horizontally while page chrome (filters, buttons) remains visible
3. **Given** I am using filter controls on a smaller screen, **When** there is insufficient horizontal space, **Then** the filter inputs and selects wrap to multiple rows without overlapping
4. **Given** I am viewing the organizations page on a phone, **When** I interact with org cards or list items, **Then** all action buttons are visible and tappable without horizontal scrolling

---

### User Story 3 - Login and authenticate on mobile (Priority: P3)

A user opens Libre LMS on a phone and needs to log in. The login form should be centered, inputs should be full-width, and the sign-in button should be easily tappable.

**Why this priority**: Authentication is the gateway to all functionality. The login page already has a max-width constraint but needs consistent mobile-friendly styling.

**Independent Test**: Can be fully tested by loading the login page on a 375px-wide viewport and verifying: the form is centered and full-width, inputs are tappable, and the sign-in button fills the width.

**Acceptance Scenarios**:

1. **Given** I am on the login page on a phone, **When** I view the form, **Then** the email and password inputs are full-width and easily tappable
2. **Given** I am on the login page on a phone, **When** I tap the sign-in button, **Then** it spans the full form width and has a minimum 44px height

---

### User Story 4 - View SCORM course progress on mobile (Priority: P3)

An enrolled student wants to check their enrolled courses and SCORM progress on their phone. The "My Courses" page should display course rows or cards in a mobile-friendly layout.

**Why this priority**: Progress tracking is a common mobile use case — students check status on-the-go. Lower priority because it builds on Story 1's responsive foundation.

**Independent Test**: Can be fully tested by loading My Courses on a 375px viewport and verifying each enrolled course displays with status, score, and launch button in a readable, tappable layout.

**Acceptance Scenarios**:

1. **Given** I am viewing My Courses on a phone, **When** I scroll through my enrolled courses, **Then** each course entry displays course title, status, and progress in a card or stacked layout without horizontal scrolling
2. **Given** I have a SCORM course enrolled, **When** I view its status on a phone, **Then** the launch button is clearly visible and tappable

### Edge Cases

- What happens when the viewport is between 481px and 767px (small tablets / landscape phones) — the layout should not break or show half-responsive states
- How does the navigation handle very long admin menu sections (6+ links) on narrow screens
- What happens when a data table row contains very long text (e.g., email addresses) on narrow screens — text should wrap or truncate gracefully
- How does the SCORM launch page (which embeds content in an iframe) behave on mobile — it should still function even if the embedded content is not itself responsive

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The navigation bar MUST provide full access to all links on viewports ≤ 480px without horizontal overflow, using a collapsible menu pattern
- **FR-002**: Course cards on the Browse Courses page MUST display in a single-column layout on viewports ≤ 480px and adapt to multi-column layouts on wider viewports
- **FR-003**: All interactive elements (buttons, links, form inputs, selects) MUST have a minimum touch target size of 44x44 CSS pixels on mobile viewports
- **FR-004**: Data tables in admin pages MUST be horizontally scrollable on viewports where the table exceeds the viewport width, without causing page-level horizontal scrolling
- **FR-005**: Filter and search controls MUST wrap to multiple rows on narrow viewports instead of overflowing horizontally
- **FR-006**: The page content container MUST use the full available width on mobile viewports (≤ 480px) and maintain a max-width constraint on desktop viewports
- **FR-007**: Dashboard metric cards MUST reflow from a single-row layout to a multi-column grid on narrower viewports
- **FR-008**: The login form MUST display correctly centered on mobile viewports with full-width inputs and button
- **FR-009**: My Courses and course detail pages MUST display enrollment status, progress indicators, and action buttons in a mobile-friendly stacked layout
- **FR-010**: All pages MUST include the viewport meta tag and must not require pinch-to-zoom to read content on mobile devices
- **FR-011**: Organization management pages (list, chart, detail) MUST remain usable on viewports ≤ 768px
- **FR-012**: The visual appearance on desktop viewports (≥ 1024px) MUST remain substantially unchanged from the current design

### Key Entities

No new data entities are introduced by this feature. This is purely a presentation-layer enhancement.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All pages render without horizontal scrolling on a 375px viewport width (iPhone SE / small phone baseline)
- **SC-002**: All interactive elements (buttons, links, form fields) meet WCAG 2.1 minimum target size of 44x44 CSS pixels on mobile viewports
- **SC-003**: The navigation menu is fully accessible and operable on viewports ≤ 480px within 2 taps from any navigation link
- **SC-004**: Data tables remain readable (no text clipping or column collapse) on viewports ≥ 375px through horizontal scrolling
- **SC-005**: Page layout transitions smoothly between mobile (≤ 480px), tablet (481px–768px), and desktop (≥ 769px) breakpoints without jarring reflows
- **SC-006**: Desktop experience (≥ 1024px) remains visually consistent with the current design — no degradation of the existing desktop layout
- **SC-007**: The core student journey (browse → view detail → enroll) completes successfully on a 375px viewport without any usability blockers

## Assumptions

- The current Razor Pages + HTMX architecture is retained; this is a CSS and layout enhancement, not a framework migration
- The existing `<meta name="viewport" content="width=device-width, initial-scale=1.0">` tag already present in the layout is sufficient
- No new JavaScript frameworks (React, Vue, etc.) will be introduced; responsive behavior is achieved through CSS media queries and layout adjustments
- The embedded SCORM content (served via iframe) is outside the scope of this responsive effort — the container page adapts, but the SCORM content itself is authored by third parties
- Touch-friendly navigation uses a hamburger/collapsible pattern rather than bottom navigation tabs, to maintain consistency with the current desktop navbar structure
- Admin pages are expected to be functional on tablets (≥ 768px) with graceful degradation on phones; they do not need to be as polished as the student-facing pages at mobile width
