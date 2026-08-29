# Plan: Retry SCORM Launch on Duplicate Attempt Number

**Input**: [spec.md](spec.md)

## Summary

Bounded retry (3 attempts, 50 ms × n backoff) around the attempt read/insert
in `ScormSessionService.LaunchAsync`, catching SQL 2601 (duplicate key on
`IX_CourseAttempts_StudentId_CourseId_AttemptNumber`) and re-reading the max
on each retry.

## Technical Approach

- **File**: `src/Modules/Scorm/Application/ScormSessionService.cs` only.
  - Enrollment validation stays in `LaunchAsync` (runs once).
  - Current body (session check → attempt read → insert → Valkey session →
    `LaunchResult.CreateSuccess`) moves to private `TryLaunchCoreAsync`.
  - `LaunchAsync` loops up to 3 attempts, catching
    `Microsoft.Data.SqlClient.SqlException` with `Number == 2601`; on catch:
    `_scormContext.ChangeTracker.Clear()` (drop the failed Added entity),
    `await Task.Delay(50 * attempt)`, continue. Exhausted →
    `LaunchResult.CreateConflict()`.
  - Add `LaunchResult.CreateConflict()` factory:
    "A momentary conflict occurred while launching (the course may be
    opening in another tab). Please try again."
- **No changes** to `IScormSessionStore`, Valkey, endpoints, or the unique
  index.
- **Usings**: add `Microsoft.Data.SqlClient` for the `SqlException` type
  (transitively available via EF Core SqlServer).
- **ArchitectureTests** (Principle III) unaffected — no new references.

## Verification (Principle XIII)

1. Rebuild (`rm -rf src/Host/obj src/Host/bin && dotnet build src/Host`) +
   restart; show build output + "Now listening" + 200.
2. `dotnet test tests/ArchitectureTests` (module boundaries intact).
3. Run `14-profile-courses.spec.ts` + `15-scorm-launch-ui.spec.ts`
   isolated (both green).
4. FULL `npx playwright test` — ideally twice — SCORM specs green (the race
   is probabilistic; repeated full runs are the evidence).
