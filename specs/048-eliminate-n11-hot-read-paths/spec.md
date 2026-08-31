# Story Specification: Eliminate N+1 and Full-Keyspace Scans on Hot Read Paths

**Story Branch**: `story/048-eliminate-n11-hot-read-paths`

**Created**: 2026-07-30

**Status**: Complete (merged 2026-07-30; post-merge gate 3: full suite 170/170, 1 documented verify-email skip — one first-pass gate-3 run hit the pre-existing 16-admin-pagination parallel-isolation flake (backlog B8), re-run green)

**Input**: Workspace code review (2026-07-30), items E1–E8. Every hot read path in the
app fans out to one query per row (or one KEYS scan per SCORM launch). At dev
scale this is invisible; at real scale each of these is O(page size) or O(total
sessions) database/Valkey round trips. This is a pure efficiency refactor:
**behavior must not change** — the existing Playwright suite is the guard.

## The Eight Sites & Fixes

### E1 — Browse page: per-course enrollment check (`Courses/Index.cshtml.cs:127`)
`foreach (var item in browseResult.Items) if (await _enrollmentLookup.IsEnrolledAsync(studentId, item.Id))`
— up to PageSize (12) queries per page load.
**Fix**: add `Task<IReadOnlyCollection<Guid>> GetEnrolledCourseIdsAsync(Guid studentId, IEnumerable<Guid> courseIds)`
to `IEnrollmentLookup` (Enrollment.Contracts) + implement in the Enrollment module as one
`WHERE StudentId = @s AND CourseId IN @ids` query (hits the existing unique
`(StudentId, CourseId)` index). Page model: one call, `HashSet` membership test.

### E2 — HTMX handler: double stored-procedure call (`OnGetCourseListAsync`)
Fetches `BrowseAsync(search, category, 1, PageSize)` **only to read TotalCount**,
then `GetPagedCourses` calls the SP again for the requested page.
**Fix**: fetch the requested page once; if it comes back empty and the page is
beyond the last one (computable from TotalCount), re-fetch at the clamped page.
Common (in-range) case: 1 SP call instead of 2. Out-of-range case: 2 calls,
same as today. (OFFSET/FETCH returns an empty set for out-of-range pages — verify
the SP does not error on it.)

### E3 — Category dropdown: full-table load (`GetCategoriesAsync`, lines ~142–164)
Both branches call `_catalogService.ListAsync()` (every course, all columns) just
to collect distinct category names.
**Fix**: authenticated branch derives categories from the already-fetched
`visible` DTOs (`CourseVisibilityDto.Category` exists) — **zero extra queries**.
Anonymous branch: new `ICourseLookup.GetDistinctCategoriesAsync()` (one
`SELECT DISTINCT Category`) — inject `ICourseLookup` into the page model.

### E4 — My Courses: per-enrollment course lookup (`EnrollmentService.GetMyEnrollmentsAsync`)
`GetCourseAsync(enrollment.CourseId)` per row.
**Fix**: `ICourseLookup.GetCoursesAsync(IEnumerable<Guid>)` **already exists**
(single `WHERE Id IN` query) — call it once, build a `Dictionary<Guid, CourseSummary>`,
keep the `?? "Unknown Course"` fallback. No contract change.

### E5 — My attempts: per-attempt course lookup (`ScormAttemptService.GetMyAttemptsAsync`)
Same pattern, same fix: one `GetCoursesAsync` call, dictionary enrichment.

### E6 — Admin course list: per-row SCORM package check (`Admin/Courses/Index.cshtml.cs:130`)
`GetPackageByCourseIdAsync(item.Id)` per row (up to 100/page).
**Fix**: add typed `Task<IReadOnlyCollection<Guid>> GetCourseIdsWithPackagesAsync(IEnumerable<Guid> courseIds)`
to `IScormPackageService` (Scorm.Contracts — **no type erasure needed**: it returns
GUIDs, not Scorm entities) + implement as one `WHERE CourseId IN` query in
`ScormPackageService`. Admin page: one call → `HashSet` → per-row boolean.
(Do NOT add an `object[]`-erased bulk method — that would perpetuate the E10 smell.)

### E7 — Dashboard + org tree: per-org counts
- `DashboardService.cs:92`: `foreach (id in descendantIds) courseCount += await courseLookup.CountByOrgAsync(id)`
  and same for `enrollmentAdmin.CountEnrollmentsAsync(id)`.
