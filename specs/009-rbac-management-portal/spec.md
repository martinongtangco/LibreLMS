# Feature Specification: RBAC Management Portal

**Feature Branch**: `story/009-rbac-management-portal`

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Created**: 2025-07-31

**Status**: Complete (merged 2026-07-31)

**Input**: User description: "we need a management portal with RBAC and dashboards. It should be able to manage learners and its organizations. there will be a root organization where a SuperUser resides, each node in the organization can have its own administrator but limited to its own organization and the branches underneath it. you can upload scorm courses to each organization and it can be unique for each level."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SuperUser Manages Organization Hierarchy (Priority: P1)

The SuperUser logs into the management portal and can create, view, edit, and delete organizations in a hierarchical tree. The SuperUser has unrestricted access across all organizations and can see the complete organizational structure from a single dashboard view.

**Why this priority**: Without the ability to create and structure the organization tree, no other functionality (learner management, course uploads, org-level administration) can exist. This is the foundational capability.

**Independent Test**: Can be fully tested by creating a root organization, adding child organizations, verifying the tree structure displays correctly, and confirming the SuperUser can navigate and modify any node in the tree.

**Acceptance Scenarios**:

1. **Given** the system has a root organization, **When** the SuperUser creates a new child organization under it, **Then** the child organization appears in the hierarchy and inherits the root as its parent
2. **Given** a multi-level organization hierarchy exists, **When** the SuperUser edits an organization's details (name, description), **Then** the changes are saved and reflected immediately across the portal
3. **Given** an organization exists with no learners or sub-organizations, **When** the SuperUser deletes it, **Then** the organization and all its data are removed from the system
4. **Given** an organization has child organizations or assigned learners, **When** the SuperUser attempts to delete it, **Then** the system prevents deletion and displays a warning explaining the constraint
5. **Given** the SuperUser views the dashboard, **When** they review organization metrics, **Then** they see aggregate data across all organizations (total learners, total courses, active enrollments)

---

### User Story 2 - Organization Admin Manages Learners Within Their Scope (Priority: P1)

An Organization Admin logs into the portal and can manage learner accounts within their own organization and all descendant (child/sub-child) organizations. They can view, create, edit, and remove learners, but cannot access or modify anything outside their organizational subtree.

**Why this priority**: Managing learners is the primary operational task for org-level administrators. Without this, organizations cannot onboard or manage their participants.

**Independent Test**: Can be fully tested by logging in as an OrgAdmin for a specific organization, creating a learner, verifying they can see that learner, and confirming they cannot see or modify learners in unrelated branches of the organization tree.

**Acceptance Scenarios**:

1. **Given** an OrgAdmin for Organization A, **When** they create a learner, **Then** the learner is assigned to Organization A and appears in the admin's learner list
2. **Given** Organization A has a child Organization B, **When** the OrgAdmin for A views learners, **Then** they can see and manage learners in both A and B
3. **Given** Organization A and Organization C are sibling organizations under the root, **When** the OrgAdmin for A attempts to view learners in C, **Then** the system denies access and the learner list for C is not visible
4. **Given** a learner exists in Organization A, **When** the OrgAdmin for A edits the learner's details, **Then** the changes are saved and the learner's record is updated
5. **Given** a learner exists in Organization A, **When** the OrgAdmin for A removes the learner, **Then** the learner is no longer accessible within that organization and their enrollments are cancelled

---

### User Story 3 - Upload and Manage SCORM Courses per Organization (Priority: P1)

Authorized users (SuperUser or Organization Admin within scope) can upload SCORM course packages to a specific organization. Each organization can host its own unique set of courses. Courses uploaded to an organization are managed independently of courses at other organizational levels.

**Why this priority**: Course content is the core asset of an LMS. Without the ability to upload and organize courses per organization, the platform cannot deliver learning content.

**Independent Test**: Can be fully tested by uploading a SCORM package to an organization, verifying it appears in that organization's course catalog, and confirming it does not interfere with courses at other organizational levels.

**Acceptance Scenarios**:

1. **Given** the SuperUser is on the course management page for Organization A, **When** they upload a SCORM package, **Then** the package is processed and the course becomes available for Organization A
2. **Given** an OrgAdmin for Organization A uploads a SCORM course, **Then** the course is associated with Organization A and is managed within the org admin's scope
3. **Given** Organization A has its own courses and Organization B (a child of A) has its own courses, **When** Organization B's courses are viewed by its admin, **Then** the list shows both B's local courses and A's inherited courses, with clear distinction between local and inherited
4. **Given** a parent organization has courses that are inherited by a child organization, **When** the OrgAdmin for the child organization hides a specific inherited course, **Then** that course is no longer visible or accessible to learners in the child organization but remains available to the parent
5. **Given** a SCORM course is uploaded to an organization, **When** the course is launched by a learner, **Then** the SCORM content renders correctly and tracks learner progress (initialization, completion, score)
5. **Given** a learner in Organization B views their available courses, **When** they browse the catalog, **Then** they see courses from their organization and all ancestor organizations (unless hidden by an admin)
6. **Given** an invalid or corrupted SCORM package is uploaded, **When** the system processes it, **Then** the upload is rejected with a clear error message and the course is not created

