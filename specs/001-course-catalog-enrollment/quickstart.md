# Quickstart Validation: Course Catalog & Enrollment

**Date**: 2025-07-29
**Feature**: [spec.md](./spec.md)

This guide validates the feature works end-to-end. See [data-model.md](./data-model.md) for entity details and [contracts/api.md](./contracts/api.md) for API specifications.

## Prerequisites

1. Dev container running: `devcontainer up --workspace-folder .`
2. MSSQL healthy: `docker compose ps mssql` shows healthy status
3. Solution built: `dotnet build LearningLms.slnx`

## Validation Scenarios

### 1. Course Catalog Browse (FR-001, FR-008)

**Setup**: Ensure seed data has been applied (courses exist in the database).

**Run**:
```bash
dotnet run --project src/Host
# In another terminal:
curl http://localhost:5000/api/courses
```

**Expected**: `200 OK` with a JSON array of courses, each containing `id`, `title`, `shortDescription`, `category`, `duration`.

**Empty state** (FR-008): If no courses exist, response is `200 OK` with `"courses": []` and the web portal displays a "no courses available" message.

---

### 2. Course Detail View (FR-003)

**Run**:
```bash
curl http://localhost:5000/api/courses/{course-id-from-step-1}
```

**Expected**: `200 OK` with full course details including `fullDescription`.

**404 test**:
```bash
curl http://localhost:5000/api/courses/00000000-0000-0000-0000-000000000000
```

**Expected**: `404 Not Found`.

---

### 3. Filter/Search (FR-002)

**Run**:
```bash
# Filter by category
curl "http://localhost:5000/api/courses?category=Programming"

# Search by title
curl "http://localhost:5000/api/courses?search=intro"
```

**Expected**: `200 OK` with filtered results matching the criteria.

---

### 4. Enroll in a Course (FR-004, FR-006)

**Setup**: Authenticate as a seeded test user (e.g., via browser login or test token).

**Run**:
```bash
curl -X POST http://localhost:5000/api/enrollments \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"courseId": "{course-id}"}'
```

**Expected**: `201 Created` with enrollment object containing `id`, `studentId`, `courseId`, `enrolledAt`.

---

### 5. Duplicate Enrollment Prevention (FR-005)

**Run**: Repeat the POST from step 4 with the same course ID.

**Expected**: `409 Conflict` — no duplicate enrollment created.

---

### 6. View Enrolled Courses (FR-007)

**Run**:
```bash
curl http://localhost:5000/api/enrollments/my \
  -H "Authorization: Bearer <token>"
```

**Expected**: `200 OK` with enrollments array containing the course from step 4, including `courseTitle` and `enrolledAt`.

**Empty state** (FR-008): If no enrollments exist, response is `200 OK` with `"enrollments": []`.

---

### 7. Architecture Tests (Constitution Principle III)

**Run**:
```bash
dotnet test tests/ArchitectureTests
```

**Expected**: All tests pass — no module references another module's internals directly.

---

### 8. Persistence Verification (FR-009, FR-010)

**Run**:
```bash
# Stop the host
# Restart
dotnet run --project src/Host

# Verify data survived
curl http://localhost:5000/api/courses
curl http://localhost:5000/api/enrollments/my -H "Authorization: Bearer <token>"
```

**Expected**: Courses and enrollments are still present after restart.
