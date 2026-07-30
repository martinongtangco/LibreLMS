# Data Model: Course Launch & Status Tracking

**Date**: 2025-07-30
**Feature**: 007-course-launch-status

## Existing Entities (no schema changes needed)

### CourseAttempt (Scorm module, ScormDbContext)

Already exists. Stores a student's attempt at a SCORM course.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Id | Guid | No | Primary key |
| StudentId | Guid | No | FK to Enrollment.Student |
| CourseId | Guid | No | FK to Catalog.Course |
| AttemptNumber | int | No | Sequential attempt number per student/course (1, 2, 3...) |
| Status | string | No | SCORM lesson_status value. Valid values: "not attempted", "incomplete", "completed", "passed", "failed", "browsed", "neutral". Also stores legacy custom values: "in-progress", "abandoned". |
| ScoreRaw | double? | Yes | Raw score from cmi.core.score.raw (0–100). Null if not set. |
| SessionTime | string? | Yes | Cumulative session time in "HH:MM:SS" format |
| SuspendData | string? | Yes | Last committed cmi.suspend_data for resume (up to 64KB) |
| StartedAt | DateTimeOffset | No | When the attempt session began |
| CompletedAt | DateTimeOffset? | Yes | When the attempt was completed/finished (set on LMSFinish) |
| LastCommitAt | DateTimeOffset | No | Timestamp of the last LMSCommit or LMSFinish |

**Validation rules**:
- `Status` accepts any string from SCORM 1.2 lesson_status vocabulary. No database-level constraint needed.
- `ScoreRaw` must be 0–100 (validated at the application layer in `ScormSessionService.SetValueAsync`).
- `AttemptNumber` is auto-incremented per student/course pair in `ScormSessionService.LaunchAsync`.

**State transitions** (SCORM 1.2 standard):
```
not attempted → incomplete → {completed, passed, failed, browsed}
                              ↕ (retake possible from any terminal state)
in-progress   → incomplete → {completed, passed, failed, browsed}
```
The system does NOT enforce transitions — it records what the SCORM content reports.

### Enrollment (Enrollment module, EnrollmentDbContext)

Already exists. No changes needed for this feature.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Id | Guid | No | Primary key |
| StudentId | Guid | No | FK to Student |
| CourseId | Guid | No | FK to Course |
| EnrolledAt | DateTimeOffset | No | When the enrollment was created |

### SessionData (Valkey, ephemeral)

Already exists. Holds the live CMI bag during an active session. No changes needed.

| Field | Description |
|-------|-------------|
| SessionId | Unique session identifier |
| StudentId | Student making the attempt |
| CourseId | Course being attempted |
| AttemptId | FK to CourseAttempt |
| CmiLessonStatus | Current cmi.core.lesson_status value |
| CmiScoreRaw | Current cmi.core.score.raw value (string) |
| CmiSessionTime | Accumulated session time |
| CmiSuspendData | Bookmark/resume data |

## Display Mapping (not a database entity)

### SCORM Status → Display Label

| Raw SCORM Value | Display Label | CSS Color Hint |
|----------------|---------------|----------------|
| "not attempted" | Not Started | Gray (#f5f5f5 / #666) |
| "neutral" | Not Started | Gray (#f5f5f5 / #666) |
| "incomplete" | In Progress | Orange (#fff3e0 / #e65100) |
| "in-progress" | In Progress | Orange (#fff3e0 / #e65100) |
| "abandoned" | Abandoned | Red (#ffebee / #c62828) |
| "completed" | Completed | Green (#e8f5e9 / #2e7d32) |
| "passed" | Passed | Green (#e8f5e9 / #2e7d32) |
| "failed" | Failed | Red (#ffebee / #c62828) |
| "browsed" | Browsed | Gray (#f5f5f5 / #666) |

### Percentage Completion Display

| Condition | Display |
|-----------|---------|
| ScoreRaw is null | "N/A" |
| ScoreRaw is 0 | "0%" |
| ScoreRaw is 1–99 | "{ScoreRaw}%" |
| ScoreRaw is 100 | "100%" |

## No Schema Changes Required

All entities already exist with sufficient fields. The feature work is in the display/mapping layer, not the database layer.
