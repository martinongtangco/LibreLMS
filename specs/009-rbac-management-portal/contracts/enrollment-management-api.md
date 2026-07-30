# Contract: Enrollment Management API (Admin-facing)

**Module**: Management  
**Namespace**: `LibreLms.Modules.Management.Endpoints`

> This is the admin-facing enrollment API. The existing self-service enrollment API
> (`/api/enrollments`) in the Enrollment module remains unchanged.

## Endpoints

### GET /api/admin/enrollments

List enrollments visible to the current admin (scoped by organization).

**Query parameters**:
- `organizationId` (Guid?, optional) — filter by organization
- `studentId` (Guid?, optional) — filter by specific learner
- `courseId` (Guid?, optional) — filter by specific course
- `status` (string?, optional) — filter by status ("active", "completed", "cancelled")

**Response 200**:
```json
{
  "enrollments": [
    {
      "id": "guid",
      "studentId": "guid",
      "studentName": "string",
      "courseId": "guid",
      "courseTitle": "string",
      "organizationId": "guid",
      "organizationName": "string",
      "status": "active | completed | cancelled",
      "enrolledAt": "ISO 8601",
      "completedAt": "ISO 8601 | null"
    }
  ]
}
```

**Authorization**: SuperUser (all enrollments), OrgAdmin (enrollments in own subtree)

---

### POST /api/admin/enrollments

Enroll a learner in a course (admin enrollment).

**Request body**:
```json
{
  "studentId": "guid",
  "courseId": "guid"
}
```

**Validation**:
- Student and course must be within user's organizational scope
- Student must not already be enrolled in this course (FR-013)

**Response 201**:
```json
{
  "id": "guid",
  "studentId": "guid",
  "courseId": "guid",
  "enrolledAt": "ISO 8601"
}
```

**Response 400**: Validation error  
**Response 403**: Student or course outside user's scope  
**Response 409**: Already enrolled  
**Authorization**: SuperUser (any scope), OrgAdmin (own subtree)

---

### POST /api/admin/enrollments/bulk

Bulk enroll multiple learners into a course.

**Request body**:
```json
{
  "studentIds": ["guid", "guid", ...],
  "courseId": "guid"
}
```

**Validation**:
- Up to 500 student IDs per batch
- Course must be within user's scope
- Only students within user's scope are enrolled (others silently skipped)
- Already-enrolled students are skipped

**Response 200**:
```json
{
  "enrolled": 42,
  "skipped": 3,
  "errors": 0
}
```

**Response 400**: Validation error (e.g., batch too large)  
**Response 403**: Course outside user's scope  
**Authorization**: SuperUser (any scope), OrgAdmin (own subtree)

---

### DELETE /api/admin/enrollments/{id:guid}

Cancel an enrollment.

**Response 204**: No content — enrollment cancelled  
**Response 403**: Enrollment outside user's scope  
**Authorization**: SuperUser (any enrollment), OrgAdmin (own subtree)

---

## DTOs

### EnrollmentSummary

```csharp
record EnrollmentSummary(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid CourseId,
    string CourseTitle,
    Guid OrganizationId,
    string OrganizationName,
    string Status,
    DateTimeOffset EnrolledAt,
    DateTimeOffset? CompletedAt
);
```

### BulkEnrollmentRequest

```csharp
record BulkEnrollmentRequest(
    Guid[] StudentIds,
    Guid CourseId
);
```

### BulkEnrollmentResult

```csharp
record BulkEnrollmentResult(
    int Enrolled,
    int Skipped,
    int Errors
);
```
