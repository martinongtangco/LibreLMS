# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 0. The Constitution comes first

`.specify/memory/constitution.md` is the authoritative source of rules for this repo — it outranks
this file, `AGENTS.md`, `.pi/APPEND_SYSTEM.md`, and any handoff prompt. Read it before editing
anything if it is not already in context.

**Before ANY file edit** (per the constitution's "⚠️ Before You Touch Code" and `AGENTS.md`):

1. `git branch --show-current` — you must be on a `bug/` or `story/` branch. `master` is for
   planning only (Principle IX); no commits land on it outside a merge (Principle VIII).
2. A spec must already exist for the change. If not, run `/speckit.specify` **on `master`** first.
3. Declare, before editing: branch name, spec/issue id, and which constitution principles apply.

If any step fails, STOP. There is no "too small to document" exemption — typos and one-line fixes
also need a branch and a spec (Principle X).

Principles that most often change what you do here:

- **XIII Verification Before Claim** — three gates, each with pasted evidence: (1) `dotnet build`
  + the app actually running, (2) Playwright green against the running app, (3) after merging to
  `master`, rebuild/restart/re-run Playwright. Never claim a gate without showing its output.
- **XIV/XV Bounded Retry** — max 2 attempts at the same failing gate. Before attempt 2, classify
  the failure (local / design / blocking) and state what changed. On the 2nd failure, write a
  diagnosis into the spec's task notes and stop.
- **XVI Independent Verification** — the session that wrote the code cannot be the only verifier
  before merge; either a fresh subagent re-runs build + Playwright from a clean worktree, or the
  human reviews the gate evidence and approves the merge.
- **XVII Verify Against a Disposable Environment** — gate evidence must come from a fresh
  per-run database: `.github/workflows/ci.yml` is the authoritative Principle XIII run, and any
  local run (in-container or host-side) against the long-lived dev DB is supporting evidence, not
  proof. Tests create their own data and read endpoints from configuration.
- **XII Return to Master** — `git checkout master` when an implementation slice ends.
- **V The Sandbox Is Not Optional** — mandates that agent work run inside `.devcontainer`. Current
  practice diverges; that conflict is unresolved, see §5. Don't treat the divergence as settled.

## 1. Spec-driven workflow

Specs live in `specs/<NNN>-<kebab-name>/` (`spec.md`, `plan.md`, `tasks.md`, `research.md`,
`data-model.md`, `quickstart.md`, `contracts/`, `checklists/`). The cycle is
`/speckit.specify → /speckit.plan → /speckit.tasks → /speckit.implement`; prompt definitions are in
`.pi/prompts/speckit.*.md` and helper scripts in `.specify/scripts/powershell/`.
`specs/HANDOFF-RUN-LOG.md` records each autonomous run's per-item A–F commits and gate evidence.

`.specify/extensions.yml` registers **mandatory** (`optional: false`) hooks that the speckit prompts
must invoke and wait on — the corresponding skills are in `.pi/skills/`:

| Hook | Skill | What it forces |
|---|---|---|
| `before_implement`, `before_converge` | `verify-test-baseline` | Record exact per-project pass/fail/skip counts and the cause of every known-red test, plus the repo's *actual* commit convention from `git log`, before trusting any gate. |
| `after_implement` | `sequential-project-test-runner` | Run tests one project at a time (see §2) — never solution-wide. |
| `after_implement` | `regression-guard-scope-check` | `git grep` the whole tracked tree for the defect pattern so a regression guard covers the *class*, not the one reported instance. |

Commit convention actually observed in `git log` (verify, don't assume): conventional-commit subject
carrying the spec number — `feat(055): …`, `fix(ci): …`, `test(054): …`, `spec|plan|tasks|docs(NNN): …`.
Merge commits read `Merge bug/055-fix-logged-out-enroll: <what changed> (spec 055)`.

Any decision that took real discussion gets a one-page ADR in `docs/adr/`, numbered sequentially
(currently through `0013`).

## 2. Commands

All test/run scripts are bash (`scripts/`, `.pi/skills/*/scripts/`). The constitution (Principle V)
says agent work runs inside `.devcontainer`; recent practice (specs 051–055, CI) is host-side with
Docker only for services — see §5 before assuming either.

```bash
# Services (MSSQL + Valkey) — required by nearly everything, including unit tests
docker compose up -d

# REQUIRED host-side: `docker compose up -d` exports these INSIDE the devcontainer only,
# never into your host shell. Without them Scorm.Tests throws
# "ConnectionStrings__Sql environment variable is required." in ~0.1s, and the other
# suites silently fall back to `Server=mssql,1433` (a compose-only hostname) and fail.
export ConnectionStrings__Sql="Server=localhost,1433;Database=LearningLms;User Id=sa;Password=$(grep MSSQL_SA_PASSWORD .env | cut -d= -f2-);TrustServerCertificate=True"
export ConnectionStrings__Valkey="localhost:6380"
export ASPNETCORE_ENVIRONMENT=Development

# Build
dotnet restore LibreLms.slnx
dotnet build LibreLms.slnx            # NU1903 (vulnerable package) is an ERROR — see Directory.Build.props

# THE test gate — sequential, per project. Do NOT use `dotnet test LibreLms.slnx`.
./.pi/skills/sequential-project-test-runner/scripts/run.sh

# A single project / a single test
dotnet test tests/Catalog.Tests/Catalog.Tests.csproj --no-restore
dotnet test tests/Enrollment.Tests/Enrollment.Tests.csproj --filter "FullyQualifiedName~AdminListLearnersTests"

# Module-boundary check (constitution Principle III)
dotnet test tests/ArchitectureTests/ArchitectureTests.csproj

# Run the host (applies EF migrations + seeders at startup; readiness = HTTP 302 on /)
dotnet run --project src/Host --urls http://localhost:5000
./scripts/restart-app.sh --background          # kill + clean rebuild + restart

# E2E — requires the host already running on :5000
cd tests/Playwright.Tests
npm ci
npx playwright test
npx playwright test tests/19-course-visibility.spec.ts -g "hidden course"
npx playwright show-report
```

**Why the sequential runner, in one line:** all test projects share one `LearningLms` database with
no isolation, so a solution-wide run collides with itself and passes or fails at random. The full
rationale (and why `-m:1` doesn't help, and why a disagreeing solution-wide run is the artifact to
ignore) lives in `.pi/skills/sequential-project-test-runner/SKILL.md` — that file owns the "why".

**Unit runs dirty the shared DB.** The unit suites write to `LearningLms`, so any state cleanup for
E2E (the CI "Filler-clean" step) must run *after* the last unit project, never before. E2E also
assumes the seeded data set is intact (`tests/Playwright.Tests/utils/testUsers.ts` mirrors
`EnrollmentSeeder`/`ManagementSeeder`).

Configuration is environment-only — there is no committed connection string. Key names:
`ConnectionStrings__Sql` (the key is `Sql`, not `DefaultConnection` — `Program.cs` reads
`GetConnectionString("Sql")`) and `ConnectionStrings__Valkey` (host default `localhost:6380`;
compose publishes Valkey on 6380 because 6379 on this machine belongs to another project).

### Host-side gotchas (none of these bite inside the devcontainer)

- **Three Playwright specs need `ConnectionStrings__Valkey` exported.** `14-profile-courses`,
  `15-scorm-launch-ui`, and `20-scorm-session-authz` each define a `flushScormSessions()` that
  reads that variable and falls back to the compose hostname `valkey:6379` (which doesn't resolve
  from Windows) only when it is unset — since `1748f50`. With the export from the block above it is
  a non-issue; without it, the specs pass until a stale active session exists, then fail with
  `ENOTFOUND valkey` on the recovery path — intermittent by construction. The fallback exists so
  the devcontainer run is unaffected.
- **Launch the Host with `--project src/Host`, not `dotnet Host.dll` from the repo root.**
  `Program.cs` computes `wwwroot` from `ContentRootPath` and *creates it if missing*, so a wrong cwd
  silently produces an empty `wwwroot` and the app serves no CSS/JS/fonts instead of erroring.
- **Watch for a CRLF flood after host-side branch switches.** There is no `.gitattributes` and the
  host has `core.autocrlf=true`, so switching branches on Windows rewrites the bind-mounted tree;
  a later in-container `git add -A` then commits pure line-ending noise. Check `git status --short`
  after a host-side switch and stage explicit paths rather than `-A`.

`.github/workflows/ci.yml` is the canonical end-to-end sequence (restore → build → ArchitectureTests
→ start host → unit projects → filler-clean → Playwright) in one job with MSSQL/Valkey service
containers — and per the constitution's Development Workflow it is the **authoritative Principle
XIII run** (fresh per-run database, Principle XVII); any local run, in-container or host-side, is
supporting evidence.

## 3. Architecture

**Modular monolith, one process.** `src/Host` is the composition root for four modules under
`src/Modules/` — `Catalog`, `Enrollment`, `Scorm`, `Management` — each with `Domain/`,
`Application/`, `Infrastructure/`, `Endpoints/` and a paired `*.Contracts` project.
`src/SharedKernel` holds only `Entity<TId>`, `Result<T>`, `IDomainEvent`, `RoleNames`,
`ITransactionalEmailSender`.

**Boundaries are compiled.** A module may reference another module only through its `*.Contracts`
project (`ICourseLookup`, `IEnrollmentAdmin`, `IUserInfoLookup`, `IOrganizationLookup`, `OrgScope`,
…). `tests/ArchitectureTests/ModuleBoundaryTests.cs` (NetArchTest) fails the build on any direct
dependency between module internals — the check is table-driven over all module pairs, so a new
module must be added to that array. The Host is exempt: as composition root it may register
module-internal services directly.

**Endpoints live in `Program.cs`, not in the modules.** The modules' `Endpoints/*Endpoints.cs`
classes are markers; all minimal-API routes (`/api/courses`, `/api/enrollments`, `/api/scorm/…`,
`/api/users`, `/api/organizations`, `/api/admin/…`, `/api/dashboard`) are mapped inline in
`src/Host/Program.cs`. Each module exposes only an `Add<Module>Module()` DI extension. The web UI is
Razor Pages under `src/Host/Pages/`.

**Storage.** One MSSQL database, four `DbContext`s (one per module) on the same connection string —
the boundary is in C#, not in the schema. All migrations live in the Host assembly under
`src/Host/Migrations/<Module>/` (`MigrationsAssembly(typeof(Program).Assembly)`) and are applied at
startup, followed by the seeders. Valkey holds only the live SCORM `cmi.*` bag for an in-progress
attempt; it is written on every `LMSSetValue` and persisted to SQL on `LMSCommit`/`LMSFinish` —
nothing lives in Valkey that isn't derived from or eventually committed to SQL (Principle VI).

**Hot read paths are stored procedures, called with raw `SqlCommand`** (not EF): `BrowseCourses`
(Catalog), `AdminListEnrollments` / `AdminListLearners` (Enrollment). They are defined inside
migrations, so changing one means a new migration, and their column contract is pinned by tests.
These procedures are the *only* place cross-module storage access is allowed (ADR 0008): the SP
joins another module's table, but no C# module boundary is crossed and no foreign domain type is
returned.

**Auth and org scope.**
- `src/Host/ManagementAuth/AuthClaims.Build(...)` is the single builder for the sign-in cookie's
  claim set; both `LoginModel.OnPostAsync` and `AuthCookieRefresher.RefreshAsync` must go through
  it. `tests/Host.Tests/AuthClaimsTests.cs` pins the contract — a dropped claim once silently
  blanked every OrgAdmin dashboard (bug-039 → story-040).
- `AuthHelpers.GetScope(User)` turns claims into an `OrgScope` (`SuperUser` = system-wide,
  `ForOrgAdmin(orgId)` = that subtree, `None` = fail-closed, matches nothing). Management
  *application services* enforce the scope — lists filter to the subtree, single-target operations
  throw `ForbiddenAccessException` (ADR 0010). Endpoints only build the scope and translate the
  exception.
- The cookie's `OnValidatePrincipal` re-checks the account's `SecurityStamp` on every authenticated
  request, so a password reset invalidates existing sessions immediately (ADR 0006).

### Two traps that have each caused a shipped bug

1. **Never `Results.Forbid()` on an API endpoint.** With cookie auth it emits a 302 to
   `AccessDeniedPath`, which clients follow into login HTML and misparse as success. Return
   `Results.Json(new { error = … }, statusCode: StatusCodes.Status403Forbidden)` instead — that's
   why every 403 in `Program.cs` is written the long way.
2. **Handler-level `[Authorize]` on a Razor Page handler is inert in .NET 10** (endpoint routing
   skips attribute→filter conversion, and per-handler attributes never reach endpoint metadata) —
   the handler just runs for anonymous callers. Guard explicitly as the handler's first statement,
   and never fall back to a hardcoded demo identity (ADR 0013):

   ```csharp
   if (User.Identity?.IsAuthenticated != true)
       return new ChallengeResult("Cookie");
   ```

   Class-level `[Authorize]` and minimal-API `[Authorize]` do work correctly.

## 4. Scope constraints

.NET 10 pinned via `global.json` (released bands only, never preview). EF Core against MSSQL,
StackExchange.Redis against Valkey. No MediatR, no CQRS framework, no repository wrapper over
`DbContext` — add an abstraction only if you can explain it in one plain sentence (Principle II).
SCORM support is deliberately SCORM 1.2 and simplified; SCORM 2004, multi-SCO sequencing, and
`cmi.interactions` are out of scope.

Note: `src/Modules/Management` and `Management.Contracts` are **not listed in `LibreLms.slnx`** —
they build transitively via `Host.csproj`'s project references. Solution-level tooling that
enumerates projects will miss them.

## 5. Open conflicts — do not silently pick a side

**Sandbox vs. host-side execution (unresolved).** Constitution Principle V and its "Development
Workflow" section say *all* coding-agent work happens inside `.devcontainer`, reaching only the
sibling `mssql`/`valkey` containers. Actual recent practice — specs 051–055 and the CI workflow —
is host-side .NET with Docker supplying only those two services, which is why §2 documents host
exports, the `valkey:6379` Playwright gap, and the CRLF trap at all. Exactly one of two things is
true: the constitution is stale and needs an amendment plus a version bump, or host-side runs are a
deviation that should stop. Until a human resolves it, state which mode you are in when you report
gate evidence, and don't cite §2 as authority to leave the container.

**`AGENTS.md` and this file can drift.** Both restate the pre-edit checklist for different tools
(pi reads `AGENTS.md`, Claude Code reads this). `AGENTS.md` currently says only "you must be on a
`bug/` or `story/` branch" and omits Principle IX's carve-out that `/speckit.specify|plan|tasks` run
on `master`. If you change the checklist in one, change both.

**`README.md` is partly stale.** It describes "three independent modules (Catalog, Enrollment,
Scorm)" — `Management` was added later and is a full fourth module — and its run instructions are
devcontainer-centric. Prefer this file and the constitution over the README for module inventory and
commands.
