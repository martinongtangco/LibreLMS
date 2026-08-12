# Feature Specification: Admin Courses Management Overhaul

**Feature Branch**: `bug/025-admin-courses-management`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects.

**Created**: 2025-08-11

**Status**: Draft

**Input**: User description: "in the Admin/Courses page, i discovered the following issues: 1. there are no Create Course button, 2. i cannot modify courses details, 3. delete course doesnt work, 4. the page doesnt have filter, sorting, and pagination, 5. UI seems to be broken. theres not much contrast on the table and background."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View and Manage Courses with Filtering, Sorting, and Pagination (Priority: P1)

As an admin (SuperUser or OrgAdmin), I want to browse all courses on the Admin/Courses page with the ability to filter by category, search by title, sort columns, and paginate through results, so that I can efficiently find and manage courses even when there are many of them.

**Why this priority**: Without filtering, sorting, and pagination, the page becomes unusable as the course catalog grows. This is the foundational usability issue that blocks all other management tasks.

**Independent Test**: Can be fully tested by navigating to Admin/Courses, verifying that search, category filter, column sorting, and pagination controls are present and functional. An admin can find a specific course using search/filter and navigate through paginated results.

**Acceptance Scenarios**:

1. **Given** an admin is on the Admin/Courses page, **When** they enter a search term, **Then** only courses matching the search term are displayed
2. **Given** an admin is on the Admin/Courses page, **When** they select a category filter, **Then** only courses in that category are displayed
3. **Given** courses are displayed in a table, **When** the admin clicks a column header, **Then** the results are sorted by that column
4. **Given** there are more courses than fit on one page, **When** the admin navigates to the next page, **Then** the next set of courses is displayed with page controls
5. **Given** the table is rendered, **When** the admin views it, **Then** table rows and headers have sufficient contrast against the page background to be easily readable

---

### User Story 2 - Create New Courses (Priority: P1)

As an admin, I want to create new courses from the Admin/Courses page by clicking a "Create Course" button, so that I can add new content to the catalog without navigating to a separate URL.

**Why this priority**: The Create Course page exists but is not accessible from the main listing. Admins cannot discover or use this capability, making course creation effectively unavailable.

**Independent Test**: Can be fully tested by navigating to Admin/Courses, clicking "Create Course", filling in course details (title, description, category, duration), submitting the form, and confirming the new course appears in the listing.

**Acceptance Scenarios**:

1. **Given** an admin is on the Admin/Courses page, **When** they click the "Create Course" button, **Then** they are navigated to the course creation form
2. **Given** an admin is on the course creation form, **When** they fill in all required fields and submit, **Then** the course is created and they see a success message
3. **Given** a course was just created, **When** the admin views the course listing, **Then** the new course appears in the table

---

### User Story 3 - Edit Existing Course Details (Priority: P2)

As an admin, I want to edit the details of an existing course (title, description, category, duration) from the Admin/Courses page, so that I can correct mistakes and keep course information up to date.

**Why this priority**: Admins need to maintain course accuracy. Currently there is no edit capability at all, making any correction require database-level intervention.

**Independent Test**: Can be fully tested by navigating to Admin/Courses, clicking "Edit" on a course, modifying a field (e.g., title), saving, and confirming the change is reflected in the listing.

**Acceptance Scenarios**:

1. **Given** an admin is on the Admin/Courses page, **When** they click "Edit" on a course row, **Then** an edit form is displayed pre-populated with the course's current details
2. **Given** an admin has modified course details, **When** they save the changes, **Then** the course is updated and they see a success confirmation
3. **Given** a course was edited, **When** the admin views the course listing, **Then** the updated details are displayed

---

### User Story 4 - Delete Courses Reliably (Priority: P2)

As an admin, I want to delete courses from the Admin/Courses page with confirmation and feedback, so that I can remove courses that are no longer needed.

**Why this priority**: The delete button exists but does not work. This is a broken feature that frustrates users who expect it to function.

