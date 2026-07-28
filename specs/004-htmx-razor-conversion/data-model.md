# Data Model: HTMX + Razor Modern UI

**Feature**: 004-htmx-razor-conversion
**Date**: 2025-07-28

## Overview

This feature introduces **no new database entities**. HTMX operates exclusively at the presentation layer. All data flows through existing module services and their existing domain models.

This document describes the **presentation-layer view models** (Razor partial view inputs) and the **existing domain entities** they render.

---

## Existing Domain Entities (Read-Only from This Feature)

### Course (Catalog.Module.Domain)

| Field | Type | Description |
|-------|------|-------------|
| Id | `Guid` | Unique identifier |
| Title | `string` | Course display name |
| ShortDescription | `string` | Brief summary for listing views |
| FullDescription | `string` | Complete description for detail view |
| Category | `string` | Classification (e.g., "Technical", "Management") |
| Duration | `string` | Estimated completion time |

**Relationships**: One-to-many with Enrollment; zero-to-one with ScormPackage

---

### Enrollment (Enrollment.Module.Domain)

| Field | Type | Description |
|-------|------|-------------|
| Id | `Guid` | Unique identifier |
| StudentId | `Guid` | Reference to Student |
| CourseId | `Guid` | Reference to Course |
| EnrolledAt | `DateTimeOffset` | Enrollment timestamp |

**Relationships**: Many-to-one with Student, Many-to-one with Course

---

### CourseAttempt (Scorm.Module.Domain)

| Field | Type | Description |
|-------|------|-------------|
| Id | `Guid` | Unique identifier |
| StudentId | `Guid` | Reference to Student |
| CourseId | `Guid` | Reference to Course |
| AttemptNumber | `int` | Sequential attempt counter |
| Status | `string` | "in-progress", "completed", "passed", "failed" |
| ScoreRaw | `double?` | Raw score (if scored) |
| SessionTime | `string?` | SCORM session time format |
| StartedAt | `DateTimeOffset` | Attempt start time |
| CompletedAt | `DateTimeOffset?` | Attempt end time (null if in progress) |
| LastCommitAt | `DateTimeOffset` | Last data commit time |

---

## Presentation View Models (New)

These are `record` types used as models for Razor partial views. They live in the Host project's page code-behind files and do not cross module boundaries.

### CourseItem (for `_CourseCard.cshtml`)

| Field | Type | Source |
|-------|------|--------|
| Id | `Guid` | Course.Id |
| Title | `string` | Course.Title |
| ShortDescription | `string` | Course.ShortDescription |
| Category | `string` | Course.Category |
| Duration | `string` | Course.Duration |
| IsEnrolled | `bool` | Computed from Enrollment lookup |

---

### CourseDetailItem (for course detail partial)

| Field | Type | Source |
|-------|------|--------|
| Id | `Guid` | Course.Id |
| Title | `string` | Course.Title |
| ShortDescription | `string` | Course.ShortDescription |
| FullDescription | `string` | Course.FullDescription |
| Category | `string` | Course.Category |
| Duration | `string` | Course.Duration |
| IsEnrolled | `bool` | Computed from Enrollment lookup |
| IsScorm | `bool` | Has associated ScormPackage |
| ScormPackageId | `Guid?` | ScormPackage.Id (if SCORM) |

---

### EnrollmentRow (for `_MyCourseRow.cshtml`)

| Field | Type | Source |
|-------|------|--------|
| EnrollmentId | `Guid` | Enrollment.Id |
| CourseId | `Guid` | Enrollment.CourseId |
| CourseTitle | `string` | Course.Title (joined) |
| EnrolledAt | `DateTimeOffset` | Enrollment.EnrolledAt |
| LatestStatus | `string?` | Latest CourseAttempt.Status (null if no attempts) |
| LatestScore | `double?` | Latest CourseAttempt.ScoreRaw |

---

### EnrollmentResult (for `_EnrollmentResult.cshtml`)

| Field | Type | Description |
|-------|------|-------------|
| Success | `bool` | Whether enrollment succeeded |
| Message | `string` | User-facing message |
| MessageType | `string` | "success", "warning", or "error" for CSS styling |
| CourseId | `Guid` | The course that was acted upon |
| IsScorm | `bool?` | Whether the course has SCORM (for showing launch button) |

---

## State Transitions

The only state change this feature triggers is enrollment creation. The HTMX layer does not manage state — it triggers the existing EnrollmentService.EnrollAsync() and renders the result.

```
Not Enrolled --[POST enrollment]--> Enrolled (badge shown, launch option appears)
Enrolled     --[POST enrollment]--> Already Enrolled (warning message shown)
```

No other state transitions are introduced. SCORM status updates occur through the existing SCORM launch flow (not through HTMX).
