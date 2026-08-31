# Plan: Eliminate N+1 / Full-Keyspace Scans (E1–E8)

**Input**: [spec.md](spec.md)

## File Map

| Item | Contract (add method) | Implementation | Caller change |
|------|----------------------|----------------|---------------|
| E1 | `Enrollment.Contracts/IEnrollmentLookup.cs` → `GetEnrolledCourseIdsAsync` | Enrollment module `IEnrollmentLookup` impl (find the class; one `IN` query) | `src/Host/Pages/Courses/Index.cshtml.cs` loop → 1 call + `HashSet` |
| E2 | — | — | same file: `OnGetCourseListAsync` restructured (fetch requested page; clamp-refetch only when empty & out-of-range) |
| E3 | `Catalog.Contracts/ICourseLookup.cs` → `GetDistinctCategoriesAsync` | `Catalog/Application/CourseLookup.cs` (`SELECT DISTINCT`) | same page file: auth branch from `visible` DTOs; anon branch via contract; inject `ICourseLookup` |
| E4 | — (reuse existing `GetCoursesAsync`) | — | `Enrollment/Application/EnrollmentService.GetMyEnrollmentsAsync` |
| E5 | — (reuse existing `GetCoursesAsync`) | — | `Scorm/Application/ScormAttemptService.GetMyAttemptsAsync` |
| E6 | `Scorm.Contracts/IScormPackageService.cs` → typed `GetCourseIdsWithPackagesAsync` | `Scorm/Application/ScormPackageService.cs` (one `IN` query) | `src/Host/Pages/Admin/Courses/Index.cshtml.cs` loop → 1 call + `HashSet` |
| E7 | `Catalog.Contracts/ICourseLookup.cs` → `GetCourseCountsByOrgsAsync`; `Enrollment.Contracts/IEnrollmentAdmin.cs` → `CountEnrollmentsByOrgsAsync` | `CourseLookup`; `IEnrollmentAdmin` impl (mirror per-org join semantics) | `Management/Application/DashboardService.cs:92` loop; `Management/Application/OrganizationService.cs:202` loop |
| E8 | — (store-internal) | `Scorm/Infrastructure/ScormSessionStore.cs`: index key `scorm:active:{studentId}:{courseId}` (SET on create, EXPIRE on `SetValueAsync` via one `HashGetAsync(key,[student,course])`, delete on `DeleteSessionAsync`, GET+validate in `FindActiveSessionKeyAsync`) | — (`ScormSessionService` call sites unchanged) |

## Notes / Risks

- **E2**: confirm the `BrowseCourses` SP tolerates out-of-range pages (empty set,
  total still returned). If it errors, fall back to: keep the page-1 probe but
  reuse its `Items` when `effectivePage == 1` (skip the second fetch in the common
  filter-change case).
- **E7 enrollment counts**: `Enrollment` has no `OrganizationId` — read the existing
  `CountEnrollmentsAsync(Guid?)` implementation first and mirror its join
  (student-org vs course-org) in the bulk variant. Numbers MUST be identical.
- **E8**: `SessionData` hash field names for student/course — read
  `ToHashEntries`/`FromHashEntries` first. `DeleteSessionAsync` becomes
  read-then-delete; the only caller is LMSFinish where the session exists.
  Keep the 046 retry as the race safety net (do not touch it).
- **E6**: typed return only — no `object`/`object[]` erasure on the new method.
- Contract additions are additive → ArchitectureTests unaffected, but re-run them
  (Principle III).
- `IEnrollmentLookup` impl + `IEnrollmentAdmin` impl: locate the concrete classes
  (grep `: IEnrollmentLookup` / `: IEnrollmentAdmin`); update DI only if the
  impl class registration is explicit by type (it should already be registered).

## Verification (Principle XIII)

1. `dotnet build` (in-container) 0 errors.
2. New unit tests green: E1/E4/E5/E6/E7 counting-fake tests (1 call for N rows);
   E8 real-Valkey lifecycle test (Scorm.Tests, `localhost:6380` host /
   `valkey:6379` in-container pattern from spec 046).
3. Existing unit suites green (Scorm.Tests incl. 046 retry tests,
   ArchitectureTests 14/14, others).
4. Full Playwright suite green (170 + 1 skip baseline) — the behavior guard.
5. Post-merge: rebuild + restart + full suite again (gate 3).

## Subagent Split (one branch, two sequential runs)

- **Run A**: E1, E2, E3, E4, E5, E7 (contracts + impls + page model + services)
  + unit tests + build + targeted E2E (02, 11, 14, 16, 04, 06).
- **Run B**: E6, E8 + unit tests (incl. real-Valkey E8 test) + build + targeted
  E2E (15, 16, 04) + FULL suite.
