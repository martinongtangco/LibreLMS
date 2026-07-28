# Feature Specification: HTMX + Razor Modern UI

**Feature Branch**: `story/004-htmx-razor-conversion`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/004-htmx-razor-conversion`.

**Created**: 2025-07-28

**Status**: Draft

**Input**: User description: "HTMLX + Razor conversion project with modern SPA"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse and Filter Courses Without Page Reloads (Priority: P1)

As a student, I want to browse the course catalog and filter by search term or category without the entire page refreshing, so that I can explore courses fluidly without losing my place or waiting for full page loads.

**Why this priority**: This is the primary entry point for all users. Making catalog browsing feel instant and SPA-like is the biggest UX win from HTMX adoption. Without it, the rest of the experience feels disjointed.

**Independent Test**: Can be fully tested by navigating to the catalog page, typing in the search box, selecting a category filter, and confirming that only the course list updates without the navigation bar, footer, or page URL changing.

**Acceptance Scenarios**:

1. **Given** I am on the course catalog page, **When** I type a search term, **Then** the course list updates automatically within a short delay without a full page reload
2. **Given** I am on the course catalog page, **When** I select a category from the dropdown, **Then** the course list updates to show only courses in that category without a full page reload
3. **Given** I have applied filters, **When** I clear the filters, **Then** the full course list is shown without a full page reload
4. **Given** I am browsing courses and the API is temporarily unavailable, **Then** I see a friendly error message in the course list area and the rest of the page remains functional

---

### User Story 2 - Enroll in a Course with Inline Feedback (Priority: P1)

As a student, I want to enroll in a course from the detail page and see the result immediately without a full page reload, so that enrollment feels instant and I get clear feedback on whether it succeeded.

**Why this priority**: Enrollment is the core conversion action. The current flow requires a `location.reload()` after a 1.5-second timeout, which is jarring. HTMX can make this a smooth inline swap.

**Independent Test**: Can be fully tested by viewing a course detail page, clicking "Enroll", and confirming that the enrollment button and status badge update inline without a page reload.

**Acceptance Scenarios**:

1. **Given** I am viewing a course detail page for a course I am not enrolled in, **When** I click the enroll button, **Then** the button is replaced with an "Enrolled" badge and a launch option (if SCORM) without a full page reload
2. **Given** I am viewing a course detail page for a course I am already enrolled in, **When** I attempt to enroll again, **Then** I see a message indicating I am already enrolled without a full page reload
3. **Given** I am viewing a course detail page and the enrollment API fails, **Then** I see an error message inline and can retry enrollment

---

### User Story 3 - Navigate to Course Details Within the Page (Priority: P2)

As a student, I want to click on a course card and see the course details load in the main content area without a full page reload, so that my browsing experience feels like a modern single-page application.

**Why this priority**: This deepens the SPA feel beyond filtering. It keeps users in-context as they explore courses and reduces perceived latency.

**Independent Test**: Can be fully tested by clicking a course title from the catalog list and confirming that course details appear in the main content area while the navigation bar and URL context are preserved.

**Acceptance Scenarios**:

1. **Given** I am on the course catalog page, **When** I click a course title, **Then** the course details load in the main content area without a full page reload
2. **Given** I have navigated to a course detail inline, **When** I click "Back to Catalog", **Then** the course list is restored without a full page reload
3. **Given** I have navigated to a course detail inline, **When** I refresh the browser, **Then** I land on the appropriate full page (catalog or detail) based on the URL

---

### User Story 4 - View My Courses with Live Status (Priority: P2)

As a student, I want my enrolled courses page to show live SCORM completion status and scores without requiring a full page reload, so that I always see my current progress.

**Why this priority**: Students returning to check progress should see current data. HTMX polling or triggered swaps keep this page fresh.

**Independent Test**: Can be fully tested by viewing the "My Courses" page and confirming that enrollment list and SCORM status badges are displayed and can refresh without a full page reload.

**Acceptance Scenarios**:

1. **Given** I am on the "My Courses" page, **When** the page loads, **Then** my enrolled courses with current status badges are displayed
2. **Given** I am on the "My Courses" page, **When** I click a refresh trigger, **Then** the enrollment status and SCORM scores update without a full page reload
3. **Given** I have no enrollments, **When** I view "My Courses", **Then** I see an empty state with a link to browse courses

---

### User Story 5 - Upload SCORM Packages with Progress Feedback (Priority: P3)

As an admin, I want to upload SCORM packages and see progress feedback and results inline, so that I know the upload succeeded or failed without navigating away.

**Why this priority**: SCORM upload is an admin-only flow used less frequently. It benefits from HTMX but is lower impact than student-facing features.

**Independent Test**: Can be fully tested by navigating to the admin upload page, selecting a ZIP file, uploading it, and confirming that the result (success or error) displays inline without a page reload.

**Acceptance Scenarios**:

1. **Given** I am on the SCORM upload page as an admin, **When** I select a valid ZIP file and submit, **Then** I see a success message with the package details without a full page reload
2. **Given** I am on the SCORM upload page as an admin, **When** I submit an invalid file, **Then** I see an error message explaining what went wrong without a full page reload
3. **Given** I am not authenticated as an admin, **When** I attempt to access the upload page, **Then** I am redirected to login

### Edge Cases

- What happens when HTMX is blocked by browser extensions or ad-blockers? The page should degrade gracefully to full-page form submissions.
- How does the system handle concurrent HTMX requests (e.g., rapid filter typing)? Requests should be debounced or cancelled appropriately.
- What happens when the API returns unexpected or malformed responses? HTMX swaps should show a user-friendly error message rather than breaking the page.
- How does browser back/forward navigation work with inline content swaps? The browser URL should reflect the current view state, or the user should be informed that inline navigation doesn't update browser history.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST load the HTMX library on every page of the web portal
- **FR-002**: Course catalog search and category filter MUST update the course list via partial page swap without a full page reload
- **FR-003**: Search input MUST be debounced to avoid excessive API requests (reasonable delay: 300-500ms after user stops typing)
- **FR-004**: Course enrollment MUST provide inline feedback (success, already enrolled, or error) without a full page reload
- **FR-005**: Course detail views MUST be loadable via partial page swap from the catalog list
- **FR-006**: "My Courses" page MUST display enrollment status and SCORM completion data that can refresh via partial page swap
- **FR-007**: SCORM upload MUST show progress and result feedback inline without a full page reload
- **FR-008**: System MUST degrade gracefully to full-page navigation when the interactivity layer is unavailable (e.g., scripts disabled, blocked by browser extensions)
- **FR-009**: All HTMX interactions MUST preserve the existing authentication and authorization model (cookie-based, with role checks)
- **FR-010**: Navigation bar, footer, and shared layout elements MUST remain stable during HTMX partial swaps
- **FR-011**: System MUST display user-friendly error messages when API calls fail during HTMX interactions
- **FR-012**: Inline HTMX navigation MUST be consistent with full-page navigation (same content, same styling, same links work both ways)

### Key Entities

No new data entities are introduced by this feature. HTMX operates at the presentation layer and interacts with existing entities through the existing API endpoints:

- **Course**: Existing entity from Catalog module (title, description, category, duration)
- **Enrollment**: Existing entity from Enrollment module (student, course, enrolled date)
- **SCORM Attempt**: Existing entity from Scorm module (status, score, session time)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Course catalog filter updates appear on screen within 1 second of user input (search or category change)
- **SC-002**: Enrollment action completes with visible feedback within 2 seconds without a full page reload
- **SC-003**: Users can browse and filter at least 10 courses without triggering more than 1 full page reload
- **SC-004**: The web portal remains fully functional (all links work via full-page navigation) when JavaScript or HTMX is disabled
- **SC-005**: No existing user-visible functionality is broken — all current features remain accessible and operational
- **SC-006**: Page layout (navbar, footer, container) remains visually stable during all HTMX partial swaps with no layout shift or flash

## Assumptions

- The interactivity layer (HTMX) is loaded from a public CDN — no build step or package manager dependency is added
- Existing backend data endpoints remain unchanged and serve as the data source for all interactive updates
- The existing server-side rendering approach is preserved; partial page swaps target rendered HTML fragments, not raw JSON
- The existing authentication model is unchanged; interactive requests carry session credentials automatically
- CSS styling improvements are out of scope unless they directly support the partial-swap behavior
- Browser history (back/forward) for inline-navigated content is not required for the initial implementation; users can rely on the navigation bar for context
- Internal data-fetching patterns in page code-behind files may evolve during planning, but the external behavior and API surface remain the same
