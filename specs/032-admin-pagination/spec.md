# Feature Specification: Admin List Pagination with Page Size Toggle

**Feature Branch**: `story/032-admin-pagination`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features.

**Created**: 2026-08-21

**Status**: Draft

**Input**: User description: "i noticed theres no pagination implemented in the Admin > Courses, Enrollment, Learners. Implement the same pagination implemented in the Browse Courses. Make sure we use stored procedures and efficient pagination thats scalable and reliable. in Admin pages, i want to be able to toggle page sizes: 10, 30, 50, 100."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Paginate the Admin Enrollments List (Priority: P1)

As an admin (SuperUser or OrgAdmin), I want the Admin > Enrollments page to show enrollments in
pages with Previous/Next navigation and a page indicator, exactly like the Browse Courses page,
so I can review and manage enrollments without the page loading every enrollment record at once.

**Why this priority**: The Enrollments page currently loads every enrollment in the system into
the page. This is the most severe of the three pages because enrollment volume grows fastest
(every learner enrolling in every course adds a row). It is the defining defect the user reported.

**Independent Test**: Seed more enrollments than one page holds, open Admin > Enrollments, and
verify only one page of rows is shown with working Previous/Next controls, a "Page X of Y (Z
total)" indicator, and that existing student-name and course-title filters still narrow the
paginated results.

**Acceptance Scenarios**:

1. **Given** there are more enrollments than the selected page size, **When** an admin opens Admin > Enrollments, **Then** only the first page of rows is displayed with pagination controls
2. **Given** an admin is on page 1 of enrollments, **When** they click Next, **Then** page 2 is displayed and the indicator shows "Page 2 of Y (Z total)"
3. **Given** an admin is on the last page, **When** they view the page, **Then** the Next control is hidden; on page 1 the Previous control is hidden
4. **Given** an admin has filtered by student name and/or course title, **When** they paginate, **Then** the filters stay active and only filtered rows are paged
5. **Given** an admin applies or changes a filter while on page N, **When** the results refresh, **Then** they are returned to page 1 of the filtered results
6. **Given** an admin cancels an enrollment, **When** the action completes, **Then** they return to the same filtered view with pagination controls consistent with the updated total

---

### User Story 2 - Paginate the Admin Learners List (Priority: P1)

As an admin, I want the Admin > Learners page to show learners in pages with the same
pagination controls as Browse Courses, so I can manage learners in an organization of any size
without the page loading every user record at once.

**Why this priority**: The Learners page currently loads every user record into the page and
applies its search box in-process over the full list. Learner counts define the scale of the
platform, so this page is the second-most exposed to unbounded growth.

**Independent Test**: Seed more learners than one page holds, open Admin > Learners, verify one
page of rows with working pagination, and verify the name/email search box narrows the paginated
results (page 1 of the matches).

**Acceptance Scenarios**:

1. **Given** there are more learners than the selected page size, **When** an admin opens Admin > Learners, **Then** only the first page of rows is displayed with pagination controls
2. **Given** an admin searches by name or email, **When** results refresh, **Then** only matching learners are shown, starting at page 1, and the total count reflects the matches
3. **Given** an admin has a role filter selected, **When** they paginate, **Then** only learners with that role are paged
4. **Given** an admin is on the last page, **When** they view the page, **Then** the Next control is hidden
5. **Given** an admin changes the search, role, or organization filter while on page N, **When** the results refresh, **Then** they are returned to page 1

---

### User Story 3 - Toggle Page Size on All Three Admin Pages (Priority: P1)

As an admin, I want a page size selector on Admin > Courses, Enrollments, and Learners offering
exactly 10, 30, 50, and 100 rows per page, so I can choose a dense view for quick scanning or a
sparse view for careful review.

**Why this priority**: The user explicitly requested this control. Without it, admins are stuck
with whatever fixed size is chosen; with it, the same page serves both quick scanning and
detailed review. It is a single control shared by all three pages, so it must behave identically
on each.

**Independent Test**: On each of the three admin pages, change the page size selector to each of
the four values and verify the table re-renders with that many rows (fewer on the last page),
that navigation always restarts at page 1 after a size change, and that the indicator total is
unchanged by the size change.

**Acceptance Scenarios**:

1. **Given** an admin is on any of the three admin pages, **When** they view the page, **Then** a page size selector is visible offering exactly the options 10, 30, 50, and 100
2. **Given** an admin changes the page size from 10 to 50, **When** the page refreshes, **Then** up to 50 rows are shown, starting at page 1
3. **Given** an admin is on page 3 with page size 10, **When** they select page size 100, **Then** page 1 of the 100-row pages is shown
4. **Given** an admin paginates forward with a selected page size, **When** they click Next/Previous, **Then** the selected page size is retained
5. **Given** a page size that is not one of 10/30/50/100 is requested (e.g., a hand-edited URL or a legacy value), **When** the page loads, **Then** the default page size is used and the selector shows the default
6. **Given** the total number of matching rows is less than or equal to the selected page size, **When** the page loads, **Then** all rows are shown and there is nothing to navigate beyond page 1

