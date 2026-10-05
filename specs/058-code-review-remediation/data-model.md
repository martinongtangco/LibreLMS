# Data Model: spec 058 — Code Review Remediation

Phase 1 output of `/speckit.plan`. No database schema changes in this spec — the changes are to
C# types (one new cross-module contract, one Host translation helper, two test-only additions).
Existing persisted entities are read, not altered.

## Existing entity read by P1 (no changes)

### `CourseAttempt` (Scorm.Domain, persisted in MSSQL `CourseAttempts`)

| Field | Type | Role in this spec |
|---|---|---|
| `Id` | Guid | attempt identity |
| `StudentId` | Guid | scoping key — Org/Personal views filter by student sets |
| `CourseId` | Guid | `DistinctCompletedCourses` |
| `AttemptNumber` | int | (not used by metrics) |
| `Status` | string | "in-progress" initially; raw SCORM 1.2 `lesson_status` after commit/finish. **Terminal completion set** T = {completed, passed, failed}, compared case-insensitively (research R1) |
| `ScoreRaw` | double? | cmi.core.score.raw (0–100); nullable — only non-null scores count toward `AverageScore` |
| `CompletedAt` | DateTimeOffset? | (not used by metrics — status is the source of truth, R1) |
| `StartedAt` / `LastCommitAt` / `SessionTime` / `SuspendData` | — | not used by metrics |

State transitions (unchanged): `in-progress` → (LMSCommit/LMSFinish) → lesson_status ∈
{not attempted, incomplete, completed, passed, failed, browsed, neutral}.

## New types

### `AttemptStatsSummary` — record in `Scorm.Contracts`

Aggregate over a set of attempts (all platform attempts, or the attempts of a student set).
Derived, never stored.

| Field | Type | Definition |
|---|---|---|
| `TotalAttempts` | int | count of attempts in the set |
| `CompletedAttempts` | int | attempts with `Status` ∈ T |
| `ScoredCompletedAttempts` | int | completed attempts with `ScoreRaw != null` |
| `CompletedScoreSum` | double | sum of `ScoreRaw` over `ScoredCompletedAttempts` |
| `DistinctCompletedCourses` | int | distinct `CourseId` among completed attempts |

Zero summary (0, 0, 0, 0.0, 0) is returned for empty student input **without touching the
database** (contract-level guarantee; keeps Personal/Org views cheap for empty subtrees).

### `IScormAttemptStats` — interface in `Scorm.Contracts`

```csharp
public interface IScormAttemptStats
{
    Task<AttemptStatsSummary> GetSystemStatsAsync();
    Task<AttemptStatsSummary> GetStatsForStudentsAsync(IEnumerable<Guid> studentIds);
}
```

- Implemented by `ScormAttemptStatsService` (Scorm.Application) — one aggregate query per call
  over `ScormDbContext.CourseAttempts` (`AsNoTracking()`).
- Registered `AddScoped<IScormAttemptStats, ScormAttemptStatsService>()` in the Scorm module's
  DI extension (the composition root already calls it; no Host change needed).
- Consumed by `Management.Application.DashboardService` **only through `LibreLms.Contracts.Scorm`**
  (Principle III; enforced by ArchitectureTests, including the new P4 assertions).

### `DashboardService` — constructor change (no DTO changes)

```text
before: DashboardService(ManagementDbContext, IUserLookup, IEnrollmentAdmin, ICourseLookup, IOrganizationLookup)
after:  DashboardService(ManagementDbContext, IUserLookup, IEnrollmentAdmin, ICourseLookup,
                          IOrganizationLookup, IScormAttemptStats)
```

| View | New computation (research R2) |
|---|---|
| `GetSystemMetricsAsync` → `AverageCompletionRate` | `stats = GetSystemStatsAsync()`; `stats.TotalAttempts == 0 ? 0.0 : CompletedAttempts / TotalAttempts` |
| `GetOrgMetricsAsync` → `AverageCompletionRate` | subtree org ids (existing `OrgSubtree.GetSubtreeOrgIdsAsync`) → student ids via `IUserProvisioning.ListByOrgAsync(orgId)` per org (union, distinct) → `GetStatsForStudentsAsync(ids)` → same formula. **New constructor dependency `IUserProvisioning`** (Enrollment.Contracts — already a legal dependency of Management) |
| `GetPersonalMetricsAsync` → `CompletedCourseCount`, `AverageScore` | `stats = GetStatsForStudentsAsync([studentId])`; `CompletedCourseCount = DistinctCompletedCourses`; `AverageScore = ScoredCompletedAttempts == 0 ? 0.0 : CompletedScoreSum / ScoredCompletedAttempts` |

DTOs `SystemMetricsDto`/`OrgMetricsDto`/`PersonalMetricsDto` are **unchanged** (field set and
names identical — no API/Razor break; the page's existing `"0.#%"` render keeps working).

### `ManagementErrors` — static class in `src/Host` (P3)

Endpoint-translation point for Management's expected business exceptions (ADR 0014).

| Input exception | Output |
|---|---|
| `LibreLms.Contracts.Management.ForbiddenAccessException` | `403` `Results.Json(new { error = ex.Message })` |
| `System.Collections.Generic.KeyNotFoundException` | `404` `Results.NotFound()` (no body — matches the users/orgs/adminCourses handlers' existing behavior) |
| `InvalidOperationException` | `400` `Results.Json(new { error = ex.Message })` |
| `ArgumentException` (incl. subclasses) | `400` `Results.Json(new { error = ex.Message })` |
| anything else | `throw` (unreachable — handlers only catch the mapped types) |

Applied only where a handler's current caught-type set is a subset of the mapped types **and**
its current per-type mapping equals the table above (users: 4 cited handlers; orgs: 5;
adminCourses: 3). `adminEnrollments` is untouched (its 404 carries a body and it maps
`InvalidOperationException` → 409).

## Test-only types

- `tests/Host.Tests/ManagementErrorsTests.cs` — xUnit facts pinning each row of the table above
  (status code + body shape).
- `tests/Management.Tests/*` new files — fake contracts (`FakeScormAttemptStats`,
  `FakeProvisioning`/`FakeOrgLookup`/`FakeUserLookup` as needed, self-contained per file per
  house pattern) + EF InMemory `ManagementDbContext` where a service reads it directly.
- `tests/Playwright.Tests/tests/21-dashboard-real-stats.spec.ts` — creates its own data per run
  (XVII.1): unique learner `dash058-<ts>@example.com` in the root org, enrollment in the seeded
  SCORM course (`11111111-1111-1111-1111-111111111111`), a completed SCORM session with
  `lesson_status=completed`, `score.raw=87.5`; plus a unique empty child org + OrgAdmin for the
  US1-2 zero-case. Best-effort teardown (delete learner/org) — assertions are lower-bound
  (`>=` / `> 0`) so the persistent dev DB's growth never flips them.
