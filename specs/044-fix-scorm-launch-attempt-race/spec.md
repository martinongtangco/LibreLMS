# Bug Fix Specification: SCORM Launch 500s on Concurrent Attempts (Duplicate Attempt Number)

**Feature Branch**: `bug/044-fix-scorm-launch-attempt-race`

**Created**: 2026-08-29

**Status**: Draft

**Input**: Discovered during spec 042's Phase-7 full-suite runs (2026-08-29):
`tests/Playwright.Tests/tests/14-profile-courses.spec.ts` —
"a course with a completed attempt appears under Completed" fails
intermittently under full parallel load (2 of 3 full-suite runs; passes in
isolated runs) with a 500 from `POST /api/scorm/{courseId}/launch`:

```
Microsoft.Data.SqlClient.SqlException: Cannot insert duplicate key row in
object 'dbo.CourseAttempts' with unique index
'IX_CourseAttempts_StudentId_CourseId_AttemptNumber'.
The duplicate key value is (550e8400-..., 11111111-..., 23).
```

## Root Cause

`ScormSessionService.LaunchAsync` (`src/Modules/Scorm/Application/
ScormSessionService.cs`) numbers attempts as **max+1 with a non-atomic
read-then-insert**:

```csharp
var lastAttempt = await _scormContext.CourseAttempts
    .Where(a => a.StudentId == studentId && a.CourseId == courseId)
    .OrderByDescending(a => a.AttemptNumber).FirstOrDefaultAsync();
var attemptNumber = (lastAttempt?.AttemptNumber ?? 0) + 1;
// ... later ...
_scormContext.CourseAttempts.Add(attempt);
await _scormContext.SaveChangesAsync();
```

The preceding "active session" check (Valkey) is also check-then-act, so two
concurrent launches of the **same student/course** both pass it, both read
the same max, and both `INSERT` the same `AttemptNumber` — the unique index
rejects the second with SQL error 2601 and the launch 500s.

Trigger: the E2E suite runs `fullyParallel`, and
`14-profile-courses.spec.ts` and `15-scorm-launch-ui.spec.ts` both drive
SCORM launches for the same seeded learner and course. When their launch
requests interleave (timing window), one of them hits the duplicate key.
Pre-existing defect (the service and both specs predate spec 042; spec 042's
branch touches neither) — exposed by parallel load, not caused by it.

## Fix

**Bounded retry on the unique violation** in `LaunchAsync` — the standard
pattern for max+1 numbering:

- Extract the current launch body (session check → attempt read → insert →
  Valkey session → result) into a private `LaunchCoreAsync`.
- `LaunchAsync` keeps the enrollment check (stable across retries) and
  loops: on `SqlException` with `Number == 2601`, clear the failed entity
  from the change tracker, back off briefly (50 ms × attempt), and re-run
  `LaunchCoreAsync` so the max is re-read. Up to 3 attempts total.
- If the conflict persists after 3 attempts, return a clean
  `LaunchResult.CreateConflict()` (`Success=false`) instead of throwing —
  the endpoint already maps `!Success` to a 400 JSON error.

Why retry rather than a serializable transaction: no new locking/isolation
semantics to reason about, no deadlock surface added, and the retry re-read
is exactly what the numbering needs. One sentence: "concurrent launches can
pick the same attempt number, so a rejected insert is retried with a fresh
read."

**Known limitation (out of scope)**: the Valkey active-session check remains
check-then-act; two *truly simultaneous* launches can still both create
sessions (the loser now gets a fresh attempt number instead of a 500).
Making session creation atomic would require a Valkey-level primitive
(SET-NX) in `IScormSessionStore` — a separate change if it ever matters.

## User Scenarios & Testing

### User Story 1 - SCORM launch succeeds under concurrent launches (Priority: P1)

**Acceptance Scenarios**:

1. **Given** a learner with an existing SCORM attempt history, **When**
   launches are triggered in rapid succession for the same course, **Then**
   no launch 500s with a duplicate-key error; attempts are numbered
   consecutively without gaps in the common case.
2. **Given** the full parallel E2E suite, **When** it runs, **Then**
   `14-profile-courses.spec.ts` and `15-scorm-launch-ui.spec.ts` pass
   (verified across repeated full runs).

**Independent Test**: repeated `npx playwright test` full-suite runs with
the SCORM specs green; plus the isolated SCORM spec runs.
