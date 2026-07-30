# Contract: SCORM Status Display Mapping

**Date**: 2025-07-30
**Feature**: 007-course-launch-status

## Overview

This contract defines the mapping between SCORM 1.2 `cmi.core.lesson_status` values and user-facing display labels. It is consumed by the Host UI layer (Razor Pages partials and helpers) and must be consistent across all views.

## Status Mapping Function

**Signature**: `string GetDisplayLabel(string rawStatus)`

**Input**: A raw SCORM `lesson_status` string value (as stored in `CourseAttempt.Status`)

**Output**: A human-readable display label

**Mapping Table**:

| Input (raw status) | Output (display label) | Category |
|--------------------|------------------------|----------|
| `"not attempted"` | `"Not Started"` | Neutral |
| `"neutral"` | `"Not Started"` | Neutral |
| `"incomplete"` | `"In Progress"` | Active |
| `"in-progress"` | `"In Progress"` | Active (legacy) |
| `"abandoned"` | `"Abandoned"` | Warning (legacy) |
| `"completed"` | `"Completed"` | Success |
| `"passed"` | `"Passed"` | Success |
| `"failed"` | `"Failed"` | Error |
| `"browsed"` | `"Browsed"` | Neutral |
| *(any other value)* | *(input as-is)* | Unknown |

**Behavior**:
- Case-insensitive matching
- Unknown values pass through unchanged (defensive behavior for future SCORM values)

## Percentage Display Function

**Signature**: `string GetDisplayPercentage(double? scoreRaw)`

**Input**: `ScoreRaw` from `CourseAttempt` (nullable double, 0–100 range)

**Output**: A human-readable percentage string

**Mapping Table**:

| Input | Output |
|-------|--------|
| `null` | `"N/A"` |
| `0` | `"0%"` |
| `1`–`99` | `"{score}%"` |
| `100` | `"100%"` |

**Behavior**:
- Integer display only (no decimal places)
- Always includes the `%` suffix for numeric values

## Enrollment Row Display Contract

**View Model**: `EnrollmentRow` (record in Host layer)

| Field | Type | Source | Display Use |
|-------|------|--------|-------------|
| EnrollmentId | Guid | Enrollment.Id | Link to enrollment actions |
| CourseId | Guid | Enrollment.CourseId | Link to course detail / launch |
| CourseTitle | string | Catalog course title | Course name display |
| EnrolledAt | DateTimeOffset | Enrollment.EnrolledAt | Enrollment date |
| LatestStatus | string? | CourseAttempt.Status (latest attempt) | Mapped via `GetDisplayLabel()` |
| LatestScore | double? | CourseAttempt.ScoreRaw (latest attempt) | Formatted via `GetDisplayPercentage()` |

**Display Rules**:
1. If `LatestStatus` is null → show "Not Started" badge
2. If `LatestStatus` is not null → show mapped display label badge
3. If `LatestScore` is not null → show percentage alongside status badge
4. If `LatestScore` is null → do not show score

## API Endpoints (existing, no changes)

These existing endpoints serve the data consumed by the display mapping:

| Endpoint | Method | Response | Change |
|----------|--------|----------|--------|
| `GET /api/scorm/attempts/{studentId}` | GET | `AttemptSummary[]` | No change — already returns `Status` and `ScoreRaw` |
| `POST /api/scorm/{courseId}/launch` | POST | `LaunchResponse` | No change — already functional |

## UI Partial Views

| Partial | Location | Contract |
|---------|----------|----------|
| `_MyCourseRow.cshtml` | `Pages/Shared/` | Receives `EnrollmentRow`, renders status badge + percentage |
| `_EnrollmentList.cshtml` | `Pages/Shared/` | Receives `IEnumerable<EnrollmentRow>`, iterates rows |
