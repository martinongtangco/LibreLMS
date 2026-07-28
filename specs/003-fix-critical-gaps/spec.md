# Feature Specification: Fix Critical Gaps

**Feature Branch**: `bug/003-fix-critical-gaps`

**Created**: 2025-07-29

**Status**: Draft

**Input**: User description: "review these gaps identified in the previous prompt" — comprehensive codebase review identified critical bugs blocking SCORM launch (missing ContentUrl), broken navigation (missing tag helpers), orphaned seed data, missing auth pages, no course creation, and several polish issues.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SCORM Course Launches and Displays Content (Priority: P1)

A student enrolled in a SCORM course clicks "Launch" and the course content loads in their browser. The SCORM runtime session initializes and the course's first screen renders from the package content.

**Why this priority**: Without this fix, the entire SCORM module is non-functional. Students cannot access any course content. The launch endpoint exists and creates sessions, but the `ContentUrl` is never computed or returned, so the iframe loads nothing.

**Independent Test**: Seed a SCORM package linked to a real course, enroll a student, click Launch — verify the course's `index.html` content renders in the browser and the SCORM session is active in Valkey.

**Acceptance Scenarios**:

1. **Given** a SCORM package is linked to a course in the catalog, **When** an enrolled student clicks "Launch" on the course detail page, **Then** the SCORM player loads and displays the course's launch HTML file
2. **Given** a SCORM package is linked to a course, **When** the launch endpoint is called, **Then** the response includes `sessionId`, `contentUrl`, `entry` mode, and `attemptNumber`
3. **Given** a course has no SCORM package attached, **When** a student views the course detail page, **Then** no "Launch SCORM Course" button is shown

---

### User Story 2 - Navigation Links Work Across All Pages (Priority: P1)

A student can navigate between Browse Courses, My Courses, and Admin Upload pages using the top navigation bar. Links resolve to correct URLs and pages render properly.

**Why this priority**: Without working navigation, the application is effectively a single-page experience. Students cannot browse, view their courses, or admins cannot upload packages. The `_Layout.cshtml` uses ASP.NET Core tag helpers (`asp-page`) but `_ViewImports.cshtml` is missing the required `@addTagHelper` directive.

**Independent Test**: Start on any page, click each navigation link — verify each destination page renders with correct content.

**Acceptance Scenarios**:

1. **Given** the student is on the course catalog page, **When** they click "My Courses" in the navigation, **Then** they are taken to the enrolled courses listing page
2. **Given** the student is on the My Courses page, **When** they click "Browse Courses", **Then** they are taken to the course catalog page
3. **Given** any page loads, **When** the navigation bar is rendered, **Then** all links resolve to correct relative URLs (not literal `asp-page` attributes)

---

### User Story 3 - Admin Creates a New Course (Priority: P2)

An admin creates a new course entry in the catalog with title, description, category, and duration. The course appears immediately in the catalog browsing experience.

**Why this priority**: Currently courses can only be added via seed data. Without the ability to create courses, the system is frozen at its initial state. Combined with SCORM upload, this enables the full admin workflow of adding new learning content.

**Independent Test**: Use an admin interface to create a course — verify it appears in the catalog listing and has a detail page.

**Acceptance Scenarios**:

1. **Given** an admin navigates to the course creation page, **When** they fill in course details and submit, **Then** the course is persisted and appears in the catalog
2. **Given** a course was just created, **When** any student browses the catalog, **Then** the new course is visible and selectable
3. **Given** a course creation form is submitted with missing required fields, **When** the form is validated, **Then** the user sees clear error messages for each missing field

---

### User Story 4 - Admin Uploads SCORM Package for a Course (Priority: P2)

An admin uploads a SCORM 1.2 ZIP package and associates it with an existing course. The course becomes launchable by enrolled students. The upload page shows a clear error if the package is invalid.

**Why this priority**: The upload page exists but requires a manually-typed course GUID. Without course creation (US3) and with the seed package orphaned, there is no practical way for an admin to set up a launchable SCORM course through the application.

**Why not P1**: The upload endpoint and page already exist and work when given a valid course ID. The blockers are: (1) no way to create courses, (2) seed package not linked to any course, (3) hardcoded localhost URL in the upload handler.

**Independent Test**: Create a course, upload a SCORM ZIP — verify the course shows a "Launch" button and the content loads.

**Acceptance Scenarios**:

1. **Given** a course exists in the catalog, **When** an admin uploads a valid SCORM ZIP and selects the course, **Then** the package is extracted, the course becomes launchable, and a success message is shown
2. **Given** an admin uploads a ZIP that is not a valid SCORM package, **When** the upload is processed, **Then** an error explains the package is invalid (e.g., missing `imsmanifest.xml`)
3. **Given** an admin uploads a package for a course that already has one, **When** the upload is processed, **Then** an error explains a package already exists for that course

