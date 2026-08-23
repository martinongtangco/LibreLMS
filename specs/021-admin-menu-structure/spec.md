# Feature Specification: Admin Menu Structure

**Feature Branch**: `story/021-admin-menu-structure`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2025-07-31

**Status**: Complete (merged 2026-08-05)

**Input**: User description: "ensure Admin menus are kept and remembered so that any ui or visual enhancement wont break it"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin discovers all management sections from the navigation (Priority: P1)

An administrator logs in and expects to find every admin management area (Dashboard, Courses, Enrollments, Learners, Organizations, Upload) accessible from the main navigation bar without needing to guess URLs or navigate through multiple pages.

**Why this priority**: Without this, 5 of 6 admin page groups are effectively hidden. Admins cannot discover or manage the system properly. This is the core problem.

**Independent Test**: Can be fully tested by logging in as an admin user, switching to "Admin" view mode, and verifying that links to all 6 admin sections are visible and navigable from the primary navigation.

**Acceptance Scenarios**:

1. **Given** I am logged in as a user with an admin role (SuperUser or OrgAdmin), **When** I view the main navigation bar in Admin mode, **Then** I can see navigation entries for Dashboard, Courses, Enrollments, Learners, Organizations, and Upload
2. **Given** I am in Admin view mode, **When** I click any admin navigation entry, **Then** I am taken to the corresponding admin management page
3. **Given** I am in Admin view mode on mobile, **When** I open the hamburger menu, **Then** I can see and access the same admin navigation entries

---

### User Story 2 - Learner view hides admin sections (Priority: P2)

A user with admin capabilities who switches to "Learner" view mode should not see admin navigation entries, maintaining the separation between learner and admin workflows.

**Why this priority**: The existing Learner/Admin toggle is an established pattern. Preserving this role-based visibility is expected behavior and prevents confusion.

**Independent Test**: Can be fully tested by toggling the role pill between Learner and Admin and verifying admin links appear/disappear accordingly.

**Acceptance Scenarios**:

1. **Given** I am logged in as a user with admin role, **When** I switch the role toggle to "Learner", **Then** admin navigation entries are hidden from the main navigation
2. **Given** I am in Learner view mode, **When** I switch the role toggle to "Admin", **Then** admin navigation entries become visible in the main navigation
3. **Given** I am logged in as a pure learner (no admin role), **When** I view the navigation, **Then** I see only learner-facing links (My Courses, Browse Courses) and no role toggle

---

### User Story 3 - Admin menu persists across UI changes (Priority: P3)

Future UI visual enhancements or redesigns must not accidentally remove, hide, or break the admin menu structure. The admin menu definition must be documented as a stable requirement.

**Why this priority**: This is the user's explicit concern — admin menus have been lost before during UI work. Making this a spec requirement prevents regression.

**Independent Test**: Can be verified by checking that the spec explicitly lists all admin menu entries as functional requirements, and that any future UI change plan references this spec.

**Acceptance Scenarios**:

1. **Given** this spec exists documenting the admin menu structure, **When** a future UI enhancement is planned, **Then** the admin menu entries are listed as requirements that must be preserved
2. **Given** the admin menu is implemented, **When** the page layout or styling changes, **Then** all admin navigation entries remain accessible

---

### Edge Cases

- What happens when an OrgAdmin (scoped to one organization) views the menu — should they see all the same entries as a SuperUser?
- How does the menu behave when the navigation is in a transitional state (e.g., during role toggle)?
- What happens on very narrow screens where the navbar is collapsed?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The main navigation bar MUST display all admin management sections when the user is in Admin view mode
- **FR-002**: The admin menu MUST include entries for: Dashboard, Courses, Enrollments, Learners, Organizations, and Upload
- **FR-003**: Admin navigation entries MUST be hidden when the role toggle is set to "Learner"
- **FR-004**: Admin navigation entries MUST respect the existing role-based authorization (only visible to SuperUser and OrgAdmin roles)
- **FR-005**: The admin menu MUST be accessible on both desktop and mobile viewports
- **FR-006**: The active admin page MUST be visually indicated in the navigation (consistent with existing active link behavior)
- **FR-007**: New admin pages added in the future MUST have their menu entry defined in this spec before implementation

### Key Entities

- **Admin Menu Entry**: A navigation item with a label, target page path, icon, and role requirement (SuperUser/OrgAdmin)
- **Role View Mode**: The Learner/Admin toggle state that controls which navigation entries are visible (persisted in localStorage)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin user can reach any of the 6 admin management sections from the main navigation in 1 click
- **SC-002**: All 6 admin menu entries are visible and functional on both desktop and mobile viewports
- **SC-003**: Toggling between Learner and Admin view correctly shows/hides all admin entries with no orphaned or misplaced links
- **SC-004**: Future UI enhancement plans explicitly reference this spec's admin menu requirements, preventing accidental regression

## Assumptions

- The existing Learner/Admin role toggle pill in the account control area remains the mechanism for switching views
- Admin menu entries follow the same visual style and interaction pattern as existing learner navigation links
- The 6 admin page groups currently implemented (Dashboard, Courses, Enrollments, Learners, Organizations, Upload) represent the complete set of admin sections
- OrgAdmin and SuperUser see the same menu entries; scope-based access control is enforced at the page level, not the menu level
- The spec documentation itself serves as the durable reference that prevents future UI work from breaking the admin menu
