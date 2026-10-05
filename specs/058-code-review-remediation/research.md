# Research: spec 058 — Code Review Remediation

Phase 0 output of `/speckit.plan`. All NEEDS CLARIFICATION items from the spec are resolved below.

## R1. What counts as a "completed" SCORM attempt (US1 metric semantics)

**Decision**: An attempt is *completed* when its `CourseAttempt.Status` is in the **terminal
completion set** `T = {"completed", "passed", "failed"}` (case-insensitive).

**Rationale**:
- `ScormSessionService` writes `Status` from the raw SCORM 1.2 `cmi.core.lesson_status` on
  `LMSCommit`/`LMSFinish` (valid set: "not attempted", "incomplete", "completed", "passed",
  "failed", "browsed", "neutral"; initial value "in-progress"). There is no separate
  completion/pass field — one string carries both.
- Real SCORM 1.2 output sets `lesson_status` to **passed/failed** whenever the package defines a
  passing score, and only "completed" otherwise. Matching "completed" strictly would report 0%
  completion for the most common real-world packages — the exact class of silent-wrong-number
  bug this spec exists to kill.
- In SCORM 1.2 semantics, `passed`/`failed` are terminal states that imply the lesson content was
  finished; completion ≠ pass rate (pass rate is a different metric this dashboard does not show).
- Non-terminal statuses ("in-progress", "incomplete", "browsed", "neutral", "not attempted",
  "abandoned") never count. This satisfies the spec's edge case: "a learner has SCORM attempts
  but no completed ones → completion rate reads 0% from real data".

**Alternatives considered**:
- `Status == "completed"` only — rejected (undercounts real packages, see above).
- "Any attempt with `CompletedAt != null`" — rejected: `CompletedAt` is set on `LMSFinish`
  regardless of lesson_status, so an abandoned-without-finishing session could be marked complete;
  status is the source of truth the SCORM contract defines.
- Course-based rate (completed distinct courses / enrolled courses) — rejected: spec Key Entities
  say the metrics "must become real aggregates over **SCORM attempt data**"; attempt-based is also
  what the data model supports without pulling enrollment joins into the Scorm contract.

**Case sensitivity**: the SCORM shim validates lesson_status with
`StringComparer.OrdinalIgnoreCase`, so stored values may vary in case. The aggregate query
compares case-insensitively (`EF.Functions.ToLower`) — dev-scale, non-indexed, correctness first.

## R2. Exact metric formulas

**Decision**:

| Metric | Formula | Zero case |
|---|---|---|
| `AverageCompletionRate` (System + Org) | `completedAttemptsInScope / totalAttemptsInScope` | 0.0 when the scope has no attempts |
| `CompletedCourseCount` (Personal) | distinct `CourseId` among the learner's T-status attempts | 0 |
| `AverageScore` (Personal) | mean of `ScoreRaw` over the learner's T-status attempts **with a non-null score** | 0.0 when none |

**Rationale**:
- US1-1 (one completed attempt with a recorded score → both metrics reflect it, not 0) holds for
  every formula above.
- `AverageScore` is restricted to completed attempts: an in-progress attempt's partial
  `ScoreRaw` is not a score *for the course*. Alternative (mean over all scored attempts) was
  rejected — it would let a half-finished attempt move the "average score" on a dashboard card
  labeled for completed learning.