---

### User Story 4 - Bring Admin Courses Pagination in Line with the Standard (Priority: P2)

As an admin managing courses, I want the Admin > Courses page to paginate with the same controls,
page size toggle, and server-side consistency as the other two admin pages, so all three admin
lists behave identically.

**Why this priority**: The Courses page already has a partial, inconsistent form of pagination
(fixed page size, and column sorting applied only within the fetched page rather than across the
whole filtered set — so sorting appears broken once a list spans multiple pages). It must be
brought to the same standard as Enrollments and Learners, but the base capability partially
exists, so this is P2.

**Independent Test**: Seed more courses than one page holds, open Admin > Courses, click a
sortable column header, verify the entire result set (not just the visible page) is in the
clicked column's order, and navigate pages in that order; then verify the page size toggle works
as in User Story 3.

**Acceptance Scenarios**:

1. **Given** an admin is on Admin > Courses with more courses than one page, **When** they click the Title or Category column header, **Then** the entire filtered result set is sorted by that column and pages are navigated in that sort order
2. **Given** an admin has a sort direction active, **When** they click the same column header again, **Then** the sort direction toggles (ascending/descending) and pages reflect the new order
3. **Given** an admin has search and/or category filters applied, **When** they paginate or re-sort, **Then** the filters remain active throughout
4. **Given** an admin changes the page size on Admin > Courses, **When** the page refreshes, **Then** behavior matches User Story 3 exactly
5. **Given** an admin deletes a course, **When** the action completes, **Then** they return to the filtered view with pagination controls consistent with the updated total

---

### Edge Cases

- **Fewer records than one page**: when total matching rows ≤ page size, pagination controls are absent (single page) and all rows are visible.
- **Zero results**: when search/filter matches nothing, an empty-state message is shown and no pagination controls appear.
- **Out-of-range page number**: a requested page beyond the last valid page (stale link, rows deleted meanwhile) is clamped to the last valid page; a page below 1 is treated as page 1.
- **Current page becomes empty after a delete/cancel**: if removing a row leaves the current page empty and page 1 still has rows, the admin is returned to the previous page; if page 1 becomes empty, the empty state is shown.
- **Invalid page size value**: any page size not in {10, 30, 50, 100} (including legacy sizes 12 and 15) is replaced by the default of 10.
- **Ties in sort order**: ordering must be deterministic (a stable tie-breaker), so no row appears on two pages or vanishes between pages while navigating.
- **Whitespace-only search**: a search term containing only spaces is treated as no search.
- **Special characters in search/filter input**: quotes, percent signs, and ampersands must be handled safely (parameterized queries; no errors, no injection).
- **Large totals**: the "Z total" indicator and page math remain correct for totals in the tens of thousands.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Admin > Enrollments page MUST paginate its results server-side, displaying only the requested page of rows plus a total count of matching enrollments.
- **FR-002**: The Admin > Learners page MUST paginate its results server-side, displaying only the requested page of rows plus a total count of matching learners.
- **FR-003**: The Admin > Courses page MUST paginate its results server-side, displaying only the requested page of rows plus a total count of matching courses.
- **FR-004**: All three admin pages MUST present the same pagination controls and indicator as the Browse Courses page: a Previous control, a Next control, and a "Page X of Y (Z total)" indicator; Previous is hidden on page 1 and Next is hidden on the last page.
- **FR-005**: Each admin page MUST offer a page size selector with exactly four options: 10, 30, 50, and 100 rows per page.
- **FR-006**: The default page size on all three admin pages MUST be 10.
- **FR-007**: Changing the page size MUST reset the view to page 1.
- **FR-008**: Changing any search or filter criterion MUST reset the view to page 1.
- **FR-009**: Filtering, sorting, and pagination for each admin list MUST be executed together in a single parameterized, server-side database query (a stored procedure, per the feature request) that returns only the requested page of rows plus the total count; the application MUST NOT load the full list into memory and page or sort it in-process.
- **FR-010**: Sorting MUST be applied across the entire filtered result set before the requested page is extracted (not just within the fetched page).
- **FR-011**: A requested page number outside the valid range MUST be clamped to the valid range (page 1 through the last page) rather than producing an error or an empty page.
- **FR-012**: A requested page size that is not one of 10, 30, 50, or 100 MUST fall back to the default page size of 10.
- **FR-013**: The displayed total count MUST always reflect the rows matching the active search/filter criteria, not the unfiltered table size.
- **FR-014**: Pagination MUST compose with the existing per-page filters: Enrollments (student name, course title), Learners (name/email search, role, organization), and Courses (title search, category) — and with existing Courses column sorting (title, category, duration, ascending/descending).
- **FR-015**: Result ordering MUST be deterministic (with a stable tie-breaker) so that navigating forward and backward never duplicates or skips a row.
- **FR-016**: The selected page size MUST be retained across subsequent page navigation on the same page until the user changes it.
- **FR-017**: The Browse Courses page and its existing pagination behavior MUST remain unchanged by this feature.
- **FR-018**: After a row-level action on an admin page (cancel enrollment, delete course), the admin MUST return to the same filtered/sorted/paged view with counts and controls consistent with the updated data.

