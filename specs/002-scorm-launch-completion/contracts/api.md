# API Contracts: SCORM Launch & Completion

**Branch**: `002-scorm-launch-completion` | **Date**: 2025-07-29

## SCORM Module Endpoints

All endpoints are under the `/api/scorm` group.

### POST /api/scorm/upload

Upload a SCORM 1.2 package (ZIP file).

**Authorization**: Admin role required

**Request**:
- `Content-Type`: `multipart/form-data`
- Field: `package` — ZIP file containing `imsmanifest.xml`

**Responses**:
- `201 Created` — Package uploaded, course created/linked
  ```json
  {
    "packageId": "guid",
    "courseId": "guid",
    "title": "Course Title from Manifest",
    "launchPath": "index.html"
  }
  ```
- `400 Bad Request` — Invalid package
  ```json
  { "error": "Missing imsmanifest.xml in the uploaded package" }
  ```
- `401 Unauthorized` — Not authenticated
- `403 Forbidden` — Not an admin

---

### POST /api/scorm/{courseId}/launch

Launch a SCORM course session for the current student.

**Authorization**: Authenticated student required

**Path Parameters**:
- `courseId` (Guid) — The course to launch

**Responses**:
- `200 OK` — Session initialized, returns session info
  ```json
  {
    "sessionId": "guid",
    "contentUrl": "/scorm-content/{packageId}/index.html",
    "apiUrl": "/api/scorm/session/{sessionId}/api.js",
    "entry": "initial"
  }
  ```
- `400 Bad Request` — Course is not a SCORM course or session already active
  ```json
  { "error": "A session for this course is already active. Please close it before launching again." }
  ```
- `401 Unauthorized` — Not authenticated
- `403 Forbidden` — Student not enrolled in this course
- `404 Not Found` — Course not found

---

### GET /api/scorm/session/{sessionId}/api.js

Serves the SCORM API JavaScript shim. Included by the SCORM wrapper page.

**Authorization**: None (served as a script resource)

**Response**:
- `200 OK` — JavaScript text containing the `window.API` object with methods:
  - `LMSInitialize()` — returns `true`
  - `LMSFinish()` — calls server to end session, returns `true`
  - `LMSGetValue(element)` — returns string value or `""` on error
  - `LMSSetValue(element, value)` — returns `true` on success, `false` on error
  - `LMSCommit()` — calls server to commit session state, returns `true`

---

### POST /api/scorm/session/{sessionId}/setValue

Set a CMI value in the current session.

**Authorization**: None (validated by sessionId)

**Path Parameters**:
- `sessionId` (Guid) — Active session identifier

**Request Body**:
```json
{
  "element": "cmi.core.lesson_status",
  "value": "completed"
}
```

**Responses**:
- `200 OK` — Value set
  ```json
  { "success": true }
  ```
- `400 Bad Request` — Invalid element or value (e.g., score out of range)
  ```json
  { "success": false, "errorCode": "403", "errorMsg": "The value specified for cmi.core.score.raw is out of range." }
  ```
- `404 Not Found` — Session not found or expired

---

### GET /api/scorm/session/{sessionId}/getValue

Get a CMI value from the current session.

**Authorization**: None (validated by sessionId)

**Path Parameters**:
- `sessionId` (Guid) — Active session identifier

**Query Parameters**:
- `element` (string) — CMI element name (e.g., `cmi.core.lesson_status`)

**Responses**:
- `200 OK` — Value retrieved
  ```json
  { "value": "completed" }
  ```
- `404 Not Found` — Session not found or expired

---

### POST /api/scorm/session/{sessionId}/commit

Commit the current session state to durable storage.

**Authorization**: None (validated by sessionId)

**Path Parameters**:
- `sessionId` (Guid) — Active session identifier

**Responses**:
- `200 OK` — Session committed
  ```json
  { "success": true, "committedAt": "2025-07-29T..." }
  ```
- `404 Not Found` — Session not found or expired

---

### POST /api/scorm/session/{sessionId}/finish

End the SCORM session permanently (commit + cleanup).

**Authorization**: None (validated by sessionId)

**Path Parameters**:
- `sessionId` (Guid) — Active session identifier

**Request Body** (optional):
```json
{
  "exit": "normal"
}
```

**Responses**:
- `200 OK` — Session finished
  ```json
  { "success": true, "status": "completed", "score": 85 }
  ```
- `404 Not Found` — Session not found or expired

---

### GET /api/scorm/attempts/my

List the current student's course attempts with course titles.

**Authorization**: Authenticated student required

**Responses**:
- `200 OK` — List of attempts
  ```json
  {
    "attempts": [
      {
        "id": "guid",
        "courseId": "guid",
        "courseTitle": "Introduction to SCORM",
        "attemptNumber": 1,
        "status": "completed",
        "scoreRaw": 85,
        "sessionTime": "00:15:30",
        "completedAt": "2025-07-29T..."
      }
    ]
  }
  ```

---

## Cross-Module Contract: IEnrollmentLookup

**Location**: `Enrollment.Contracts` (new interface)

```csharp
namespace LearningLms.Contracts.Enrollment;

public interface IEnrollmentLookup
{
    /// <summary>Check if a student is enrolled in a specific course.</summary>
    Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId);
}
```

**Implementation**: `Enrollment.Module` — queries `EnrollmentDbContext.Enrollments` for matching `(StudentId, CourseId)`.

---

## SCORM Wrapper Page (Razor Pages)

### GET /scorm/launch/{courseId}

The Razor Pages wrapper that serves SCORM content. Injects the API script and `beforeunload` handler.

**Authorization**: Authenticated student required

**Flow**:
1. Validates enrollment and checks for active session
2. Calls `POST /api/scorm/{courseId}/launch` to initialize session
3. Renders the SCORM content's HTML with the API script injected
4. Includes `beforeunload` handler for auto-commit on tab close

**Layout**: Minimal layout (no nav/sidebar) — the SCORM content fills the viewport.
