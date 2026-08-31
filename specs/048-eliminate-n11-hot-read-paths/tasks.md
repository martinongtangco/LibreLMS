# Tasks: Eliminate N+1 / Full-Keyspace Scans (E1–E8)

**Input**: [plan.md](plan.md), [spec.md](spec.md)

## Phase 1: Setup

- [ ] T001 Branch `story/048-eliminate-n11-hot-read-paths` from `master` (Principle VIII)

## Phase 2: Run A — contracts, page model, enrollment/attempt/dashboard (E1, E2, E3, E4, E5, E7)

- [ ] T002 E1: `IEnrollmentLookup.GetEnrolledCourseIdsAsync` + impl (one `IN` query) + page model loop → single call + HashSet
- [ ] T003 E2: `OnGetCourseListAsync` — single SP call on the common path (verify SP out-of-range behavior first)
- [ ] T004 E3: `ICourseLookup.GetDistinctCategoriesAsync` + impl; page model: auth branch from `visible` DTOs, anon branch via contract; inject `ICourseLookup`
- [ ] T005 E4: `GetMyEnrollmentsAsync` → one `GetCoursesAsync` + dictionary
- [ ] T006 E5: `GetMyAttemptsAsync` → one `GetCoursesAsync` + dictionary
- [ ] T007 E7: `ICourseLookup.GetCourseCountsByOrgsAsync` + `IEnrollmentAdmin.CountEnrollmentsByOrgsAsync` + impls (mirror per-org semantics) + DashboardService/OrganizationService loops → bulk
- [ ] T008 Unit tests: counting-fake assertions (E1, E4, E5, E7) — 1 lookup call for N rows
- [ ] T009 Build 0 errors; ArchitectureTests 14/14; existing unit suites green
- [ ] T010 Targeted E2E green (02, 04, 06, 11, 14, 16) — capture output

## Phase 3: Run B — SCORM (E6, E8)

- [ ] T011 E6: `IScormPackageService.GetCourseIdsWithPackagesAsync` (typed) + impl + admin course list → single call
- [ ] T012 E8: `scorm:active:{studentId}:{courseId}` index in `ScormSessionStore` (SET on create / EXPIRE on activity / delete on delete / GET+validate on find)
- [ ] T013 Unit tests: E6 counting fake; E8 real-Valkey lifecycle (create→find, activity→TTL refresh, delete→index gone, stale index→null + self-clean)
- [ ] T014 Build 0 errors; Scorm.Tests green (incl. 046 retry tests); ArchitectureTests green
- [ ] T015 Targeted E2E green (15, 16, 04) + FULL Playwright suite green (170 + 1 skip) — capture output

## Phase 4: Merge

- [ ] T016 Merge into `master`, post-merge rebuild + restart + full Playwright suite (Principle XIII gate 3), push, back to `master` clean (Principle XII)