---

### User Story 5 - Student Authentication Works (Priority: P2)

A student logs into the system and subsequent requests are associated with their identity. If not logged in, protected pages redirect to a login form.

**Why this priority**: Cookie authentication is configured in `Program.cs` but the `/Account/Login` page does not exist. Any authenticated endpoint redirects to a 404. The system currently falls back to a hardcoded student ID, which works for single-user testing but breaks for any real scenario.

**Independent Test**: Access a protected page without login — see a login form. Log in — verify subsequent requests identify the student correctly.

**Acceptance Scenarios**:

1. **Given** a student is not authenticated, **When** they attempt to enroll in a course, **Then** they are redirected to a login page
2. **Given** a student logs in successfully, **When** they access "My Courses", **Then** the page shows that student's enrollments (not another student's)
3. **Given** a student's session expires, **When** they attempt an action, **Then** they are prompted to log in again

---

### Edge Cases

- **Orphaned SCORM package**: A SCORM package exists in the database with no matching course — it should not crash the catalog or launch flow
- **Concurrent SCORM upload**: Two admins upload packages for different courses simultaneously — no data corruption
- **Upload with large file**: A SCORM ZIP larger than typical (e.g., 100MB+) — upload should succeed or fail with a clear size limit message
- **Course creation with duplicate title**: Multiple courses can share a title — no uniqueness constraint required
- **Navigate to non-existent page**: Returns the error page defined in `Pages/Error.cshtml`
- **Seed data conflicts**: If the database already has data, seeders must not duplicate entries (currently handled by `Any()` checks)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST compute and return the `contentUrl` for a SCORM course during launch, derived from the SCORM package's `ContentDirectory` and `LaunchPath` fields
- **FR-002**: System MUST serve SCORM content files from `wwwroot/scorm-content/{packageId}/` as static files accessible at the computed `contentUrl`
- **FR-003**: System MUST render all navigation links as correct URLs by including the `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` directive
- **FR-004**: System MUST provide a login page at `/Account/Login` that authenticates users and establishes a cookie-based session
- **FR-005**: System MUST provide a way to create new courses through the application (API endpoint + admin UI)
- **FR-006**: System MUST allow associating a SCORM package with a course during upload, using a course selector (dropdown) instead of requiring a manually typed GUID
- **FR-007**: System MUST link the seeded sample SCORM package to a real seeded course so the demo flow works end-to-end out of the box
- **FR-008**: System MUST not hardcode `http://localhost:5000` in any request — use relative URLs or the current request's base address
- **FR-009**: System MUST show a "Launch SCORM Course" button only on courses that have an associated SCORM package
- **FR-010**: System MUST validate uploaded SCORM packages and reject invalid packages with a clear error message
- **FR-011**: System MUST prevent duplicate SCORM package uploads for the same course
- **FR-012**: System MUST persist course data created through the admin interface so it survives application restarts

### Key Entities

- **Course** (existing): Represents a learnable unit — title, descriptions, category, duration. Extended to support creation through the application.
- **ScormPackage** (existing): Represents an uploaded SCORM package linked to a Course. The seed instance must be linked to a real course ID.
- **Student** (existing): Represents a learner. Extended to support authentication credentials for the login flow.
- **CourseAttempt** (existing): Represents a student's SCORM session attempt — no changes needed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An enrolled student can launch a SCORM course and see the content within 3 seconds (existing criterion, currently broken)
- **SC-002**: All 3 navigation links (Browse Courses, My Courses, Upload SCORM) resolve to correct URLs and load their respective pages
- **SC-003**: An admin can create a new course and upload a SCORM package in a single workflow without manually entering GUIDs
- **SC-004**: The seeded demo data includes at least one launchable SCORM course accessible on first startup
- **SC-005**: A student can log in and have their identity persisted across requests and page navigations
- **SC-006**: No hardcoded host URLs exist in the codebase — all requests use relative URLs or request-scoped base addresses

## Assumptions

- **SCORM 1.2 only**: Consistent with existing spec — no SCORM 2004 or multi-SCO support
- **Single admin role**: The existing `[Authorize(Roles = "Admin")]` pattern is reused; no new role management is needed
- **Simple authentication**: Cookie-based login with seeded credentials — no OAuth, no SSO, no password reset
- **Existing Razor Pages pattern**: All new pages follow the existing Razor Pages convention (`.cshtml` + `.cshtml.cs`)
- **Seed data is sufficient for demo**: The seeded courses, students, and SCORM package provide a working demo without requiring admin setup
- **Static file serving**: SCORM content continues to be served as static files from `wwwroot` — no proxy or streaming changes
- **Module boundaries preserved**: New endpoints are added in `Program.cs` (Host) as per the existing pattern; no cross-module references are introduced
- **MSSQL is the system of record**: Course creation persists to the same database used by the existing seeders