- `OrganizationService.cs:202`: `foreach (orgId in orgIds) courseCounts[orgId] = await courseLookup.CountByOrgAsync(orgId)`.
**Fix**:
- `ICourseLookup.GetCourseCountsByOrgsAsync(IEnumerable<Guid>) → Task<IReadOnlyDictionary<Guid,int>>`
  (one `GROUP BY OrganizationId WHERE OrganizationId IN @ids`) — used by both callers
  (dashboard sums the values; org tree fills its dictionary).
- `IEnrollmentAdmin.CountEnrollmentsByOrgsAsync(IEnumerable<Guid>) → Task<int>`
  (one query; must equal the sum of the per-org calls — subagent confirms the
  per-org implementation's join semantics, e.g. student's org vs course's org,
  and mirrors them).

### E8 — SCORM launch: full-keyspace scan (`ScormSessionStore.FindActiveSessionKeyAsync`)
`server.Keys("scorm:session:*")` (blocking KEYS) + `HGETALL` **per key**, on every
launch. Cost is O(all live sessions), not O(1).
**Fix** — secondary index key `scorm:active:{studentId}:{courseId}` → sessionId (string):
- `CreateSessionAsync`: pipeline hash-write + `SET scorm:active:{sid}:{cid} {sessionId} EX 1800`
  (same TTL as the session).
- `SetValueAsync` (commit path — the service hot path has no student/course in
  scope, so the store refreshes it itself): after the hash write, `HashGetAsync(key, [studentField, courseField])`
  (single round trip, both fields) → `KeyExpireAsync(indexKey, DefaultTtl)`. Skip
  silently if either field is missing. This keeps the index TTL in step with the
  hash TTL (which *is* refreshed on activity) — without it, an actively-committing
  session older than 30 min would lose its index and a relaunch would start a
  second live attempt, a behavior regression vs. today's scan.
- `DeleteSessionAsync`: `ReadSessionAsync` first (null → nothing to do) → delete
  index key + session key.
- `FindActiveSessionKeyAsync`: `GET` the index → null → return null. Otherwise
  `ReadSessionAsync(sid)` to validate the hash still exists (stale-index guard:
  delete the stale index, return null) → return sessionId. O(1): 1–2 round trips
  regardless of session count.
- The 046 unique-violation retry remains the correctness safety net for any race.

## Out of Scope
- **E10** (`ScormPackageService` sync-over-async `.Result` via type-erased
  `object` returns): real fix is de-erasing the whole `IScormPackageService`
  surface — a separate story. E6 deliberately adds only a typed method.
- Per-row lookups in cold admin paths (single-record pages: `Edit.cshtml.cs`,
  `Detail.cshtml.cs`, `Program.cs:226`) — one lookup each, not N+1.

## Acceptance Scenarios

1. **Given** a learner on the browse page, **When** the page loads (full or HTMX
   partial), **Then** enrollment state is correct AND the enrollment check is
   exactly one query (unit test with counting fake).
2. **Given** a browse filter change or page click, **When** the HTMX handler runs,
   **Then** exactly one SP call for in-range pages (code inspection + E2E pagination
   specs 02/11/16 stay green).
3. **Given** any learner/admin on /Courses/Index, **When** the category dropdown
   renders, **Then** it lists exactly the same categories as before (E2E), with no
   full-table `ListAsync()` call (code inspection).
4. **Given** a learner's My Courses / attempts pages, **When** rendered, **Then**
   titles are correct (E2E 14) with exactly one batch course lookup (counting-fake
   unit test).
5. **Given** the admin course list, **When** rendered, **Then** SCORM badges are
   correct (E2E 16) with one package query (counting fake / code inspection).
6. **Given** the OrgAdmin dashboard or the org tree, **When** rendered, **Then**
   learner/course/enrollment counts are unchanged (E2E 04/06) and computed with
   two queries total instead of 2×orgs.
7. **Given** a live SCORM session, **When** the same student/course launches
   again, **Then** "session already active" (E2E 15 + 046 unit tests stay green);
   **when** the session is deleted, the index key is gone; **when** the session
   has expired, a stale index returns null and cleans itself (new unit test,
   real Valkey in Scorm.Tests).

## Testing Strategy
- **Unit (counting fakes)**: E1 (IEnrollmentLookup impl), E4, E5 (services with
  fake ICourseLookup asserting 1 call for N rows), E6/E7 (fake contracts).
- **Unit (real Valkey, Scorm.Tests pattern from 046)**: E8 index lifecycle —
  create → find; activity → index TTL refreshed; delete → index gone; stale
  index (hash deleted manually) → null + self-clean.
- **E2E (behavior guard)**: full Playwright suite green (170 passed + 1
  documented skip baseline).
