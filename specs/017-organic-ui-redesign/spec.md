# Feature Specification: Organic Design System Redesign

**Feature Branch**: `story/017-organic-ui-redesign`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/017-organic-ui-redesign`.

**Created**: 2026-08-03

**Status**: Complete (merged 2026-08-03)

**Input**: User description: "review new design documents to redesign Libre LMS. Read the handoffs, pdfs, templates" — referencing a design handoff bundle (`LibreLMS_designPhilosophy.pdf`, `README.md`, `Libre LMS.dc.html`) that specifies an **Organic** design system (warm cream ground, terracotta + sage accents, Caprasimo/Figtree type, heavily rounded shapes) for a redesigned My Courses, Browse Courses, Course Detail, Admin Dashboard, and Profile/Settings experience, plus a restyled nav shell and mobile responsiveness.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Learner browses and manages courses in the new visual system (Priority: P1)

A learner opens My Courses, Browse Courses, and Course Detail and sees the same information and actions they have today (enrolled courses, status, search/filter, enroll), now presented in the warm, rounded Organic visual system instead of the current styling — with no loss of existing functionality.

**Why this priority**: These three screens are the core, highest-traffic learner journey. They deliver the majority of the redesign's visible value and carry the least functional risk since no business logic changes.

**Independent Test**: Can be fully tested by logging in as a learner, viewing My Courses (enrolled cards with status/progress), navigating to Browse Courses (search + filter + enroll), and opening a Course Detail page — confirming all existing behaviors still work and the new visual system (colors, type, rounded cards/tags/buttons) is applied throughout.

**Acceptance Scenarios**:

1. **Given** a learner with enrolled courses, **When** they open My Courses, **Then** each course renders as a rounded card with a category kicker, title, description, a status tag ("Not Started"/"In Progress"/"Completed"), an hours tag, a pill-shaped progress bar, and an "Enrolled {date} · {pct}% complete" line
2. **Given** a learner with no enrolled courses, **When** they open My Courses, **Then** they see a centered empty-state card with the message "You haven't enrolled in any courses yet." and a primary button to Browse Courses
3. **Given** a learner on Browse Courses, **When** they type into the search box or choose a category, **Then** the grid filters to matching courses exactly as it does today, now shown in the Organic card style with a category tag, hours tag, an "✓ Enrolled" tag where applicable, and a "View Details" button
4. **Given** a learner on Browse Courses with a search that matches nothing, **When** the grid is empty, **Then** a muted "No courses match your search." message is shown
5. **Given** a learner opens a course's detail page, **When** the course is not yet enrolled, **Then** they see the category/hours tags, title, description, and a primary "Enroll now" button that enrolls them on click
6. **Given** a learner opens a course's detail page, **When** the course is already enrolled, **Then** the CTA is a disabled "✓ Enrolled" button instead of "Enroll now"

---

### User Story 2 - Admin views platform activity in the new visual system (Priority: P2)

An admin (SuperUser or OrgAdmin) opens the Dashboard and sees the same scoped metrics they see today (organizations/learners/courses/enrollments/completion), now presented as Organic-styled stat tiles, plus a table of all courses visible to them with category, hours, and enrollment count per course.

**Why this priority**: Admin usage is less frequent than learner browsing but is the second core audience explicitly covered by the design handoff.

**Independent Test**: Can be fully tested by logging in as SuperUser and as OrgAdmin and confirming the Dashboard shows the correct scoped stat tiles and an all-courses table with accurate per-course enrollment counts, styled per the Organic system.

**Acceptance Scenarios**:

1. **Given** an admin opens Dashboard, **When** the page loads, **Then** their existing scoped metrics (organizations, learners, courses, enrollments, average completion) render as stat tiles with a kicker label and a large accent-colored number
2. **Given** an admin opens Dashboard, **When** they scroll to the courses table, **Then** every course visible to their scope is listed with its category tag, hours, and current enrollment count

---

### User Story 3 - Any authenticated user manages their profile and settings (Priority: P3)

An authenticated user clicks their avatar/name in the nav and gets a small dropdown with "View Profile" and "Settings" (no top-level Logout). View Profile shows their name, role, and email. Settings lets them toggle email notifications and pick a theme preference, and contains the Logout action as its last row.

**Why this priority**: This is new user-facing surface (profile/settings pages don't exist today) rather than a re-skin of existing functionality, so it's lower priority than the two learner/admin journeys above, but it's explicitly called out in the design handoff and needed for Logout to have a home once it's removed from the top nav.

**Independent Test**: Can be fully tested by clicking the profile control, opening View Profile (confirm name/role/email display), opening Settings (toggle email notifications and theme, reload the page, confirm the choice persisted), and logging out from the Settings page.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they click their avatar/name in the nav, **Then** a dropdown opens with exactly two entries: "View Profile" and "Settings"
2. **Given** a user opens View Profile, **When** the page loads, **Then** it shows their name, role label, and email in a bordered-row card
3. **Given** a user opens Settings and toggles email notifications or changes the theme preference, **When** they reload Settings later, **Then** their chosen values are still shown (the change persisted)
4. **Given** a user is on the Settings page, **When** they click "Logout" (the last row), **Then** they are logged out, matching current logout behavior

---

### User Story 4 - Mobile users get the same experience in the new visual system (Priority: P4)

A user on a phone (≤ 760px) uses My Courses, Browse Courses, Course Detail, Dashboard, and the profile menu exactly as they do on desktop today (via the existing hamburger nav and responsive stacking established in prior work), now wrapped in the Organic visual system.

**Why this priority**: The responsive mechanics already exist (prior mobile-UI work); this story is about carrying that behavior through the new visual system rather than building it from scratch, so it rides behind the three stories above.

**Independent Test**: Can be fully tested by loading each redesigned page at a 375px viewport width and confirming: the nav collapses behind a hamburger button containing the page links, headings shrink, toolbars and the course-detail hero stack vertically, and no horizontal scrolling occurs.

**Acceptance Scenarios**:

1. **Given** a user on a ≤ 760px viewport, **When** they open the hamburger menu, **Then** it shows the role-appropriate page links (My Courses/Browse Courses for learners, Browse Courses/Dashboard for admins) styled per the Organic system
2. **Given** a user on a ≤ 760px viewport, **When** they view any redesigned page, **Then** the H1 renders at the mobile size, toolbars/hero blocks stack in a single column, and no content requires horizontal scrolling

---

### Edge Cases

- A learner's enrolled course has no SCORM attempt yet (never launched): treated as "Not Started" / 0% complete, not an error.
- A brand-new organization/admin with zero courses, learners, or enrollments: Dashboard stat tiles show 0 without erroring, and the courses table shows an empty state rather than breaking.
- Very long course titles or descriptions: card text truncates/wraps without breaking the card layout or overlapping neighboring cards.
- A user record with no display name available: the avatar falls back to a reasonable initials placeholder rather than rendering blank.
- An unauthenticated visitor: the nav shows only a "Login" link — no avatar, profile menu, or Settings/Logout affordance.
- Switching a Settings preference and immediately closing the tab: the change must already be persisted (not just held in page state) since there is no explicit "Save" step described in the design.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST restyle the top navigation bar (brand wordmark, page links, profile control) using the Organic design system's color, typography, and shape tokens, applied consistently across every page the shared layout renders.
- **FR-002**: System MUST replace the current inline username-and-Logout nav control with an avatar (initials) + name control that opens a dropdown containing exactly "View Profile" and "Settings"; Logout MUST NOT appear as a top-level nav item.
- **FR-003**: My Courses MUST render each enrolled course as a card with: category kicker, title, one-line description, a status tag ("Not Started" / "In Progress" / "Completed"), an hours/duration tag, an 8px pill progress bar, and an "Enrolled {date} · {pct}% complete" meta line.
- **FR-004**: System MUST derive each enrolled course's status tag and progress percentage from the learner's most recent course attempt: no attempt → "Not Started" / 0%; an in-progress attempt → "In Progress"; a completed/passed attempt → "Completed" / 100%.
- **FR-005**: My Courses MUST show a centered empty state ("You haven't enrolled in any courses yet." + a primary button to Browse Courses) when the learner has no enrollments.
- **FR-006**: Browse Courses MUST retain existing search (case-insensitive title substring match) and category filter (exact match) behavior, plus a "Clear" action resetting both, restyled per the Organic toolbar.
- **FR-007**: Browse Courses cards MUST show a category tag, an hours tag, an "✓ Enrolled" tag when the learner is already enrolled in that course, and a "View Details" action.
- **FR-008**: Browse Courses MUST show a muted "No courses match your search." message when the filtered result set is empty.
- **FR-009**: Course Detail MUST show a "Back to courses" control, a tag row (category, hours), the course title and description, and either a primary "Enroll now" button (not yet enrolled, wired to the existing enroll action) or a disabled "✓ Enrolled" secondary button (already enrolled).
- **FR-010**: Admin Dashboard MUST present the existing scoped summary metrics (organizations, learners, courses, enrollments, average completion — scoped by SuperUser vs. OrgAdmin exactly as today) as stat tiles with a kicker label and a large accent-colored number.
- **FR-011**: Admin Dashboard MUST include an "All Courses" table listing every course visible to the admin's scope, with title, category tag, hours/duration, and an enrollment count computed from existing enrollment records.
- **FR-012**: System MUST provide a "View Profile" page showing the authenticated user's name, role label, and email in a bordered-row card.
- **FR-013**: System MUST provide a "Settings" page with an email-notifications toggle and a theme-preference selector, each persisted per user, plus a "Logout" action as the page's last row.
- **FR-014**: Changing the email-notifications or theme setting MUST persist immediately (no separate save step) and MUST still reflect the chosen value the next time Settings loads.
- **FR-015**: All redesigned pages MUST preserve existing responsive behavior — nav collapses behind a hamburger control with role-appropriate links at ≤ 760px, headings shrink, toolbars and the course-detail hero stack vertically — under the new visual system.
- **FR-016**: Interactive elements (buttons, links, inputs, the profile/hamburger dropdowns) on redesigned pages MUST use the Organic design system's shared hover/pressed/focus states rather than page-specific style overrides.
- **FR-017**: Pages not explicitly covered by this redesign (Login, SCORM Launch, and the Admin Organizations/Learners/Course-management/Enrollments/Upload screens) MUST continue to function exactly as today this slice, only inheriting the shared nav-chrome restyle from FR-001.

### Key Entities

- **Course**: Existing catalog entry (title, description, category, duration). No structural change — only its presentation changes.
- **Enrollment**: Existing student-to-course relationship and enrollment date, used to populate My Courses and the enrollment count shown on Browse Courses and the admin course table.
- **Course Attempt**: Existing per-learner attempt record whose status is used to derive the "Not Started / In Progress / Completed" tag and progress percentage shown on My Courses.
- **User Preference** *(new)*: One record per user capturing their email-notification opt-in and theme preference, read and written by the Settings page.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A learner can identify whether a course is Not Started, In Progress, or Completed within 2 seconds of viewing My Courses, without opening the course.
- **SC-002**: All six redesigned screens (My Courses, Browse Courses, Course Detail, Dashboard, View Profile, Settings) render with zero horizontal scrolling at 375px, 768px, and 1280px viewport widths.
- **SC-003**: A learner can go from landing on Browse Courses to triggering a course's enrollment action in 3 or fewer interactions (search/filter optional).
- **SC-004**: An authenticated user can reach Logout in 2 clicks or fewer (profile menu → Settings → Logout), with no Logout control cluttering the top-level nav.
- **SC-005**: 100% of existing acceptance scenarios for search, filtering, enrollment, and admin metrics (as verified by current automated tests) continue to pass after the redesign, confirming no functional regressions from the visual change.
- **SC-006**: An admin can read every scoped summary metric (organizations/learners/courses/enrollments/completion) without scrolling on a 1280px-wide desktop viewport.

## Assumptions

- **Scope of screens**: This redesign covers exactly the screens named in the design handoff — My Courses, Browse Courses, Course Detail, Admin Dashboard, View Profile, Settings — plus the shared nav shell. Other existing pages (Login, SCORM Launch, Admin Organizations/Learners/Course-management/Enrollments/Upload) are out of scope for this slice and keep their current inner styling, per the constitution's thin-vertical-slice principle; a follow-up spec would extend the design system to them.
- **Nav link set unchanged**: The design handoff's mockup nav shows only My Courses/Browse Courses/Dashboard because it's a simplified prototype. In the real app, all existing role-gated nav links (Organizations, Org Chart, Learners, Courses, Enrollments, Create Course, Upload SCORM) are retained and restyled — none are removed — since removing them is a separate RBAC/IA decision outside this redesign's intent.
- **No manual role switcher**: The design's Learner/Admin pill toggle was a device for demoing both states in one static prototype. The real app already drives nav visibility from the authenticated user's actual role (existing RBAC), so no user-facing role-switching control is added.
- **Syllabus checklist deferred**: The Course Detail mockup's per-module syllabus checklist has no backing data model in the current Catalog domain (courses have no "modules" concept yet). Building that content model is out of scope for a visual redesign; Course Detail ships without the syllabus section this slice.
- **Dashboard course table via aggregation, not new schema**: The "All Courses" table's enrollment counts are computed by counting existing Enrollment records per course at read time — no new schema is introduced for this.
- **Settings persistence added, minimally**: Because Settings is a real (not decorative) page, its two preferences are given lightweight real persistence (one small per-user preference record) rather than being non-functional. The theme preference is stored and displayed, but only the single Organic light theme currently has design tokens — selecting a different theme value does not yet change the rendered appearance; visual theme-switching is out of scope until additional theme tokens exist.
- **Existing enroll/search/filter logic is reused as-is**: This is a visual and information-architecture redesign of existing screens, not a rewrite of their underlying business logic (enrollment rules, search/filter matching, metric calculations).
