# Feature Specification: Interactive Organization Chart View

**Feature Branch**: `story/013-org-chart-view`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2025-08-01

**Status**: Complete (merged 2026-07-31)

**Input**: User description: "create a dynamic tree view for the organization. it has to look like an actual org chart and you can create nodes below to add a new org. i dont want a list view or table for this. admins should be able to zoom in and out of the org. there should also be a context menu on each node box of the org to edit, disable, and add/assign a new or existing user or course"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View the Organization as an Interactive Chart (Priority: P1)

An admin navigates to the organization management page and sees the entire organizational hierarchy rendered as a visual org chart. Each organization is displayed as a node box showing its name and key summary information. Lines or connectors visually link parent organizations to their children. The admin can pan across and zoom in/out of the chart to navigate large hierarchies. The chart auto-fits to the viewport on initial load.

**Why this priority**: The core value of this feature is the visual org chart itself. Without the chart rendering and navigation, no other interaction (editing, adding nodes, context menus) is possible.

**Independent Test**: Can be fully tested by loading the org chart page with a pre-populated multi-level hierarchy, verifying all nodes and connectors render correctly, and confirming pan and zoom interactions work smoothly.

**Acceptance Scenarios**:

1. **Given** a multi-level organization hierarchy exists, **When** the admin opens the organization chart view, **Then** all organizations are displayed as connected node boxes arranged in a top-down hierarchical layout
2. **Given** the org chart is displayed, **When** the admin uses zoom controls (buttons, mouse wheel, or pinch gesture), **Then** the chart scales proportionally while maintaining node readability and connector lines
3. **Given** the org chart is displayed, **When** the admin pans or drags the canvas, **Then** the entire chart moves smoothly without nodes disappearing or connectors breaking
4. **Given** the org chart loads for the first time, **When** the hierarchy fits within the viewport, **Then** the chart auto-fits and centers itself in the view
5. **Given** the hierarchy is too large for the viewport, **When** the chart loads, **Then** the admin can pan and zoom to access all nodes

---

### User Story 2 - Create a New Sub-Organization via the Chart (Priority: P1)

An admin clicks or right-clicks on any organization node in the chart and selects an option to add a child organization. A form or dialog appears where the admin enters the new organization's details (name, description). Upon confirmation, the new organization node appears visually as a child of the selected parent, connected by a line, without requiring a full page reload.

**Why this priority**: The ability to grow the organization tree directly from the chart is a primary interaction. It makes the chart a living management tool rather than a static display.

**Independent Test**: Can be fully tested by right-clicking an existing org node, creating a new child org with a name and description, and verifying the new node appears as a connected child in the chart.

**Acceptance Scenarios**:

1. **Given** an organization node exists in the chart, **When** the admin selects "Add Child Organization" from the context menu, **Then** a form appears requesting the new organization's name and optional description
2. **Given** the admin has filled in the new organization details, **When** they confirm creation, **Then** the new node appears as a child of the selected parent with a connector line drawn between them
3. **Given** the admin attempts to create a child organization with a blank name, **When** they submit the form, **Then** the system shows a validation error and does not create the node
4. **Given** a new child organization is created, **When** the admin navigates away and returns to the chart, **Then** the new organization persists and is still visible in its correct position

---

### User Story 3 - Manage an Organization via Context Menu (Priority: P2)

An admin right-clicks on an organization node to open a context menu with actions: Edit, Disable, Add User, and Assign Course. Each action opens the appropriate form or dialog. After completing an action, the chart updates to reflect the change (e.g., a disabled node appears visually distinct) without requiring a full page reload.

**Why this priority**: The context menu is the primary mechanism for administering organizations from the chart view. It consolidates all management actions into an intuitive right-click interaction.

**Independent Test**: Can be fully tested by right-clicking various org nodes, performing each context menu action, and verifying the results are reflected in the chart and persisted correctly.

**Acceptance Scenarios**:

