# Tasks: Enforce Hide-Course Visibility in the Learner Catalog

**Input**: [plan.md](plan.md), [spec.md](spec.md)

## Phase 1: Setup

- [ ] T001 Create branch `bug/047-hide-course-visibility-not-enforced` from `master` and confirm `git branch --show-current` reports it (Principle VIII)

## Phase 2: Fix

- [ ] T002 In `src/Host/Pages/Courses/Index.cshtml.cs`: filter `!v.IsHidden` in `GetPagedCourses` (visibleCourseIds) and `GetCategoriesAsync` (category dropdown), with a short comment citing spec 009 scenario 5 / bug-047
- [ ] T003 New `tests/Playwright.Tests/tests/19-course-visibility.spec.ts` per plan: child org + verified learner per run, hide → learner catalog drops the course, unhide → back, cleanup in finally

## Phase 3: Verification (Principle XIII)

- [ ] T004 Rebuild in devcontainer + restart app in-container (Development, LearningLms, Valkey), show build output + "Now listening" + 200
- [ ] T005 `dotnet test tests/ArchitectureTests` (Principle III)
- [ ] T006 RED check: run `19-course-visibility.spec.ts` against the pre-fix code (temp revert), confirm it fails, restore the fix
- [ ] T007 Isolated `19-course-visibility.spec.ts` green on the fix
- [ ] T008 FULL Playwright suite green — capture output (gate 2)

## Phase 4: Merge

- [ ] T009 Merge into `master`, then on `master` rebuild + restart + re-run the full Playwright suite (Principle XIII gate 3), push, and switch back to `master` clean (Principle XII)
