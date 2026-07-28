# API Contracts: Course Catalog & Enrollment

**Date**: 2025-07-29
**Feature**: [spec.md](./spec.md)

All endpoints are ASP.NET Core minimal APIs mapped in `Program.cs` via module registration extensions.

## Catalog Module Endpoints

### GET /api/courses

List all available courses with optional filtering.

**Query parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| search | string | No | Filter courses by title (case-insensitive substring match) |
| category | string | No | Filter courses by exact category match |

**Response** `200 OK`:
```json
{
  "courses": [
    {
      "id": "guid",
      "title": "Introduction to C#",
      "shortDescription": "Learn the basics...",
      "category": "Programming",
      "duration": "3 hours"
    }
  ]
}
```

---

### GET /api/courses/{id}

Get detailed information about a specific course.

**Response** `200 OK`:
```json
{
  "id": "guid",
  "title": "Introduction to C#",
  "shortDescription": "Learn the basics...",
  "fullDescription": "A comprehensive introduction...",
  "category": "Programming",
  "duration": "3 hours"
}
```

**Response** `404 Not Found`: Course does not exist.

---

## Enrollment Module Endpoints

### POST /api/enrollments

Enroll the current authenticated student in a course.

**Headers**: `Authorization: Bearer <token>` (required)

**Request body**:
```json
{
  "courseId": "guid"
}
```

**Response** `201 Created`:
```json
{
  "id": "guid",
  "studentId": "guid",
  "courseId": "guid",
  "enrolledAt": "2025-07-29T12:00:00Z"
}
```

**Response** `400 Bad Request`: Invalid course ID or course does not exist.

**Response** `409 Conflict`: Student is already enrolled in this course (FR-005).

**Response** `401 Unauthorized`: Student is not authenticated.

---

### GET /api/enrollments/my

List all courses the current authenticated student is enrolled in.

**Headers**: `Authorization: Bearer <token>` (required)

**Response** `200 OK`:
```json
{
  "enrollments": [
    {
      "id": "guid",
      "courseId": "guid",
      "courseTitle": "Introduction to C#",
      "enrolledAt": "2025-07-29T12:00:00Z"
    }
  ]
}
```

**Response** `401 Unauthorized`: Student is not authenticated.

---

## Cross-Module Contract

### Catalog.Contracts

The Enrollment module validates course existence through this contract:

```csharp
namespace LearningLms.Contracts.Catalog;

public record CourseSummary(Guid Id, string Title);

public interface ICourseLookup
{
    Task<CourseSummary?> GetCourseAsync(Guid courseId);
}
```

This interface is implemented by the Catalog module's Infrastructure layer and registered in DI. The Enrollment module depends only on the interface, never on Catalog internals.
