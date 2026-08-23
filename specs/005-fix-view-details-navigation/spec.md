# Feature Specification: Fix Course View Details Navigation

**Feature Branch**: `bug/005-fix-view-details-navigation`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `bug/005-fix-view-details-navigation`.

**Created**: 2025-07-30

**Status**: Complete (merged 2026-07-30)

**Input**: User description: "missing gap identified in this thread (View Details)" — the "View Details" button on course catalog cards does not reliably navigate to the course detail page. The implementation mixes HTMX inline swaps and full-page navigation attributes on the same link, causing conflicts. Additionally, HTMX's `hx-push-url` pushes a handler-specific URL that breaks on browser refresh (returns a partial view without layout).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Click "View Details" Navigates to Course Detail Page (Priority: P1)

A student browsing the course catalog clicks the "View Details" button on any course card and is taken to the full course detail page. The page loads with the course title, description, category, duration, enrollment status, and an enroll/launch action. This works whether the student reaches the catalog via full-page navigation or HTMX inline swap.

**Why this priority**: Without a working "View Details" link, students cannot access course information beyond the catalog listing. The catalog is effectively read-only at the listing level, and enrollment (the core action) requires the detail page. This blocks the entire user flow from browse → detail → enroll → launch.

**Independent Test**: Navigate to the course catalog, click "View Details" on any course card — verify the course detail page renders with the layout (navbar, footer), course information, and appropriate action buttons.

**Acceptance Scenarios**:

1. **Given** a student is on the course catalog page, **When** they click "View Details" on a course card, **Then** the course detail page loads showing the course title, full description, category, duration, and enrollment status
2. **Given** a student clicks "View Details" from the catalog, **When** they arrive at the course detail page, **Then** the page includes the navigation bar, footer, and full layout (not just a content fragment)
3. **Given** a student is viewing a course detail page, **When** they click the back/browser back button, **Then** they return to the course catalog page
4. **Given** a student views a course detail page, **When** they refresh the browser, **Then** the full detail page re-renders correctly with layout, course data, and action buttons

---

### User Story 2 - Click Course Title Also Navigates to Detail Page (Priority: P1)

A student browsing the course catalog clicks the course title (the link inside the course card heading) and is taken to the same course detail page. This provides a second navigation path to the detail view, matching common web conventions where headings are clickable links.

**Why this priority**: Course title as a clickable link is a standard web pattern. Without it, students must find and click the separate "View Details" button, adding friction. Both paths (title and button) should deliver the same result.

**Independent Test**: Navigate to the course catalog, click the course title link — verify it navigates to the same detail page as clicking "View Details."

**Acceptance Scenarios**:

1. **Given** a student is on the course catalog page, **When** they click a course title link, **Then** they navigate to the course detail page with full layout and course information
2. **Given** a student clicks a course title from the catalog, **When** they arrive at the detail page, **Then** the page is identical in content and functionality to arriving via the "View Details" button

---

### User Story 3 - Direct URL Access to Course Detail Works (Priority: P2)

A student can bookmark or directly navigate to a course detail URL (e.g., from a shared link or browser history) and see the full course detail page. The page renders correctly with layout, course data, and action buttons regardless of how the user reaches it.

**Why this priority**: Users expect URLs to be bookmarkable and shareable. If the detail page only works as an HTMX inline swap but breaks on direct URL access, the experience is fragile and confusing. Direct URL access is the fallback for all navigation and must always work.

**Independent Test**: Copy the URL from a course detail page, open it in a new tab or after a browser restart — verify the full page renders correctly.

**Acceptance Scenarios**:

1. **Given** a student has navigated to a course detail page, **When** they copy the URL and open it in a new tab, **Then** the full detail page renders with layout, course data, and action buttons
2. **Given** a student navigates directly to a course detail URL (e.g., from a bookmark), **When** the page loads, **Then** it displays the full page layout and course information
3. **Given** a student navigates to a course detail URL for a non-existent course, **When** the page loads, **Then** they see a "Course Not Found" message with a link back to the catalog

---

### User Story 4 - HTMX Inline Course Detail Swap Works from Catalog (Priority: P3)

> **SUPERSEDED** (spec 006): HTMX inline swap from the course card was intentionally abandoned in favor of full-page navigation via `asp-page` tag helpers. Rationale: simpler, more reliable, works without JavaScript, eliminates HTMX/full-page conflict. The `OnGetDetailAsync` handler that would have served these inline swaps has been removed.

When HTMX is available and the student is on the catalog page, clicking a course card can optionally load the course detail inline (within the page content area) without a full page reload. The navigation bar and page structure remain stable. If HTMX is unavailable or the swap fails, the interaction degrades gracefully to full-page navigation.

**Why this priority**: ~~This is a UX enhancement (faster, SPA-like feel) that builds on top of reliable full-page navigation.~~ **Abandoned** — full-page navigation IS the approach.

**Independent Test**: On the catalog page with HTMX loaded, click a course card — verify the detail loads inline within the content area. Disable JavaScript — verify the same click navigates to the full detail page.

