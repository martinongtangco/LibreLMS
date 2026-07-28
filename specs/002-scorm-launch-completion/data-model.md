# Data Model: SCORM Launch & Completion

**Branch**: `002-scorm-launch-completion` | **Date**: 2025-07-29

## Entities

### ScormPackage

Represents an uploaded SCORM 1.2 package. One package is linked to one Course in the Catalog module.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK, auto-generated | |
| `CourseId` | `Guid` | FK → `Course.Id`, unique | Links to the catalog course this package belongs to |
| `ManifestTitle` | `string` | Required, max 200 chars | Title extracted from `imsmanifest.xml` |
| `LaunchPath` | `string` | Required | Relative path to the launch SCO's HTML file (e.g., `index.html`) |
| `ContentDirectory` | `string` | Required | Server-relative path to extracted content files (e.g., `scorm-content/{Id}`) |
| `CreatedAt` | `DateTimeOffset` | Required, server default | When the package was uploaded |

**Relationships**:
- `ScormPackage` (0..1) → (1) `Course` — a course may or may not have a SCORM package
- `ScormPackage` (1) → (0..*) `CourseAttempt` — each attempt references a package's course

### CourseAttempt

Represents a single student's attempt at a SCORM course. Multiple attempts per student/course are allowed.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK, auto-generated | |
| `StudentId` | `Guid` | FK → `Student.Id`, indexed | The student who made this attempt |
| `CourseId` | `Guid` | FK → `Course.Id`, indexed | The course attempted |
| `AttemptNumber` | `int` | Required, default 1 | Sequential attempt number per student/course (1, 2, 3...) |
| `Status` | `string` | Required | One of: `in-progress`, `completed`, `abandoned`, `passed`, `failed` |
| `ScoreRaw` | `double?` | Nullable, 0–100 range | Raw score from `cmi.core.score.raw` (null if not set) |
| `SessionTime` | `string` | Nullable, "HH:MM:SS" format | Cumulative session time from `cmi.core.session_time` |
| `SuspendData` | `string?` | Nullable, max 64KB | Last committed `cmi.suspend_data` for resume |
| `StartedAt` | `DateTimeOffset` | Required | When the attempt session began |
| `CompletedAt` | `DateTimeOffset?` | Nullable | When the attempt was completed/finished (set on `LMSFinish`) |
| `LastCommitAt` | `DateTimeOffset` | Required | Timestamp of the last `LMSCommit` or `LMSFinish` |

**Index**: `(StudentId, CourseId, AttemptNumber)` — unique, for attempt sequencing
**Index**: `(StudentId, CourseId)` — for querying latest attempt

**State Transitions**:
```
(in-progress) ── LMSFinish(status=completed) ──→ (completed)
(in-progress) ── LMSFinish(status=incomplete) ──→ (abandoned)
(in-progress) ── timeout/crash ────────────────→ (abandoned) [implicit]
(completed) ── relaunch (retake) ──────────────→ new attempt starts as (in-progress)
```

**Validation Rules**:
- `ScoreRaw` must be between 0 and 100 (inclusive); values outside this range are rejected per FR-015
- `Status` must be a valid SCORM 1.2 lesson_status value
- `AttemptNumber` auto-increments per `(StudentId, CourseId)` pair

### ScormSession (Ephemeral — Valkey)

Live in-progress state stored in Valkey as a JSON hash. Not persisted to MSSQL until commit.

| Key Pattern | `scorm:session:{sessionId}` |
|-------------|----------------------------|
| **TTL** | 30 minutes (auto-expires on inactivity) |
| **Fields** | |
| `sessionId` | `string` — unique session identifier (Guid) |
| `studentId` | `string` — the student's GUID |
| `courseId` | `string` — the course's GUID |
| `attemptId` | `string` — the CourseAttempt GUID |
| `cmi.core.student_id` | `string` |
| `cmi.core.student_name` | `string` |
| `cmi.core.lesson_status` | `string` — defaults to "not attempted" |
| `cmi.core.credit` | `string` — defaults to "credit" |
| `cmi.core.entry` | `string` — defaults to "initial" or "resume" |
| `cmi.core.exit` | `string` — "normal", "suspend", "timeout", or "log-out" |
| `cmi.core.score.raw` | `string` — numeric string, validated 0–100 |
| `cmi.core.session_time` | `string` — "HH:MM:SS" cumulative format |
| `cmi.suspend_data` | `string` — up to 64KB |
| `startedAt` | `string` — ISO 8601 timestamp |
| `error_code` | `string` — "0" (no error) or SCORM error code |

**Lifecycle**:
1. Created on `LMSInitialize` (new Valkey hash with defaults)
2. Updated on each `LMSSetValue` (hash field set)
3. Read on each `LMSGetValue` (hash field get)
4. On `LMSCommit`/`LMSFinish`: read hash → write to `CourseAttempt` in MSSQL
5. On `LMSFinish`: delete hash from Valkey (session complete)
6. On TTL expiry: hash deleted automatically (uncommitted progress lost)

### New Contract: IEnrollmentLookup

A new interface to be added to `Enrollment.Contracts` to allow the Scorm module to validate enrollment:

```csharp
namespace LearningLms.Contracts.Enrollment;

public interface IEnrollmentLookup
{
    /// <summary>
    /// Check if a student is enrolled in a specific course.
    /// </summary>
    Task<bool> IsEnrolledAsync(Guid studentId, Guid courseId);
}
```

## Relationships Diagram

```
Catalog Module                Enrollment Module              Scorm Module
┌─────────┐                  ┌──────────┐                  ┌──────────────┐
│ Course   │◄─────────────── │Enrollment│                  │ ScormPackage  │
│ (Id)     │   CourseId FK   │          │                  │               │
└─────────┘                  │  Student │                  │  CourseId FK ─┼──→ Course
                             │  (Id)    │                  └──────────────┘
                              └────┬─────┘                      │
                                   │ StudentId FK               │ CourseId FK
                                   ▼                            ▼
                            ┌──────────┐                  ┌──────────────┐
                            │Enrollment│                  │CourseAttempt  │
                            │          │                  │               │
                            └──────────┘                  └──────────────┘
                                  │                           │
                                  └────── StudentId FK ───────┘
                                           (same student)
```

## Storage Notes

- **MSSQL**: `ScormPackage` and `CourseAttempt` tables via `ScormDbContext`
- **Valkey**: `scorm:session:{sessionId}` hashes with 30-minute TTL
- **Filesystem**: Extracted SCORM content under `wwwroot/scorm-content/{packageId}/`
