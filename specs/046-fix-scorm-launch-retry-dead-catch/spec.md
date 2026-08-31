# Bug Fix Specification: SCORM Launch 2601 Retry Never Fires (Dead Catch in Spec 044)

**Feature Branch**: `bug/046-fix-scorm-launch-retry-dead-catch`

**Created**: 2026-07-30

**Status**: In progress

**Input**: Workspace code review (2026-07-30) of spec 044's fix in
`src/Modules/Scorm/Application/ScormSessionService.cs` `LaunchAsync`:

```csharp
for (var attempt = 1; ; attempt++)
{
    try
    {
        return await TryLaunchCoreAsync(studentId, courseId);
    }
    catch (SqlException ex) when (ex.Number == 2601)   // ← never matches
    {
        ...
    }
}
```

Spec 044 added this loop to retry the non-atomic max+1 attempt numbering
when two concurrent launches insert the same `AttemptNumber` (SQL 2601 on
`IX_CourseAttempts_StudentId_CourseId_AttemptNumber`). The loop's catch,
however, never executes.

## Root Cause

`TryLaunchCoreAsync` raises the duplicate key through
`_scormContext.SaveChangesAsync()`. EF Core does **not** let the provider's
`SqlException` escape `SaveChangesAsync` — it wraps it in
`Microsoft.EntityFrameworkCore.DbUpdateException` (the `SqlException`
becomes `InnerException`). The spec-044 loop catches
`Microsoft.Data.SqlClient.SqlException` at the top level, which EF Core
never throws from `SaveChangesAsync`, so:

- the catch is **dead code**;
- a racing launch still escapes with an unhandled `DbUpdateException` and
  the endpoint still 500s — spec 044's original symptom is unchanged;
- no test exercised concurrent launches, so spec 044 shipped green
  (its E2E evidence ran the suite where the race did not happen to hit).

**Probe evidence** (integration probe run 2026-07-30 against the dev
MSSQL container, duplicate `(StudentId, CourseId, AttemptNumber)` insert
through `ScormDbContext.SaveChangesAsync()`):

```
PROBE top-type:    Microsoft.EntityFrameworkCore.DbUpdateException
PROBE inner-type:  Microsoft.Data.SqlClient.SqlException
PROBE sql-number:  2601
PROBE raw-SqlException-catch-would-fire: False
```

Contrast: `UserProvisioningService.CreateAsync` in the same codebase
catches `DbUpdateException` correctly for the same class of unique-index
violation — the Scorm service is the outlier.

## Fix

**Catch the exception EF Core actually throws** in
`ScormSessionService.LaunchAsync`:

- Replace `catch (SqlException ex) when (ex.Number == 2601)` with
  `catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))`, where
  `IsDuplicateKeyViolation` unwraps `InnerException` and checks
  `SqlException.Number == 2601`.
- Retry behavior unchanged: `ChangeTracker.Clear()`, 50 ms × attempt
  backoff, up to 3 attempts, then `LaunchResult.CreateConflict()`.
- One sentence: "EF Core wraps SQL 2601 in `DbUpdateException`, so the
  retry loop must catch that wrapper and inspect the inner `SqlException`."

**Regression tests** (new, in `tests/Scorm.Tests`, run against the dev
MSSQL + Valkey containers like the other integration test projects):

1. **Exception-contract test** — a duplicate-key insert through
   `SaveChangesAsync` throws `DbUpdateException` with an inner
   `SqlException` of number 2601, and the top-level exception is **not** a
   raw `SqlException` (documents the contract the fix relies on; fails
   loudly if EF Core's wrapping behavior ever changes).
2. **Concurrent-launch test** — N parallel `LaunchAsync` calls (same
   student, course, no pre-existing active session): every call completes
   without throwing; results are a mix of one-or-more successes and/or
   "session already active" outcomes; no duplicate attempt numbers exist
   afterwards (the unique index guarantees it, but the test asserts the
   observable no-500 behavior spec 044 promised).

## User Scenarios & Testing

### User Story 1 - Concurrent SCORM launches no longer 500 (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a learner with no active SCORM session for a course, **When**
   multiple launches race for the same student/course, **Then** no launch
   throws / 500s with a duplicate-key error; exactly the spec-044 outcome
   holds (one wins, the rest get a clean already-active or conflict
   result).
2. **Given** the full parallel E2E suite, **When** it runs, **Then**
   `14-profile-courses.spec.ts` and `15-scorm-launch-ui.spec.ts` pass.

**Independent Test**: the new `Scorm.Tests` concurrent-launch test
(deterministic trigger of the race) + full Playwright suite.

**Known limitation (unchanged, out of scope)**: the Valkey active-session
check remains check-then-act (spec 044's documented limitation).
