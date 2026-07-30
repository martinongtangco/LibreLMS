# Research: Course Launch & Status Tracking

**Date**: 2025-07-30
**Feature**: 007-course-launch-status

## Research Tasks Resolved

### 1. SCORM 1.2 lesson_status values and display mapping

**Decision**: Map all 6 user-facing SCORM 1.2 `cmi.core.lesson_status` values to human-readable labels:
- `"not attempted"` → "Not Started"
- `"incomplete"` → "In Progress"
- `"completed"` → "Completed"
- `"passed"` → "Passed"
- `"failed"` → "Failed"
- `"browsed"` → "Browsed"

**Rationale**: SCORM 1.2 specification defines these as the standard lesson_status values. The 7th value `"neutral"` is a transitional state that SCORM content rarely uses and has no meaningful user-facing meaning — it will be treated as "Not Started" for display purposes.

**Alternatives considered**:
- Using raw SCORM values directly in the UI — rejected because they are not user-friendly (e.g., "not attempted" is jargon)
- Adding a separate enum type — rejected per Constitution Principle II (no unnecessary abstractions). A switch expression is sufficient.

### 2. Current CourseAttempt.Status values vs SCORM standard values

**Decision**: The `CourseAttempt.Status` field currently stores a mix of custom values (`"in-progress"`, `"abandoned"`) and SCORM values (`"completed"`, `"passed"`, `"failed"`). The `ScormSessionService.CommitAsync` and `FinishAsync` methods write the raw SCORM `CmiLessonStatus` directly into `CourseAttempt.Status`, which means the stored value could be any valid SCORM value including `"not attempted"`, `"incomplete"`, `"browsed"`.

**Current gap**: The `_MyCourseRow.cshtml` partial uses a C# `switch` expression on the raw status string but only handles `"completed"`, `"passed"`, `"in-progress"`, and a fallback. It does NOT handle `"incomplete"`, `"failed"`, `"browsed"`, or `"not attempted"` — these fall through to the generic fallback which displays the raw SCORM value.

**Resolution**: Add a centralized `ScormHelpers.GetDisplayLabel(string rawStatus)` method that maps ALL known SCORM values plus the legacy custom values. Replace the inline switch in `_MyCourseRow.cshtml` with a call to this helper.

**Alternatives considered**:
- Normalizing Status on write (in `ScormSessionService`) — rejected because it would lose the original SCORM value and make debugging harder. Better to store the raw value and map on display.
- Adding a new enum to the Domain — rejected per Constitution Principle II.

### 3. Percentage completion from score.raw

**Decision**: Use `cmi.core.score.raw` (0–100 range) for percentage display. Derive from `score.scaled` (0.00–1.00) only as fallback (multiply by 100, round to integer).

**Current gap**: The `CourseAttempt.ScoreRaw` field already exists and is populated from `CmiScoreRaw`. However, `ScormSessionService.CommitAsync` and `FinishAsync` both have a guard `if (score > 0)` that skips saving score when it is exactly 0. This means a student who legitimately scored 0 will have `ScoreRaw = null`, which is incorrect.

**Resolution**:
1. Change `score > 0` to `score >= 0` in both `CommitAsync` and `FinishAsync` to save score=0
2. Display logic: if `ScoreRaw` is null → show "N/A", otherwise show `ScoreRaw` as percentage
3. The `AttemptSummary` DTO already includes `ScoreRaw` — no changes needed to the service layer

**Alternatives considered**:
- Storing `score.scaled` separately — rejected. SCORM 1.2 defines `score.raw` as the primary field; `score.scaled` is a derived value. No need to store both.
- Computing percentage from session time or objective completion — rejected. This would require SCORM 2004 objective tracking, which is out of scope.

### 4. Consistency across views

**Decision**: The status and percentage display must be consistent across:
- `MyCourses/Index.cshtml` → `_EnrollmentList.cshtml` → `_MyCourseRow.cshtml`
- `Courses/Detail.cshtml`

**Current state**: 
- `MyCourses/Index.cshtml.cs` builds `EnrollmentRow` with `LatestStatus` (raw string) and `LatestScore` (double?). This is passed to `_MyCourseRow.cshtml`.
- `Courses/Detail.cshtml.cs` does NOT currently display enrollment status or percentage — it only shows "Enroll" or "Enrolled".

**Resolution**:
1. Add status + percentage to the course detail page for enrolled students
2. The `EnrollmentRow` record already carries `LatestStatus` and `LatestScore` — no structural changes needed
3. Add a `DisplayStatus` and `DisplayPercentage` computed property (or use the helper in the view)

**Alternatives considered**:
- Adding a shared view component — rejected. The existing partial view pattern is sufficient and simpler.
- Computing display values in the backend — rejected. This is presentation logic that belongs in the UI layer.

### 5. Score validation (0–100 range)

**Decision**: `ScormSessionService.SetValueAsync` already validates `cmi.core.score.raw` to be within 0–100. No changes needed. Out-of-range values return SCORM error code "403".

**Verification**: Confirmed in code — the guard `score < 0 || score > 100` is present.

## Summary of Findings

| Area | Finding | Action |
|------|---------|--------|
| Status mapping | `_MyCourseRow.cshtml` only handles 3 of 6 SCORM values | Add `ScormHelpers.GetDisplayLabel()` and update partial |
| Score=0 bug | `CommitAsync`/`FinishAsync` skip saving score when exactly 0 | Change `score > 0` to `score >= 0` |
| Course detail page | No status/percentage display for enrolled students | Add status + percentage section to detail view |
| Percentage display | No percentage shown currently (only raw score number) | Format as percentage with % suffix |
| ScoreRaw null handling | Shows raw score or nothing, no "N/A" fallback | Add N/A display when ScoreRaw is null |