1. **Given** an org node is displayed, **When** the admin right-clicks it, **Then** a context menu appears with options: Edit Organization, Disable Organization, Add New User, Assign Existing User, and Assign Course
2. **Given** the admin selects "Edit Organization", **When** they modify the name or description and save, **Then** the node updates to show the new details on the chart
3. **Given** the admin selects "Disable Organization", **When** they confirm the action, **Then** the node visually changes to indicate it is disabled (e.g., grayed out or dimmed) and its child organizations are also disabled
4. **Given** a disabled organization node, **When** the admin selects "Enable Organization" from the context menu, **Then** the node returns to its active visual state and all descendant nodes are re-enabled
5. **Given** the admin selects "Edit Organization" on a disabled node, **Then** the edit form includes an option to re-enable the organization alongside other editable fields

---

### User Story 4 - Assign Users to an Organization from the Chart (Priority: P2)

From an organization node's context menu, an admin can either create a brand new user and assign them to the organization, or select an existing user from the system and assign them to that organization. After assignment, the user count or relevant indicator on the node updates to reflect the change.

**Why this priority**: Assigning people to organizations is a frequent admin task. Having it directly in the chart context menu eliminates navigation to separate user management pages.

**Independent Test**: Can be fully tested by right-clicking an org node, using both "Add New User" and "Assign Existing User" options, and verifying the users appear correctly associated with that organization.

**Acceptance Scenarios**:

1. **Given** the admin selects "Add New User" from an org node's context menu, **When** they fill in the new user's details and confirm, **Then** the user is created and assigned to that organization
2. **Given** the admin selects "Assign Existing User" from an org node's context menu, **When** they search for and select a user from the system, **Then** that user is assigned to the selected organization
3. **Given** a user is already assigned to another organization, **When** the admin assigns them to a new organization via the chart, **Then** the user can belong to multiple organizations simultaneously (or the system handles the transfer with a clear confirmation)
4. **Given** the admin creates a new user via the chart, **When** the user details form is submitted, **Then** required fields are validated and the user is created only if validation passes

---

### User Story 5 - Assign Courses to an Organization from the Chart (Priority: P3)

From an organization node's context menu, an admin can assign existing courses to the organization. A course selection interface appears listing available courses. After selection and confirmation, the course is associated with that organization and the node updates to reflect the assignment.

**Why this priority**: Course assignment is an important but secondary workflow compared to org structure and user management. It provides convenience but doesn't block other chart functionality.

**Independent Test**: Can be fully tested by right-clicking an org node, selecting "Assign Course", choosing a course from the available list, and verifying the assignment is reflected on the node and persists.

**Acceptance Scenarios**:

1. **Given** the admin selects "Assign Course" from an org node's context menu, **When** the course selection dialog opens, **Then** it lists courses available for assignment
2. **Given** a list of courses is displayed, **When** the admin selects one or more courses and confirms, **Then** those courses are associated with the selected organization
3. **Given** a course is already assigned to the organization, **When** the admin opens the course assignment dialog, **Then** the already-assigned course is indicated as such to avoid duplicate assignment
4. **Given** a course is assigned to an organization via the chart, **When** the admin navigates away and returns, **Then** the assignment persists and is still reflected on the node

---

### Edge Cases

