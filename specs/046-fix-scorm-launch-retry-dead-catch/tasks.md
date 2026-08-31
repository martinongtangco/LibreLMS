# Tasks: Fix the Dead 2601 Retry in SCORM Launch

**Input**: [plan.md](plan.md), [spec.md](spec.md)

## Phase 1: Setup

- [ ] T001 Create branch `bug/046-fix-scorm-launch-retry-dead-catch` from `master` and confirm `git branch --show-current` reports it (Principle VIII)

## Phase 2: Fix

- [ ] T002 In `src/Modules/Scorm/Application/ScormSessionService.cs`: change the retry catch to `catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))`, add the private static `IsDuplicateKeyViolation` (inner `SqlException`, `Number == 2601`), and update the comment to record the bug-046 root cause (EF Core wraps 2601 in `DbUpdateException`)
- [P] T003 `tests/Scorm.Tests/Scorm.Tests.csproj`: add Host project reference (migrations assembly) + `Microsoft.Data.SqlClient` 6.1.1
- [P] T004 `tests/Scorm.Tests/DuplicateKeyExceptionContractTests.cs`: exception-contract test — duplicate-key insert through `SaveChangesAsync` throws `DbUpdateException` with inner `SqlException` 2601; top-level is not a raw `SqlException`; self-cleaning
- [P] T005 `tests/Scorm.Tests/ConcurrentLaunchRetryTests.cs`: 16 parallel `LaunchAsync` calls (same student/course, no active session) — all complete without throwing, ≥1 success, remaining outcomes are already-active/conflict, no duplicate attempt numbers; cleanup attempts + Valkey keys

## Phase 3: Verification (Principle XIII)

- [ ] T006 Rebuild + restart the Host app, show build output + "Now listening" + 200
- [ ] T007 `dotnet test tests/Scorm.Tests` (both new tests green; env: `ConnectionStrings__Sql`, `ConnectionStrings__Valkey`)
- [ ] T008 `dotnet test tests/ArchitectureTests` (Principle III)
- [ ] T009 Isolated `npx playwright test tests/14-profile-courses.spec.ts tests/15-scorm-launch-ui.spec.ts`
- [ ] T010 FULL `npx playwright test` — capture passing output (gate 2)

## Phase 4: Merge

- [ ] T011 Merge `bug/046-fix-scorm-launch-retry-dead-catch` into `master`, then on `master` rebuild + restart + re-run the full `npx playwright test` (Principle XIII gate 3), push, and switch back to `master` clean (Principle XII)
