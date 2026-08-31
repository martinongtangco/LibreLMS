# Plan: Fix the Dead 2601 Retry in SCORM Launch

**Input**: [spec.md](spec.md)

## Summary

Change the spec-044 retry loop in `ScormSessionService.LaunchAsync` to
catch `DbUpdateException` (what EF Core actually throws) and inspect the
inner `SqlException` for number 2601. Add two integration regression
tests to `tests/Scorm.Tests`.

## Technical Approach

- **`src/Modules/Scorm/Application/ScormSessionService.cs`** (the only
  production file touched):
  - `catch (SqlException ex) when (ex.Number == 2601)` →
    `catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))`.
  - New private static `IsDuplicateKeyViolation(DbUpdateException)`:
    `ex.InnerException is SqlException sql && sql.Number == 2601`.
  - Retry body (`ChangeTracker.Clear()`, `Task.Delay(50 * attempt)`, max
    3 attempts, `CreateConflict()` on exhaustion) unchanged.
  - Update the comment block to record WHY the wrapper is caught
    (bug-044 fix + bug-046 root cause) so the next reader doesn't
    "simplify" it back to a raw `SqlException` catch.
  - Usings: keep `Microsoft.Data.SqlClient` (still needed for the
    `SqlException` inner check); `Microsoft.EntityFrameworkCore` already
    imported.
- **`tests/Scorm.Tests/Scorm.Tests.csproj`**: add the Host project
  reference (migrations assembly — same pattern as Catalog.Tests) and
  `Microsoft.Data.SqlClient` 6.1.1.
- **`tests/Scorm.Tests/DuplicateKeyExceptionContractTests.cs`**:
  exception-contract test (the probe, promoted to a permanent regression
  test). Marker-scoped random GUIDs, self-cleaning.
- **`tests/Scorm.Tests/ConcurrentLaunchRetryTests.cs`**: concurrent-launch
  test — real `ScormSessionService` + real `ScormSessionStore` (Valkey at
  `ConnectionStrings__Valkey`, default `localhost:6380`) + real
  `ScormPackageService` (temp wwwroot) + a test-local `IEnrollmentLookup`
  that reports enrolled for the marker student/course. N=16 parallel
  `LaunchAsync` calls; assert all complete, no exceptions, ≥1 success,
  no duplicate attempt numbers; cleanup of attempts + Valkey keys.
- **No changes** to Valkey schema, endpoints, unique index, or module
  boundaries — **ArchitectureTests** (Principle III) unaffected.

## Verification (Principle XIII)

1. Rebuild (`dotnet build LibreLms.slnx`) + restart the Host; show build
   output + "Now listening" + 200.
2. `dotnet test tests/Scorm.Tests` — both new tests green (needs
   `ConnectionStrings__Sql` + `ConnectionStrings__Valkey` env vars).
3. `dotnet test tests/ArchitectureTests` (module boundaries intact).
4. Isolated `npx playwright test tests/14-profile-courses.spec.ts
   tests/15-scorm-launch-ui.spec.ts` — green.
5. FULL `npx playwright test` — green (gate 2); re-run after merge
   (gate 3).