**Acceptance Scenarios**:

1. **Given** HTMX is loaded and the student is on the catalog page, **When** they click a course card, **Then** the course detail loads inline within the content area without a full page reload
2. **Given** HTMX is unavailable (e.g., JavaScript disabled), **When** they click a course card, **Then** the browser navigates to the full course detail page via standard link navigation
3. **Given** an HTMX inline swap fails (e.g., server error), **When** the error occurs, **Then** the student sees an error message and can retry or navigate via full-page link

---

### Edge Cases

- **Catalog page reached via HTMX swap**: If the catalog listing itself was loaded via HTMX (not a full page load), clicking "View Details" still works — full-page navigation via `asp-page` tag helpers is unaffected by how the catalog was loaded
- **~~HTMX inline swap from course cards~~**: ~~SUPERSEDED~~ — HTMX inline swap was abandoned for course card navigation (see US4). The `OnGetDetailAsync` handler has been removed. Note: spec 004 (`004-htmx-razor-conversion`) may have cross-spec inconsistency regarding this handler, but that is out of scope for this cleanup.
- **Browser back/forward with mixed navigation**: Student navigates catalog → detail (full page) → back to catalog → detail again (HTMX swap) — browser history should behave predictably
- **URL with handler parameter from old HTMX push-url**: If a student has an old bookmark containing `?handler=Detail`, it should either redirect to the clean URL or render correctly
- **Concurrent clicks**: Student rapidly clicks two different course cards — only one detail view should load, no duplicate requests
- **Detail page for a course with no SCORM package**: The detail page should render correctly showing only the enroll button (no launch button) — this path already exists but must be confirmed working after the fix

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST navigate to the full course detail page when the student clicks "View Details" on a course catalog card
- **FR-002**: System MUST navigate to the full course detail page when the student clicks a course title link in the catalog
- **FR-003**: The course detail page MUST render with the full page layout (navbar, footer, container) when accessed via direct URL or full-page navigation
- **FR-004**: The course detail page URL MUST be bookmarkable and shareable — accessing it directly must produce the same result as navigating from the catalog
- **FR-005**: System MUST NOT use HTMX `hx-push-url` with a handler-specific URL path that breaks on browser refresh
- **FR-006**: System MUST use full-page navigation as the primary approach for course card links — `asp-page` tag helpers navigate directly to the detail page. HTMX is not used on course card navigation links. HTMX remains only for catalog filtering (search, category dropdown).
- **FR-007**: The course detail page URL MUST use a clean path (e.g., `/Courses/Detail?id={guid}`) without HTMX handler parameters
- **FR-008**: System MUST render the enrollment state (enroll button, enrolled badge, launch button) correctly on the detail page regardless of how the page was reached
- **FR-009**: System MUST display a "Course Not Found" state when a student accesses a detail URL for a non-existent or deleted course
- **FR-010**: Browser back/forward navigation MUST work correctly between catalog and detail pages

### Key Entities

No new entities are introduced by this fix. The change affects only the navigation and rendering behavior of existing views:

- **Course** (existing): Course data fetched and displayed on the detail page
- **Enrollment** (existing): Enrollment status determines which action buttons appear on the detail page
- **ScormPackage** (existing): Presence of a SCORM package determines whether a "Launch" button appears

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A student can navigate from the course catalog to the detail page by clicking either the course title or "View Details" button — both paths work consistently
- **SC-002**: The course detail page loads with full layout (navbar, content, footer) within 2 seconds when accessed via direct URL
- **SC-003**: Bookmarking a course detail URL and reopening it in a new tab renders the full page correctly 100% of the time
- **SC-004**: Browser refresh on the course detail page re-renders the full page with layout, course data, and action buttons
- **SC-005**: With JavaScript disabled, clicking "View Details" navigates to the full course detail page via standard browser navigation
- **SC-006**: No URL in the application contains `?handler=Detail` or similar HTMX handler parameters when accessed through normal user navigation

## Assumptions

- **Existing Detail page is structurally sound**: The `Courses/Detail.cshtml` Razor Page and its code-behind (`Detail.cshtml.cs`) already contain the correct data-fetching and rendering logic. The fix targets the *navigation to* this page, not the page content itself
- **HTMX is optional**: The application must work with and without HTMX. HTMX provides an enhanced inline-swap experience but full-page navigation is the baseline requirement
- **Catalog page structure is stable**: The catalog page (`Courses/Index.cshtml`) and its course card partial (`_CourseCard.cshtml`) are the sources of the "View Details" links. These are the files that need fixing
- **Existing authentication is reused**: The detail page may require authentication (consistent with spec 001, FR-012). The navigation fix does not change auth requirements
- **Spec 004 HTMX conversion follows this fix**: The HTMX inline-swap enhancement (spec 004, User Story 3) assumes full-page navigation works first. This fix establishes that foundation
- **No database or API changes needed**: This is a frontend navigation and rendering fix — no changes to data models, API endpoints, or module contracts are required