---

### User Story 4 - Role-Based Access Enforcement (Priority: P2)

The portal enforces role-based access control so that users can only see and perform actions permitted by their role and organizational scope. The SuperUser has full system access. Organization Admins have full access within their own organization and all descendant organizations. Learners can only access courses they are enrolled in.

**Why this priority**: Access control is essential for security and data isolation between organizational units. Without proper RBAC, org boundaries are meaningless and data leaks between tenants are possible.

**Independent Test**: Can be fully tested by creating multiple users with different roles, assigning them to different organizations, and verifying each user can only access resources within their permitted scope.

**Acceptance Scenarios**:

1. **Given** a SuperUser account, **When** they access the portal, **Then** they can see all organizations, all learners, all courses, and all dashboards without restriction
2. **Given** an OrgAdmin for Organization A with child organizations B and C, **When** they access the portal, **Then** they can manage learners and courses in A, B, and C but not in any sibling or unrelated organization
3. **Given** a Learner account assigned to Organization A, **When** they access the portal, **Then** they can only view and interact with courses they are enrolled in
4. **Given** a user attempts to access a resource outside their organizational scope, **When** the request is made, **Then** access is denied with an appropriate message
5. **Given** an OrgAdmin attempts to elevate their permissions or access SuperUser-only features, **When** the request is made, **Then** the system denies the action

---

### User Story 5 - Dashboard with Organization and Learner Metrics (Priority: P2)

The management portal provides role-aware dashboards showing key metrics. The SuperUser sees system-wide metrics. Organization Admins see metrics for their organizational subtree. Metrics include learner counts, course counts, enrollment activity, and completion rates.

**Why this priority**: Dashboards provide at-a-glance operational visibility. Without them, administrators must manually navigate through lists and reports to understand system health and usage.

**Independent Test**: Can be fully tested by populating the system with sample data (organizations, learners, courses, enrollments, completions) and verifying the dashboard displays accurate metrics scoped to the logged-in user's role and organization.

**Acceptance Scenarios**:

1. **Given** the SuperUser views the dashboard, **When** the page loads, **Then** they see aggregate metrics for the entire system (total organizations, total learners, total courses, overall completion rates)
2. **Given** an OrgAdmin for Organization A views the dashboard, **When** the page loads, **Then** they see metrics scoped to Organization A and its descendants only
3. **Given** learners complete courses, **When** the dashboard is refreshed, **Then** completion rate metrics reflect the updated data
4. **Given** the dashboard loads, **When** a user views it, **Then** the page renders within 3 seconds regardless of data volume

---

### User Story 6 - Assign Learners to Courses (Priority: P3)

Organization Admins can enroll learners into courses that are available within their organizational scope. Enrollment can be done individually or in bulk for multiple learners.

**Why this priority**: Course enrollment is the bridge between learners and content. It is necessary for the learning workflow but depends on both learner management (P1) and course management (P1) being functional first.

**Independent Test**: Can be fully tested by having an OrgAdmin enroll a learner in a course within their scope, then verifying the learner can access the course and the enrollment is tracked in the admin's dashboard.

**Acceptance Scenarios**:

1. **Given** a learner and a course exist in the same organization, **When** the OrgAdmin enrolls the learner, **Then** the learner can access the course and the enrollment appears in both the learner and course records
2. **Given** multiple learners exist in an organization, **When** the OrgAdmin performs a bulk enrollment into a course, **Then** all selected learners are enrolled and each enrollment is tracked individually
3. **Given** a learner is already enrolled in a course, **When** the OrgAdmin attempts to enroll them again, **Then** the system recognizes the existing enrollment and does not create a duplicate
4. **Given** an OrgAdmin attempts to enroll a learner in a course from an organization outside their scope, **When** the enrollment is attempted, **Then** the action is denied

---

### Edge Cases

