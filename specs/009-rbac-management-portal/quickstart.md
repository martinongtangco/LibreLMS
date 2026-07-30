# Quickstart Validation Guide: RBAC Management Portal

## Prerequisites

- Docker and Docker Compose running
- `.devcontainer` environment active (mssql + valkey services up)
- Database migrated with all existing and new migrations applied
- Seed data populated (root organization + SuperUser created)

## Setup Commands

```bash
# Start infrastructure
docker compose up -d

# Ensure database is current
dotnet ef database update --project src/Host --startup-project src/Host

# Run the application
dotnet run --project src/Host
```

## Validation Scenarios

### Scenario 1: SuperUser Creates Organization Hierarchy

**Goal**: Verify the SuperUser can create a multi-level org structure.

1. Login as SuperUser (seeded credentials — check `ManagementSeeder`)
2. Navigate to `/Admin/Organizations`
3. Click "Create Organization" and create "Engineering" under root
4. Click "Create Organization" and create "Backend Team" under "Engineering"
5. Verify the org tree displays: Root → Engineering → Backend Team
6. Verify dashboard shows `totalOrganizations: 3`

**Expected**: All operations succeed; tree structure renders correctly.

---

### Scenario 2: Organization Admin Learner Management

**Goal**: Verify an OrgAdmin can manage learners within their scope.

1. As SuperUser, create an OrgAdmin user assigned to "Engineering" org
2. Logout and login as the OrgAdmin
3. Navigate to `/Admin/Learners`
4. Create a learner "Alice" assigned to "Engineering"
5. Create a learner "Bob" assigned to "Backend Team"
6. Verify both learners appear in the OrgAdmin's learner list
7. Attempt to access a learner in a sibling org (should be denied — 403)

**Expected**: Learners in the org subtree are visible; sibling org learners are inaccessible.

---

### Scenario 3: SCORM Course Upload and Inheritance

**Goal**: Verify course upload, inheritance, and visibility override.

1. As SuperUser, upload a SCORM package to the root organization (use existing sample `.zip` from `tests/` or `wwwroot/scorm-content/`)
2. Login as OrgAdmin for "Engineering"
3. Navigate to `/Admin/Courses` — verify the root org course appears as "inherited"
4. Toggle "Hide" on the inherited course
5. Verify the course no longer appears in Engineering's course list
6. Upload a local SCORM course to "Engineering" — verify it appears as "local"
7. Login as a Learner in "Backend Team" — verify they see both Engineering's local course and the root course (if not hidden at Engineering level)

**Expected**: Inheritance works; hide override blocks inherited courses at the target org level.

---

### Scenario 4: Enrollment Management

**Goal**: Verify admin enrollment (single and bulk).

1. As OrgAdmin, navigate to `/Admin/Enrollments`
2. Enroll "Alice" in a course (single enrollment)
3. Verify the enrollment appears in the list
4. Perform bulk enrollment of multiple learners into a course
5. Verify the result shows counts: `enrolled: N, skipped: M, errors: 0`
6. Verify learners can see their enrolled courses at `/MyCourses`

**Expected**: Enrollments created successfully; learners can access enrolled courses.

---

### Scenario 5: Role-Based Access Control Enforcement

**Goal**: Verify RBAC prevents unauthorized access.

1. Create two sibling orgs: "Engineering" and "Marketing" under root
2. Create OrgAdmin for Engineering, OrgAdmin for Marketing
3. As Engineering OrgAdmin, attempt to:
   - Access Marketing's learners → **403 Forbidden**
   - Upload a course to Marketing → **403 Forbidden**
   - View Marketing's dashboard metrics → **403 Forbidden**
4. As Marketing OrgAdmin, verify same restrictions for Engineering
5. As SuperUser, verify full access to both orgs

**Expected**: 100% of cross-org access attempts are denied (SC-006).

---

### Scenario 6: Dashboard Metrics

**Goal**: Verify role-scoped dashboard metrics.

1. Populate test data: 3 orgs, 10 learners, 5 courses, 8 enrollments, 3 completions
2. Login as SuperUser → verify system-wide metrics:
   - `totalOrganizations: 3`, `totalLearners: 10`, `totalCourses: 5`
3. Login as OrgAdmin → verify subtree-scoped metrics:
   - Counts reflect only the org's subtree
4. Verify dashboard loads within 3 seconds (SC-004)

**Expected**: Metrics are accurate and role-scoped.

---

### Scenario 7: Edge Case — Delete Protection

**Goal**: Verify organizations with dependents cannot be deleted.

1. Create an org with learners and courses
2. Attempt to delete it → **400 Bad Request** with message about dependents
3. Remove all learners and courses from the org
4. Delete the org → **204 No Content** (success)
5. Attempt to delete the last SuperUser → **400 Bad Request**
6. Attempt to demote the last SuperUser → **400 Bad Request**

**Expected**: Deletion blocked when dependents exist; last SuperUser protected.

---

## Architecture Tests

```bash
# Verify module boundaries are intact (Constitution Principle III)
dotnet test tests/ArchitectureTests

# Expected: All tests pass, including new Management module boundary checks
```

## Rollback

If validation fails, revert database changes:

```bash
dotnet ef database update 0 --project src/Host --startup-project src/Host
```