### Non-Functional Requirements

- **NFR-001**: Loading any of the three admin pages, and each page navigation, MUST complete within 1 second for lists up to 10,000 matching rows under normal development-environment conditions.
- **NFR-002**: Only the current page of rows (plus the total count) MUST be transferred from the database to the application and rendered; application memory usage for a list view MUST stay bounded by the page size, not by the table size.
- **NFR-003**: Pagination MUST remain correct (no duplicate or missing rows) even while other users create or delete records concurrently.
- **NFR-004**: The pagination control placement, labels, and behavior MUST be visually and functionally consistent across all three admin pages and with the Browse Courses page.

### Key Entities

- **Course**: existing entity (title, category, duration, organization, SCORM status) — listed and paginated in Admin > Courses.
- **Enrollment**: existing entity linking a learner to a course with an enrollment timestamp — listed and paginated in Admin > Enrollments.
- **Learner (User)**: existing entity (name, email, role, organization) — listed and paginated in Admin > Learners.
- **Page Request**: transient query state — page number (integer), page size (one of 10/30/50/100), plus the page's active search/filter/sort criteria.
- **Page Result**: transient result state — the page's rows, the total count of matching rows, the effective page number, and the effective page size.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can open any of the three admin pages with 1,000+ matching records and see the first page rendered in under 1 second.
- **SC-002**: Each Previous/Next page navigation completes in under 1 second for lists up to 10,000 matching rows.
- **SC-003**: An admin can switch the page size to any of the four offered values in a single interaction and immediately see the table re-render with that page size, starting at page 1.
- **SC-004**: All three admin pages display the identical pagination control set (Previous/Next plus "Page X of Y (Z total)") and the identical page size selector (10/30/50/100).
- **SC-005**: 100% of the acceptance scenarios in User Stories 1–4 pass manual or automated testing.
- **SC-006**: With 10,000 matching rows, first-page load and page navigation complete within 2 seconds, and the page's row count never exceeds the selected page size.
- **SC-007**: No admin list page loads the full table into application memory — inspection of the data access for a page view confirms only the requested page of rows plus a count is fetched.
- **SC-008**: After navigating across pages (forward and back) with stable data, no row is displayed twice and no matching row is unreachable.

## Assumptions

- **Default page size**: 10 rows per page (smallest offered size; standard for dense admin tables). The user did not specify a default.
- **Page size persistence**: the selected page size is request state (carried in the page's URL) and is retained while navigating on that page; it is not persisted in the browser across sessions or carried over to the other admin pages.
- **Navigation mechanism**: the three admin pages keep their existing full-page form/GET navigation. "Same pagination as Browse Courses" refers to the pagination behavior and controls (server-side paging, Previous/Next with hidden boundary controls, page indicator), not to the Browse page's in-place partial updates. The Browse Courses page is not modified.
- **Authorization and data scoping unchanged**: all three pages remain restricted to SuperUser and OrgAdmin roles, and both roles continue to see the same data they see today (no org-scoping change is in scope for this feature).
- **Existing filters preserved**: the Learners organization filter dropdown is not applied to results today (pre-existing gap). Making it functional is out of scope here; it is recorded for a separate defect spec. All other existing filters and the Courses column sorting remain as-is.
- **Default ordering**: Enrollments remain newest-first (by enrollment time), as today. Learners get a deterministic name-ascending default order (currently unordered). Courses keep title-ascending as the default with existing sortable columns.
- **Scale context**: this is a teaching project; "efficient and scalable" targets are set at 10,000 rows per list, which is orders of magnitude beyond current data volumes.
- **Data location**: all three lists' data lives in the single relational database that is the system of record; each page's list query is a stored procedure owned by the module that owns the data (Catalog for courses; Enrollment module for enrollments and learners).