- What happens when an organization is deleted that has active learner enrollments? Enrollments are cancelled and learners are notified or reassigned.
- What happens when an OrgAdmin is reassigned to a different organization? Their access scope changes to match the new organization's subtree immediately.
- How does the system handle a SCORM package that references external resources? The package is validated and missing/corrupted references result in an upload rejection with details.
- What happens when a learner's organization is changed? The learner retains existing course enrollments but may lose access to courses not available at their new organization level.
- What happens if two users attempt to upload a course with the same name to the same organization? The system prevents duplicate course titles within a single organization.
- What happens when the root organization's SuperUser is the only user in the system? The system prevents the last SuperUser from being deleted or demoted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST maintain a hierarchical organization structure with a single root organization
- **FR-002**: System MUST support three user roles: SuperUser, Organization Admin, and Learner
- **FR-003**: System MUST assign exactly one primary organization to each user
- **FR-004**: System MUST allow the SuperUser to create, read, update, and delete organizations at any level in the hierarchy
- **FR-005**: System MUST allow the SuperUser to create and manage users of any role at any organization
- **FR-006**: System MUST allow Organization Admins to create, read, update, and delete learners within their own organization and all descendant organizations
- **FR-007**: System MUST allow Organization Admins to create and manage sub-organizations under their own organization
- **FR-008**: System MUST enforce access boundaries so that an Organization Admin cannot access or modify data outside their organizational subtree
- **FR-009**: System MUST support SCORM 1.2 package upload and parsing for course content delivery
- **FR-010**: System MUST associate each course with exactly one organization at the time of upload
- **FR-011**: System MUST allow course uploads by the SuperUser (to any organization) and by Organization Admins (to their own organization or descendants)
- **FR-019**: System MUST make courses from parent organizations automatically visible to child organizations by default (course inheritance)
- **FR-020**: System MUST allow Organization Admins to hide specific inherited (parent) courses from their organization's visible catalog
- **FR-021**: System MUST distinguish between locally uploaded courses and inherited courses in the course management interface
- **FR-012**: System MUST provide role-aware dashboards showing metrics scoped to the user's role and organizational access
- **FR-013**: System MUST allow Organization Admins to enroll learners into courses within their organizational scope
- **FR-014**: System MUST prevent deletion of an organization that contains active learners, courses, or child organizations (require reassignment or removal first)
- **FR-015**: System MUST prevent removal or demotion of the last remaining SuperUser
- **FR-016**: System MUST provide a management portal accessible via web browser for all authenticated users
- **FR-017**: System MUST allow Learners to view and launch courses they are enrolled in
- **FR-018**: System MUST track learner progress and completion status for SCORM courses (leveraging existing SCORM runtime tracking)

### Key Entities

- **Organization**: Represents a node in the organizational hierarchy. Has a name, optional description, a parent organization (null for root), and zero or more child organizations. Each organization can host its own set of courses.
- **User**: Represents a person in the system. Has a role (SuperUser, Organization Admin, Learner), profile information, and is assigned to exactly one primary organization.
- **Role**: Defines the permission level of a user. SuperUser (full system access), Organization Admin (full access within their org subtree), Learner (course consumption only).
- **Course**: Represents a SCORM course package. Has a title, associated SCORM content, and is linked to exactly one organization. Each organization can have its own unique courses.
- **Enrollment**: Represents a learner's enrollment in a course. Tracks enrollment date, status (active, completed, cancelled), and completion data.
- **Course Visibility Override**: Represents an Organization Admin's decision to hide an inherited parent course from their organization's catalog. Links an organization to an inherited course and records the visibility state.
- **Organization Tree**: The complete hierarchical structure of organizations, representing the parent-child relationships across the system.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: SuperUser can create and configure a multi-level organization hierarchy (3+ levels deep) within 5 minutes
- **SC-002**: Organization Admins can manage learners and courses within their scope with zero access to data outside their organizational subtree
- **SC-003**: SCORM course packages can be uploaded and made available for learners within 2 minutes of upload completion
- **SC-004**: Dashboard loads and displays accurate, role-scoped metrics within 3 seconds for organizations with up to 1,000 learners
- **SC-005**: 95% of learner enrollment actions are completed successfully on first attempt without errors
- **SC-006**: Role-based access control prevents 100% of unauthorized cross-organization access attempts
- **SC-007**: Users can complete their primary administrative tasks (create org, add learner, upload course, enroll learner) without more than 3 clicks from the dashboard
- **SC-008**: System supports organization hierarchies of up to 10 levels deep without performance degradation

## Assumptions

- Users belong to exactly one primary organization; cross-organization user sharing is out of scope for this release
- Organization Admins can manage both learners AND sub-organizations within their organizational subtree
- Courses cascade down by default — learners in child organizations can access courses uploaded at their level and at any ancestor level. Organization Admins can hide specific parent courses from their own organization's visible catalog, providing opt-out control over inherited content
- Existing SCORM course delivery and progress tracking infrastructure will be reused
- Learner authentication and session management are handled by existing or soon-to-be-implemented authentication; this spec focuses on the management portal and RBAC layer
- The management portal is a web-based interface accessible via browser
- Bulk enrollment operations support up to 500 learners per batch
- Organization names must be unique within their immediate parent (siblings cannot share names, but cousins can)