- Division-by-zero is guarded in the service (spec edge case: "must not divide by zero; must
  return 0, not throw").
- `AverageCompletionRate` stays a `double` in 0.0–1.0; the Razor page's existing `"0.#%"` custom
  format (bug-043, culture-proof) renders it unchanged. **No DTO shape changes** —
  `SystemMetricsDto`/`OrgMetricsDto`/`PersonalMetricsDto` keep their fields; only the values stop
  being constants.

## R3. Cross-module surface for attempt aggregates (US1 architecture)

**Decision**: new interface in `Scorm.Contracts`:

```csharp
public record AttemptStatsSummary(
    int TotalAttempts,
    int CompletedAttempts,          // Status in T
    int ScoredCompletedAttempts,    // Completed && ScoreRaw != null
    double CompletedScoreSum,       // sum of ScoreRaw over those
    int DistinctCompletedCourses);  // distinct CourseId among completed attempts

public interface IScormAttemptStats
{
    Task<AttemptStatsSummary> GetSystemStatsAsync();                        // all attempts
    Task<AttemptStatsSummary> GetStatsForStudentsAsync(IEnumerable<Guid> studentIds); // empty → zeros, no DB hit
}
```

implemented by `ScormAttemptStatsService` (Scorm Application) over `ScormDbContext.CourseAttempts`
with one aggregate EF query per call, registered scoped in `AddScormModule`/`ConfigureScormModule`.

**Rationale**:
- Management has **no** legal path to Scorm data today (Scorm.Contracts contains only
  `IScormPackageService`); a new Contracts interface is the only Principle-III-compliant wiring.
- The Scorm module knows nothing about organizations (attempts carry `StudentId` only); org
  scoping is Management's job. So the contract is student-scoped, and `DashboardService` resolves
  "which students are in my subtree" itself — mirroring how it already resolves learner counts
  (per-org `IUserLookup`/`IUserProvisioning` calls, "dev scale: a handful of orgs" — the
  established spec 048 pattern).
- One summary record serves all three views: System = `GetSystemStatsAsync()`; Org =
  `GetStatsForStudentsAsync(subtreeStudentIds)` (student ids from
  `IUserProvisioning.ListByOrgAsync(orgId)` per subtree org, unioned); Personal =
  `GetStatsForStudentsAsync([studentId])`. One query per dashboard view — same shape as the
  existing per-metric bulk queries.
- `DistinctCompletedCourses` is computed in the same query and only consumed by Personal; keeping
  one record avoids two contract shapes (Principle II: one sentence — "per-student-set aggregate
  of attempt counts and scores").

**Alternatives considered**:
- Add methods to an existing Scorm contract (`IScormPackageService`) — rejected: packages ≠
  attempts; conflating would violate the one-concern shape of the existing interface.
- Org-scoped contract methods (`GetOrgStatsAsync(rootOrgId)`) — rejected: would leak the org
  hierarchy into the Scorm module (it has no org data; it would have to call back into
  Enrollment/Management — boundary inversion).
- Expose raw attempts and aggregate in Management — rejected: N+1 at dev scale is tolerable, but
  it moves Scorm-domain semantics (what "completed" means) into Management; the set T stays in
  Scorm.

## R4. Error-handling convention (US3 / ADR 0014)

**Decision**: **Typed exceptions remain the convention for Management Application services'
expected business failures; endpoints translate them at a single shared translation point
(`ManagementErrors.Translate` in Host).** `Result`/`Result<T>` and per-operation result records
stay exactly where they are today (Registration, Scorm session/package surfaces) and are not
extended to this layer. Full context/consequences in `docs/adr/0014`.

**Rationale** (the ADR develops this; summary):
- The spec's acceptance bar is "a documented decision + at least one applied refactor with zero
  observable HTTP change". The four cited `Program.cs` catch blocks map three exception types to
  **three different status/body shapes** (403 JSON body, 404 *no body*, 400 JSON body).
  `SharedKernel.Result<T>` carries only an `Error` string — an endpoint handed a `Result<T>`
  cannot recover 403-vs-404-vs-400 without matching message strings (the Scorm launch endpoint
  already does string-matching: `result.Error == "Student is not enrolled in this course."`).
  Generalizing that to the Management layer would trade ~10 duplicated catch blocks for
  fragile message matching — a worse trade, not a better convention.
- The typed exceptions already carry the distinction losslessly (`ForbiddenAccessException`,
  BCL `KeyNotFoundException`, `InvalidOperationException`, `ArgumentException`); the duplication
  is in the *translation*, not the signaling. Collapsing each handler's 4-block catch into
  `catch (T1 or T2 or T3 or T4 ex) => ManagementErrors.Translate(ex)` — with **the same type set
  per handler as today** — removes the repetition with a provably identical mapping.
- Principle II: the helper is one sentence ("translates Management's expected business
  exceptions into their HTTP responses"); a per-service result-record family or a
  config-driven mapper would be the "clever generalization" the constitution warns against.
- The spec's edge case explicitly blesses a scoped outcome: the two conventions coexist, each in
  its documented lane — exceptions = typed, recoverable business failures in Management services;
  result records = multi-field operational outcomes on the Scorm session/registration surfaces
  (where the endpoint genuinely needs more than a status: `sessionId`, `committedAt`, `status`,
  `score`).

**Application scope (zero-behavior-change proof)**: the shared mapper is applied to exactly the
handlers whose caught-type sets are uniform with the mapper's mapping:
- `users` group — the 4 cited handlers (GET `/{id}`, POST `/`, PUT `/{id}`, DELETE `/{id}`) —
  **mandatory** per FR-003;
- `orgs` group (5 handlers) and `adminCourses` group (3 handlers) — same uniform shape
  (403-JSON / 404-no-body / 400-JSON), collapsed in the same commit set;
- `adminEnrollments` group — **kept as-is**: its failure set differs (404 *with* body, 409
  Conflict); the ADR documents that groups whose failure shape diverges keep explicit catches.
  Each refactored handler catches the *identical* exception-type set it catches today, so no new
  exception can be translated where it previously 500'd.

**Regression guard**: `tests/Host.Tests/ManagementErrorsTests.cs` pins all four mappings
(status + body shape) so the translation contract is enforced, not just reviewed.

## R5. Which auth-declaration style to standardize on (US4/FR-006)

**Decision**: standardize on the **group-level role declaration** (the `adminEnrollments` style):

```csharp
app.MapGroup("/api/users").WithTags("Users")
    .RequireAuthorization(new AuthorizeAttribute { Roles = "SuperUser,OrgAdmin" });
```

and remove the per-handler `[Authorize(Roles = "SuperUser,OrgAdmin")]` from every handler in the
`users`, `orgs`, and `adminCourses` groups (all of whose handlers use exactly that role set today).

**Rationale**:
- Functionally identical today: every handler in the four groups already requires exactly
  `SuperUser,OrgAdmin` (verified per-handler in `Program.cs`); group policy + no handler
  attributes compose to the same effective policy.
- The spec's stated goal: "a new handler added to any group can't silently inherit the wrong
  pattern". With group-level *roles*, a new handler automatically inherits the full role check.
  With group-level bare `.RequireAuthorization()` + per-handler attributes (the current
  majority style), a new handler added without its `[Authorize(Roles=...)]` silently inherits
  *authentication-only* access — the wrong pattern by construction.
- `dashboard` group is out of scope (FR-006 names only the four groups) and keeps its
  per-endpoint role variation (Learner on `GET /`, SuperUser/OrgAdmin on `/activity`).
- Playwright `08-rbac.spec.ts` covers anonymous/wrong-role 401/403 for these groups — the
  no-access-regression proof (SC-004).

## R6. Execution mode & gate evidence (CLAUDE.md §5 open conflict)

**Decision (declared, not adjudicated)**: this run works **host-side** — .NET SDK and the Host
run on the Windows host; Docker supplies only the `mssql` + `valkey` sibling services
(`docker compose up -d` from the main repo root, which owns the committed-adjacent `.env` and the
long-lived `mssql-data` volume). Every gate report states "host-side, long-lived dev DB —
supporting evidence per XVII". The authoritative gate for each milestone is **branch CI**
(fresh per-run DB), observed via `gh` (`gh` is authenticated on this machine with `repo` +
`workflow` scopes; `git push --dry-run` to origin verified clean at plan time). The §5 conflict
(Principle V in-container mandate vs host-side practice) remains open; nothing here picks a side.

**Local gate order (per CLAUDE.md §2, host-side)**:
1. `docker compose up -d` (main repo root) → MSSQL :1433, Valkey :6380.
2. `export ConnectionStrings__Sql=… ConnectionStrings__Valkey=localhost:6380 ASPNETCORE_ENVIRONMENT=Development`.
3. `dotnet build LibreLms.slnx` (0 errors; NU1903 = error).
4. `./scripts/restart-app.sh --background` (kill + clean rebuild + restart; readiness `/` → 302).
5. `./.pi/skills/sequential-project-test-runner/scripts/run.sh` (the unit gate — sequential,
   per-project; unit runs dirty the shared DB, so any E2E filler-clean must come after).
6. `cd tests/Playwright.Tests && npx playwright test` (host already running on :5000).
7. `git push origin story/058-code-review-remediation` → watch CI to completion (`gh run watch`);
   record the run ID.

**Known environmental hazards** (from CLAUDE.md §2 + run log, budgeted before they bite):
- Host Playwright SCORM specs need `ConnectionStrings__Valkey` exported (else ENOTFOUND valkey
  flake) — handled by step 2.
- Launch Host with `--project src/Host` (wwwroot computed from ContentRootPath).
- Unit suites write to the shared `LearningLms` DB; E2E assumes seeded data intact; Valkey
  FLUSHALL before E2E if stale SCORM sessions interfere (the new spec has its own launch-retry
  pattern from `20-scorm-session-authz`).
- CRLF flood after branch switches on Windows (no `.gitattributes`, `core.autocrlf=true`) —
  stage explicit paths, never `git add -A`.

## R7. Where milestone evidence lands (run log)

**Decision**: `specs/HANDOFF-RUN-LOG.md` gains **Item 10 — spec 058**, updated per milestone on
the branch (the implementing worktree), following the file's existing A–F format. The branch is
merged (externally, after XVI), which carries the run-log entries to master — the same path the
F/gate-3 docs commits took for in-branch work in prior items. The reviewing Claude session reads
branch + master and is itself confined to an isolated worktree (per `HANDOFF.md`), so no
write-conflict on master is expected mid-run.

## R8. P2 test strategy details

**Decision**: follow the existing Management.Tests patterns — self-contained xUnit files, fake
contract implementations (the `FakeOrgLookup`/`FakeUserLookup`/`FakeProvisioning` style in
`UserServiceScopeTests.cs`), EF InMemory for services that take `ManagementDbContext`
(`OrganizationLookup`, and `DashboardService` for its org-name/count reads), unique
`databaseName: $"…{Guid.NewGuid()}"` per test class (the `CourseVisibilityScopeTests` pattern).

Per-service minimums (FR-002 "at least one non-trivial case each"):
- **DashboardService** (lands with P1 — the red→green guard for the fix): org-metrics subtree
  aggregate over a 3-level fake org tree with hand-computed expected values (US2-1); system
  metrics from fake stats (incl. zero-attempts → 0.0); personal metrics (one completed scored
  attempt → count 1 + exact score; no attempts → 0/0, no divide-by-zero; attempts-but-none-
  completed → 0/0).
- **OrganizationLookup**: found/missing/soft-deleted org; children exclude deleted; ancestor
  chain includes self, stops at root and at a deleted parent.
- **UserInfoLookup**: maps Enrollment `UserScopeInfo` → Management `UserScopeInfo`; null pass-through.
- **OrgSubtree**: SuperUser → null; OrgAdmin → org + all descendants (multi-level BFS); None →
  empty; `IsOrgInScopeAsync` self/descendant/sibling/ancestor/None matrix.
- **TreeLayoutService**: a non-trivial shape (root, 2 children, one child with 2 children):
  depths correct, Y = depth × (NodeHeight 50 + LevelGap 80), X positions distinct and parent
  centered over children, soft-deleted child excluded (US2-2).

**Red→green evidence (US2 independent test)**: the P1 DashboardService tests are run once
against the still-hardcoded service (expected 0.4 / actual 0.0) and recorded, then wired green.
The P2 commit adds the other four files; `dotnet test tests/Management.Tests` count grows by
≥ 4 new tests (SC-002) and all pass.

## R9. ArchitectureTests additions (US4/FR-005)

**Decision**: extend `ModuleBoundaryTests` with two more table-driven assertions, using
NetArchTest against the contracts assemblies (already referenced by the test project, and each
`*.Contracts` has a `ModuleMarker` in `LibreLms.Contracts.<Module>`):
1. For each module M: `Types.InAssembly(Contracts(M)).That().ResideInNamespace("LibreLms.Contracts.M").ShouldNot().HaveDependencyOn("LibreLms.Modules.M")` — a Contracts project must not depend on its own module's internals.
2. `Types.InAssembly(SharedKernel)` must not have a dependency on any `LibreLms.Modules.*` or
   `LibreLms.Contracts.*` namespace — SharedKernel stays at the bottom of the graph.

Both rules hold today (the test is green on merge); during development each is manually
violated once (temporary type reference → red → revert) to prove the assertion actually enforces
the rule (US4 acceptance scenario 2 — "manually verified during development, not left in the
tree").

## R10. Duplicate `Database.Migrate()` calls (US4/FR-004)

**Decision**: delete the second `enrollmentCtx`/`scormCtx`/`managementCtx` `Migrate()` calls
(Program.cs:161-167), leaving exactly one per context. `Migrate()` is idempotent (EF applies
missing migrations only), so removal is behavior-preserving; proof = gate 1 app start on the
existing dev DB (migrations no-op, seeders no-op) + a fresh-DB CI run (migrations run once,
schema complete — the CI green itself is the proof that one call suffices).

## Open items deliberately NOT decided here

- Whether to convert the remaining throwing services (Organization/CourseVisibility/
  AdminEnrollment) to the ADR 0014 pattern — out of scope per the spec ("not a wholesale
  rewrite"); recorded as a follow-up candidate in ADR 0014's consequences.
- `README.md` staleness (CLAUDE.md §5) — separate concern, untouched.
- Fast-forwarding `master` beyond the spec commit — done once at plan time (3b421c3); no further
  master updates from this session.
