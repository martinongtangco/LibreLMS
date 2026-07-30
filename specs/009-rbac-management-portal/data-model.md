# Data Model: RBAC Management Portal

## New Entities

### Organization

Represents a node in the organizational hierarchy. Every organization except the root has exactly one parent. An organization can have zero or more child organizations and can host its own courses.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | Guid | PK, required | Unique identifier |
| Name | string | Required, max 200 chars, unique within parent | Display name for the organization |
| Description | string? | Optional, max 2000 chars | Free-text description |
| ParentId | Guid? | FK → Organization.Id, nullable | Null for root organization |
| CreatedAt | DateTimeOffset | Required, server default | Creation timestamp |
| IsDeleted | bool | Default false | Soft delete flag |

**Relationships**:
- Self-referencing: `ParentId` → `Organization.Id` (adjacency list)
- One-to-many: Organization → Courses (via `Course.OrganizationId`)
- One-to-many: Organization → Students (via `Student.OrganizationId`)
- One-to-many: Organization → CourseVisibilityOverrides (via `CourseVisibilityOverride.OrganizationId`)

**Validation rules**:
- Name must be unique among sibling organizations (same ParentId)
- Root organization (ParentId = null) cannot be deleted
- Cannot delete an organization with active children, learners, or courses (FR-014)
- Maximum tree depth: 10 levels (SC-008)
- Exactly one root organization must exist at all times

**State transitions**:
- Created → Active (default)
- Active → Deleted (soft delete, only if no dependents)

---

### UserRole

Defines the permission level of a user. Stored as a string value in the `Student.Roles` field for backward compatibility.

| Value | Display Name | Permissions |
|-------|-------------|-------------|
| SuperUser | Super User | Full system access — all orgs, all users, all courses |
| OrgAdmin | Organization Admin | Full access within own org subtree — manage learners, sub-orgs, courses |
| Learner | Learner | Course consumption only — view enrolled courses, launch SCORM |

**Notes**:
- Existing "Admin" role value is migrated to "SuperUser" on data migration
- Each user has exactly one role (single value in Roles field, not comma-separated)
- Role changes are tracked; last SuperUser cannot be demoted (FR-015)

---

### CourseVisibilityOverride

Records an Organization Admin's decision to hide a specific inherited (parent) course from their organization's visible catalog.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | Guid | PK, required | Unique identifier |
| OrganizationId | Guid | FK → Organization.Id, required | The org whose admin made the override |
| CourseId | Guid | FK → Course.Id, required | The inherited course being hidden |
| IsHidden | bool | Required, default true | Visibility state (true = hidden) |
| CreatedAt | DateTimeOffset | Required, server default | When the override was created |
| CreatedBy | Guid? | FK → Student.Id, optional | Which admin created the override |

**Validation rules**:
- CourseId must reference a course NOT owned by OrganizationId (only inherited courses can be overridden)
- One override per (OrganizationId, CourseId) pair (unique constraint)
- Course must be from an ancestor organization (enforced at application level)

---

## Modified Existing Entities

### Student (Modified)

| Field | Type | Constraints | Change |
|-------|------|-------------|--------|
| Id | Guid | PK | Existing |
| Name | string | Required | Existing |
| Email | string | Required, unique | Existing |
| PasswordHash | string | Required | Existing |
| Roles | string | Required | **Extended**: values now "SuperUser", "OrgAdmin", "Learner" |
| OrganizationId | Guid | FK → Organization.Id, required | **NEW** — primary organization assignment |
| CreatedAt | DateTimeOffset | Required | Existing |

**Validation rules** (new):
- OrganizationId must reference an existing organization
- Exactly one role value (no comma-separated roles)
- Cannot set last SuperUser's role to anything other than SuperUser

---

### Course (Modified)

| Field | Type | Constraints | Change |
|-------|------|-------------|--------|
| Id | Guid | PK | Existing |
| Title | string | Required | Existing |
| ShortDescription | string | Existing | Existing |
| FullDescription | string | Existing | Existing |
| Category | string | Existing | Existing |
| Duration | string | Existing | Existing |
| OrganizationId | Guid | FK → Organization.Id, required | **NEW** — owning organization |
| CreatedAt | DateTimeOffset | Required | Existing |

**Validation rules** (new):
- OrganizationId must reference an existing organization
- Title must be unique within the same organization (FR-015 edge case)

---

### Enrollment (Unchanged)

No structural changes to the Enrollment entity. However, enrollment queries will be filtered by organizational scope at the application level. OrgAdmins can only enroll learners and courses within their subtree.

---

### ScormPackage (Unchanged)

No structural changes. SCORM packages remain linked to Courses via `CourseId`. Organization-level access is inherited through the Course→Organization relationship.

---

### CourseAttempt (Unchanged)

No structural changes. Attempts are scoped to Student+Course pairs. Access control is enforced at the query/API level, not the entity level.

## Entity Relationship Diagram

```
Organization (1) ────< Organization (N)     [self-ref, ParentId]
Organization (1) ────< Student (N)          [via Student.OrganizationId]
Organization (1) ────< Course (N)           [via Course.OrganizationId]
Organization (1) ────< CourseVisibilityOverride (N) [via CVO.OrganizationId]

Student (1) ────< Enrollment (N)            [via Enrollment.StudentId]
Course (1) ────< Enrollment (N)             [via Enrollment.CourseId]
Course (1) ────< ScormPackage (0..1)        [via ScormPackage.CourseId]
Course (1) ────< CourseVisibilityOverride (N) [via CVO.CourseId]
Student (1) ────< CourseAttempt (N)         [via CourseAttempt.StudentId]
Course (1) ────< CourseAttempt (N)          [via CourseAttempt.CourseId]
```

## Migration Strategy

1. **Add Organizations table** with root org seed
2. **Add OrganizationId to Students** (nullable first, populate, then NOT NULL)
3. **Add OrganizationId to Courses** (nullable first, populate, then NOT NULL)
4. **Add CourseVisibilityOverrides table**
5. **Backfill**: Assign all existing students and courses to root organization
6. **Migrate roles**: "Admin" → "SuperUser", all others → "Learner"
7. **Add unique constraints**: Student name uniqueness within parent org, course title uniqueness within org
