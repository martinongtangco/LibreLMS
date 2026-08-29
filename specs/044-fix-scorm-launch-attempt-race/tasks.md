# Tasks: Retry SCORM Launch on Duplicate Attempt Number

**Input**: [plan.md](plan.md), [spec.md](spec.md)

## Phase 1: Setup

- [X] T00[1-6] Create branch `bug/044-fix-scorm-launch-attempt-race` from `master` and confirm `git branch --show-current` reports it (Principle VIII)

## Phase 2: Fix

- [X] T00[1-6] In `src/Modules/Scorm/Application/ScormSessionService.cs`: (a) move the launch body (active-session check → attempt read → CourseAttempt insert → Valkey session → success result) from `LaunchAsync` into a private `TryLaunchCoreAsync`; (b) make `LaunchAsync` run the enrollment check once, then loop up to 3 attempts calling `TryLaunchCoreAsync`, catching `Microsoft.Data.SqlClient.SqlException` with `Number == 2601` — on catch: `ChangeTracker.Clear()`, `Task.Delay(50 * attempt)`, retry; on exhaustion return `LaunchResult.CreateConflict()`; (c) add the `CreateConflict()` factory to `LaunchResult`; (d) add the `using Microsoft.Data.SqlClient;` and a comment at the catch explaining the max+1 race (bug-044)

## Phase 3: Verification (Principle XIII)

- [X] T00[1-6] Rebuild + restart the app, show build output + "Now listening" + 200
- [X] T00[1-6] Run `dotnet test tests/ArchitectureTests` (Principle III — no new cross-module references)
- [X] T00[1-6] Run `npx playwright test tests/14-profile-courses.spec.ts tests/15-scorm-launch-ui.spec.ts` (both green)
- [X] T00[1-6] Run the FULL `npx playwright test` suite — twice — and capture passing output (the race is probabilistic; repeated runs are the evidence)

## Phase 4: Merge

- [ ] T007 Merge `bug/044-fix-scorm-launch-attempt-race` into `master`, then on `master` rebuild + restart + re-run the full `npx playwright test` (Principle XIII gate 3), push, and switch back to `master` clean (Principle XII)