**Independent Test**: Can be fully tested by navigating to Admin/Courses, clicking "Delete" on a course, confirming the deletion, and verifying the course no longer appears in the listing with a success message displayed.

**Acceptance Scenarios**:

1. **Given** an admin clicks "Delete" on a course, **When** they confirm the action, **Then** the course is removed from the system
2. **Given** a course was deleted, **When** the admin views the course listing, **Then** the deleted course no longer appears and a success message is shown
3. **Given** a course does not exist, **When** an admin attempts to delete it, **Then** an appropriate error message is displayed

---

### User Story 5 - Improved Table Readability and Visual Design (Priority: P3)

As an admin viewing the course listing, I want the table to have clear visual distinction between rows, headers, and the page background, so that I can read the data without straining.

**Why this priority**: The current table header uses `--color-bg` (#faf8f4) which has very low contrast against the page background (`--page-bg`, #f5ead8), making it hard to distinguish table structure. This is a quality-of-life issue but important for usability.

**Independent Test**: Can be fully tested by visual inspection of the Admin/Courses page, verifying that table headers, rows, and the background are visually distinct and the table follows the site's organic design system.

**Acceptance Scenarios**:

1. **Given** an admin views the Admin/Courses page, **When** they look at the table, **Then** the table header has a clearly distinguishable background from the page background
2. **Given** courses are displayed in the table, **When** the admin views alternate rows, **Then** rows have visual separation (alternating colors or clear borders) for easy scanning
3. **Given** the page is displayed, **When** the admin views it on various screen sizes, **Then** the table layout is responsive and readable on mobile, tablet, and desktop

---

### Edge Cases

- What happens when the search returns no matching courses? (Should show an empty state message with guidance)
- What happens when an admin tries to delete a course that has active enrollments? (Should either prevent deletion or warn the admin)
- What happens if multiple admins edit the same course simultaneously? (Last save wins, or show a conflict message)
- What happens when pagination is at the last page and a course is deleted? (Should navigate to the previous page)
- What happens when a course has no category assigned? (Should display a default like "Uncategorized" rather than blank)
- What happens when the admin uploads an invalid SCORM ZIP (missing imsmanifest.xml)? (Should show a clear error and keep the form intact)
- What happens when no unassociated SCORM packages are available? (Should show "No available SCORM packages" in the dropdown with a link to upload)
- What happens when a course with SCORM is deleted? (Both the Course and its ScormPackage should be deleted, including content directory cleanup)
- What happens when SCORM content extraction fails mid-upload? (Should roll back partial extraction and show an error)
- What happens when a course with SCORM is deleted? (Should show a confirmation warning that the SCORM package and its content will also be deleted)
- What happens when the available SCORM pool grows stale? (Admins should be able to delete unassociated SCORM packages from the Upload page)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Admin/Courses page MUST display a "Create Course" button prominently near the page header that navigates to the course creation form
- **FR-002**: The course creation form MUST accept title, short description, full description, category, and duration fields
- **FR-003**: After creating a course, the admin MUST be shown a success message and returned to the course listing
- **FR-004**: Each course row MUST have an "Edit" action that opens an edit form pre-populated with the course's current details
- **FR-005**: The edit form MUST allow modifying all course fields (title, short description, full description, category, duration)
- **FR-006**: After saving edits, the admin MUST be shown a success message and the updated data must appear in the listing
- **FR-007**: Each course row MUST have a "Delete" action that prompts for confirmation before deleting
- **FR-008**: After deleting a course, the course MUST be removed from the listing and a success message MUST be displayed
- **FR-009**: The Admin/Courses page MUST provide a text search input to filter courses by title
- **FR-010**: The Admin/Courses page MUST provide a category filter (dropdown) to filter courses by category
- **FR-011**: Table column headers MUST be clickable to toggle sort order (ascending/descending) for that column
- **FR-012**: The page MUST paginate results with configurable page size and navigation controls (previous/next/page numbers)
- **FR-013**: Search, filter, and sort state MUST be preserved when navigating between pages
- **FR-014**: The table header background MUST be clearly distinguishable from the page background at a glance
- **FR-015**: Table rows MUST have clear visual separation through borders, alternating row colors, or sufficient spacing
- **FR-016**: The page MUST display an empty state message when no courses match the current search/filter criteria, with guidance on how to adjust filters
- **FR-017**: The page MUST be accessible to users with SuperUser or OrgAdmin roles only

### SCORM Integration Requirements

- **FR-018**: The course creation form MUST provide three SCORM options: (1) No SCORM content, (2) Upload new SCORM package, (3) Associate existing unassociated SCORM package
- **FR-019**: When "Upload new SCORM" is selected, the form MUST accept a ZIP file containing a valid SCORM 1.2 package (with imsmanifest.xml)
- **FR-020**: When "Associate existing SCORM" is selected, the form MUST display a dropdown of SCORM packages not yet associated with any course
- **FR-021**: Creating a course with SCORM upload MUST create both the Course and ScormPackage entities in a single transaction
- **FR-022**: Creating a course with SCORM association MUST link the Course to the selected ScormPackage and set its CourseId
- **FR-023**: A ScormPackage MUST have a nullable CourseId — it can exist without a course association (in the "available pool")
- **FR-024**: The unique index on ScormPackage.CourseId MUST allow null values (filtered index) — only non-null CourseIds are unique
- **FR-025**: SCORM content (launch/play) MUST be blocked when the ScormPackage has no associated CourseId (is null)
- **FR-026**: The separate Admin/Upload page MUST be updated to upload SCORM packages without requiring a course — adding to the available pool
- **FR-027**: The course edit form MUST allow adding SCORM to a course that has none, or replacing existing SCORM with a new upload
- **FR-028**: When replacing SCORM on an existing course, the old ScormPackage MUST be deleted (including its content directory) before the new one is created
- **FR-029**: When deleting a course that has SCORM content, the UI MUST show a confirmation warning that the SCORM package and its extracted files will also be deleted
- **FR-030**: The Admin/Upload page MUST be repurposed to upload SCORM packages to the available pool without requiring course association; association is done through the Courses pages
- **FR-031**: The Admin/Upload page MUST list available (unassociated) SCORM packages with a delete option for pool cleanup
- **FR-032**: SCORM ZIP uploads MUST be limited to 50MB
- **FR-033**: SCORM packages MUST use single-SCO launch (first SCO from manifest) — multi-SCO sequencing is out of scope per constitution

### Key Entities

- **Course**: Represents a learning course with title, short description, full description, category, duration, owning organization, and creation timestamp.
- **Visibility Override**: Represents an admin's decision to hide an inherited course within a specific organization scope.
- **Course Listing Entry**: The display representation of a course in the admin listing, containing title, category, organization name, source, and visibility status.

## Key Entities (continued)

- **ScormPackage**: Represents an uploaded SCORM 1.2 package with CourseId (nullable FK to Course), ManifestTitle (from imsmanifest.xml), LaunchPath (relative HTML path), ContentDirectory (server-relative path to extracted files), and CreatedAt. When CourseId is null, the package is in the "available pool" awaiting association. When CourseId is set, the package belongs to that course. Non-null CourseId values must be unique (one SCORM per course).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can create a new course in under 30 seconds from clicking "Create Course" to seeing the success confirmation
- **SC-002**: Admins can find a specific course by name using the search filter within 3 seconds
- **SC-003**: The delete action completes and shows a success confirmation within 2 seconds
- **SC-004**: 100% of course management actions (create, edit, delete, search, filter, sort, paginate) work without errors on the first attempt
- **SC-005**: Table header background has a visibly distinct contrast from the page background, confirmed by visual inspection on at least desktop and mobile viewports
- **SC-006**: Pagination loads the next page of results within 1 second when there are 100+ courses
- **SC-007**: The empty state message provides clear, actionable guidance when no courses match current filters
- **SC-008**: Admins can create a course with SCORM content in a single flow (under 60 seconds from form to confirmation)
- **SC-009**: SCORM packages without a course association are visible in the "Associate existing SCORM" dropdown but cannot be launched directly
- **SC-010**: Replacing SCORM on an existing course preserves all course metadata while updating only the SCORM content

### User Story 6 - Upload SCORM During Course Creation (Priority: P1)

As an admin, I want to upload a SCORM package or associate an existing SCORM package when creating a new course, so that I can complete the course setup in a single flow instead of navigating between separate Course and SCORM upload pages.

**Why this priority**: Currently, creating a course and uploading SCORM are disjoint flows — the admin creates a course, then navigates to a separate Upload page to attach SCORM. This is confusing and error-prone. Integrating SCORM into course creation streamlines the workflow.

**Independent Test**: Can be fully tested by navigating to Admin/Courses/Create, choosing to upload a SCORM ZIP or associate an existing SCORM package, completing the form, and confirming both the course and SCORM are created/linked correctly.

**Acceptance Scenarios**:

1. **Given** an admin is on the course creation form, **When** they select "No SCORM content" and submit, **Then** the course is created without a SCORM package
2. **Given** an admin is on the course creation form, **When** they select "Upload new SCORM" and choose a ZIP file, **Then** the course is created and the SCORM package is uploaded and associated in one operation
3. **Given** unassociated SCORM packages exist in the system, **When** the admin selects "Associate existing SCORM" and chooses from the dropdown, **Then** the course is created and linked to that SCORM package
4. **Given** a course was created with SCORM, **When** the admin views the course listing, **Then** the course shows as having SCORM content
5. **Given** a SCORM package exists but is not associated with any course, **When** the admin tries to launch it directly, **Then** they cannot — SCORM content is only accessible through a course

---

### User Story 7 - SCORM Management in Course Edit (Priority: P2)

As an admin, I want to manage SCORM association when editing an existing course, so that I can add SCORM content to a course that was created without it, or replace its SCORM package.

**Why this priority**: Courses created without SCORM should be able to have SCORM added later. Admins also need to replace SCORM content if the content is updated.

**Independent Test**: Navigate to Admin/Courses/Edit for a course without SCORM, upload a SCORM ZIP, save, and confirm the course now has SCORM content.

**Acceptance Scenarios**:

1. **Given** a course has no SCORM package, **When** the admin edits the course and uploads a SCORM ZIP, **Then** the SCORM package is created and associated with the course
2. **Given** a course has a SCORM package, **When** the admin edits the course and uploads a new SCORM ZIP, **Then** the old SCORM package is replaced with the new one
3. **Given** a course has a SCORM package, **When** the admin views the edit form, **Then** the current SCORM package information is displayed (title, upload date)

---

## Assumptions

- A course creation form already exists in the application and is functional; it only needs to be linked from the listing page
- The application already has server-side capabilities for creating courses; the admin page will use whatever mechanism is available
- Editing course details will require a new server-side capability to update existing courses
- The delete capability exists on the server side but may have a bug — root cause will be investigated during implementation
- The application's existing design system (organic design with established CSS classes and color tokens) will be used for consistency
- The existing page background and color palette will be preserved; contrast improvements will work within the current design system
- Courses can be deleted regardless of enrollment status; a warning may be shown for courses with active enrollments
- Pagination default page size will be 10-20 courses per page
- The page follows standard web patterns for form submissions (redirect after post to prevent duplicate submissions)
- **SCORM-Course relationship**: A course can exist without SCORM, but a SCORM package cannot be consumed (launched/studied) without being associated with a course
- **SCORM independence**: If two courses use the same SCORM content, each upload creates a new, independent ScormPackage entity with its own content directory copy — packages are never shared between courses
- **SCORM association pool**: SCORM packages can be uploaded independently of a course (CourseId is null), forming a pool of available packages that can be associated during course creation
- **One SCORM per course**: A course can have at most one SCORM package at a time (uploading a new one replaces the existing)
