# Feature Specification: Course Browse Search, Filter, and Pagination

**Feature Branch**: `story/019-course-search-pagination`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features.

**Created**: 2025-07-31

**Status**: Complete (merged 2026-08-05)

**Input**: User description: "the Browse Courses search box and filter doesnt work. I want to implement this along with pagination. One thing I'm very particular is that i want search and pagination written in T-SQL. I think it's the most efficient way to do pagination and search"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Search Courses by Title (Priority: P1)

As a learner browsing the course catalog, I want to type into a search box and instantly see only courses matching my search terms, so I can quickly find courses of interest without scrolling through the entire catalog.

**Why this priority**: Search is the primary mechanism for course discovery. If search doesn't work, the Browse Courses page is unusable for catalogs with more than a handful of courses. This is the most critical defect to fix.

**Independent Test**: Can be fully tested by entering text into the search box and verifying that the course list updates to show only matching courses.

**Acceptance Scenarios**:

1. **Given** I am on the Browse Courses page, **When** I type a term that matches one or more course titles, **Then** the course list updates to show only courses whose titles contain the search term
2. **Given** I am on the Browse Courses page, **When** I type a term that matches no course titles, **Then** an empty state message is displayed indicating no matches were found
3. **Given** I have typed a search term, **When** I clear the search box, **Then** the full course list is restored
4. **Given** I am searching with a term, **When** I modify the search term, **Then** the results update to reflect the new term

---

### User Story 2 - Filter Courses by Category (Priority: P1)

As a learner, I want to select a category from a dropdown to see only courses in that category, so I can narrow down the catalog by subject area.

**Why this priority**: Category filtering is a core navigation mechanism. Combined with search, it forms the complete filtering system. Both are broken and both are P1.

**Independent Test**: Can be fully tested by selecting a category and verifying only courses in that category appear.

**Acceptance Scenarios**:

1. **Given** I am on the Browse Courses page, **When** I select a category from the dropdown, **Then** the course list updates to show only courses in that category
2. **Given** I have a category filter applied, **When** I select "All Categories", **Then** all courses are shown again
3. **Given** I have a search term entered AND a category selected, **When** the filter applies, **Then** the course list shows only courses matching BOTH the search term AND the category
4. **Given** I have active filters applied, **When** I click the Clear button, **Then** both search and category are reset and all courses are shown

---

### User Story 3 - Paginate Course Results (Priority: P2)

As a learner viewing a large course catalog, I want courses to load in pages with navigation between pages, so I don't have to scroll through hundreds of courses to find what I need.

**Why this priority**: Pagination is essential for usability with large catalogs but is secondary to fixing the broken search/filter functionality.

**Independent Test**: Can be fully tested by navigating between pages and verifying correct courses appear on each page.

**Acceptance Scenarios**:

1. **Given** there are more courses than fit on one page, **When** I view the Browse Courses page, **Then** I see only the first page of results with pagination controls (Previous/Next or page numbers)
2. **Given** I am on the first page, **When** I click Next, **Then** I see the next set of courses with the page indicator updating
3. **Given** I am on a middle page, **When** I click Previous, **Then** I see the previous page of courses
4. **Given** I am on the last page, **When** I view the page, **Then** the Next button is disabled or hidden
5. **Given** I am on the first page, **When** I view the page, **Then** the Previous button is disabled or hidden
6. **Given** I have search or filter applied while on page N, **When** results change, **Then** I am returned to page 1 of the filtered results

---

### User Story 4 - Combined Search, Filter, and Pagination (Priority: P2)

As a learner, I want search, category filtering, and pagination to work together seamlessly, so I can progressively narrow down my course selection.

**Why this priority**: This is the integrated workflow — critical for a polished experience but depends on P1 stories being functional first.

**Independent Test**: Can be tested by applying search + filter + page navigation in sequence.

**Acceptance Scenarios**:

1. **Given** I have search and category filters applied, **When** I navigate to page 2, **Then** the filters remain active and I see page 2 of the filtered results
2. **Given** I am on page 2 of filtered results, **When** I change the search term, **Then** I see page 1 of the new filtered results
3. **Given** I am on page 2 of filtered results, **When** I change the category, **Then** I see page 1 of the new filtered results

---

### Edge Cases

- What happens when the catalog has fewer courses than the page size (no pagination needed)?
- What happens when search + filter combination returns zero results?
- What happens when a user navigates to a page number that no longer exists after changing filters (e.g., was on page 5, changes filter and only 2 pages remain)?
- How does the system handle special characters in search input (e.g., quotes, ampersands)?
- What happens when the search term is a single space or only whitespace?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST filter courses by title text when the user types in the search box, with results updating automatically as the user types (with a short debounce delay)
- **FR-002**: The system MUST filter courses by category when the user selects a value from the category dropdown
- **FR-003**: The system MUST support combining search and category filters simultaneously (logical AND)
- **FR-004**: The system MUST provide a Clear button that resets both search and category filters and restores the full course list
- **FR-005**: The system MUST paginate course results, displaying a configurable number of courses per page with navigation controls (Previous/Next)
- **FR-006**: The system MUST reset to page 1 when search or filter criteria change
- **FR-007**: The system MUST hide or disable the Previous button on page 1 and the Next button on the last page
- **FR-008**: The system MUST display an empty state message when no courses match the current search/filter criteria
- **FR-009**: Search must be case-insensitive (e.g., "python" matches "Python Programming")
- **FR-010**: Search must support partial matching (e.g., "data" matches "Data Science Fundamentals")
- **FR-011**: Search and pagination queries MUST be executed natively in T-SQL (stored procedures or parameterized SQL) rather than client-side or LINQ-to-SQL translation, for performance efficiency
- **FR-012**: Course title search MUST utilize SQL Server Full-Text Search (FTS) if the database environment supports it, to enable tokenized, language-aware searching with ranking; fall back to `LIKE`-based search if FTS is not available

### Non-Functional Requirements

- **NFR-001**: Search results must update within 500ms of the user stopping typing (debounce included)
- **NFR-002**: Pagination controls must update without a full page reload
- **NFR-003**: The search and pagination queries must scale efficiently to catalogs with 10,000+ courses using database-level filtering and pagination

### Key Entities

- **Course**: Represents a learnable unit with attributes: Id (GUID), Title, ShortDescription, FullDescription, Category, Duration, OrganizationId, CreatedAt
- **SearchFilter**: Transient query parameters — Search term (string), Category (string), Page number (integer), Page size (integer)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can search and find a course by title within 3 interactions (type, see results, click)
- **SC-002**: Users can filter courses by category with a single dropdown selection
- **SC-003**: Search and filter results update visibly within 1 second of user input
- **SC-004**: Pagination loads the next/previous page within 1 second of clicking
- **SC-005**: 100% of the acceptance scenarios defined in User Stories pass manual testing
- **SC-006**: The Browse Courses page remains responsive and usable with catalogs of 1,000+ courses
- **SC-007**: Search and pagination queries execute at the database level using native database queries, without loading the full course table into application memory

## Assumptions

- The existing HTMX-based partial page update pattern will continue to be used for search/filter/pagination interactions
- The page size for pagination defaults to 12 courses per page (a common grid layout size for course cards)
- Search matches against course Title only (not ShortDescription or FullDescription), consistent with the current implementation
- Results are sorted alphabetically by course title (A-Z), consistent with the current implementation
- The current course visibility logic (org-scoped vs. all courses) remains in place and is applied before search/filter/pagination
- The existing Course domain model and Courses table schema do not require changes for this feature
- Users are authenticated or unauthenticated; the feature works for both states with appropriate org-scoping