- **What happens when the organization hierarchy is very deep (10+ levels)?** — The chart supports panning and zooming to navigate deep hierarchies without performance degradation; auto-fit still works but may show a zoomed-out view
- **What happens when an admin tries to disable a root organization?** — The system prevents disabling the root organization and displays a warning explaining that the root cannot be disabled
- **What happens when the org chart has no organizations?** — The chart displays an empty state message prompting the admin to create the first organization
- **What happens when two admins modify the chart simultaneously?** — The system reflects the latest state on the next refresh; conflicting edits are resolved with the last-write-wins approach
- **What happens when a disabled organization has active learners enrolled in courses?** — Disabling an organization also disables access for all learners within that org and its descendants, with a confirmation dialog warning the admin of the impact
- **What happens when a user tries to access the org chart without admin permissions?** — Non-admin users cannot access the org chart view; they see an access denied message or are redirected

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST render the organization hierarchy as a visual tree chart with node boxes and connector lines, not as a list or table
- **FR-002**: Each organization node MUST display the organization name and at minimum one summary indicator (e.g., user count or status)
- **FR-003**: System MUST support zoom in and zoom out of the org chart with smooth scaling
- **FR-004**: System MUST support panning (dragging) the chart canvas to navigate large hierarchies
- **FR-005**: System MUST auto-fit the chart to the viewport on initial load when the hierarchy fits within the visible area
- **FR-006**: System MUST provide a context menu on right-click of any organization node
- **FR-007**: Context menu MUST include an "Edit Organization" option that opens a form to modify the organization's details
- **FR-008**: Context menu MUST include a "Disable Organization" option that deactivates the organization and all its descendant organizations
- **FR-009**: Context menu MUST include an "Enable Organization" option for previously disabled organizations
- **FR-010**: Context menu MUST include an "Add New User" option that creates a new user account and assigns them to the selected organization
- **FR-011**: Context menu MUST include an "Assign Existing User" option that lets the admin search for and assign a current user to the selected organization
- **FR-012**: Context menu MUST include an "Assign Course" option that lets the admin select and associate existing courses with the selected organization
- **FR-013**: System MUST allow creating a new child organization node from any existing organization node in the chart
- **FR-014**: System MUST visually distinguish disabled organization nodes from active nodes (e.g., different color, opacity, or icon)
- **FR-015**: System MUST prevent disabling the root organization
- **FR-016**: System MUST update the chart in real-time after any action (create, edit, disable, assign) without requiring a full page reload
- **FR-017**: Only users with SuperUser or Organization Admin roles MUST have access to the org chart view
- **FR-018**: Organization Admins MUST only see and interact with their own organization and its descendants in the chart
- **FR-019**: System MUST display an empty state message when no organizations exist, with a call-to-action to create the first organization

### Key Entities

- **Organization**: A node in the hierarchy with a name, optional description, parent organization reference, enabled/disabled status, and a list of child organizations
- **User**: A person in the system with an identity, assigned role(s), and one or more organization memberships
- **Course**: A SCORM learning package associated with one or more organizations
- **Org Chart Node**: The visual representation of an Organization on the chart, displaying name, status, summary indicators, and position within the tree layout

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can view the complete organization hierarchy in a single chart view without switching between pages
- **SC-002**: Admins can create a new sub-organization in under 30 seconds from the chart view (right-click, fill form, confirm)
- **SC-003**: Chart zoom and pan interactions respond within 200ms, providing a smooth navigation experience
- **SC-004**: Context menu appears within 150ms of right-clicking a node
- **SC-005**: After performing any action (edit, disable, assign), the chart reflects the change within 1 second without a full page reload
- **SC-006**: Admins can assign a user to an organization in under 45 seconds from the chart view
- **SC-007**: 90% of admins performing org management tasks complete them without navigating away from the chart view
- **SC-008**: The chart renders correctly for hierarchies with up to 100 organization nodes without performance degradation

## Assumptions

- The existing RBAC system (SuperUser, Organization Admin, Learner roles) from spec 009 is in place and will be used for access control
- Organization hierarchy data already exists in the database from previous specs (orgs, users, courses)
- The management portal frontend already exists and has pages for organization management
- Users and courses can already be created and managed through existing portal pages; this feature adds convenience pathways from the chart
- A disabled organization prevents all learners within it from accessing courses; re-enabling restores access
- Courses assigned to an organization follow the inheritance model defined in spec 009 (child orgs can see parent courses)
- The chart is intended for admin use only; learners do not see the org chart
- "Zoom in/out" refers to visual scaling of the chart canvas (pan and zoom), not navigating to detail views of individual orgs
