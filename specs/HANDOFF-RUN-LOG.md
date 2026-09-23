# Hardening Run Log

Autonomous multi-spec hardening pass driven by
`HANDOFF-hardening-loop.md` (scratchpad). Six SpecKit cycles back to back.
Constitution (`.specify/memory/constitution.md` v1.8.0) outranks the handoff.

## Baseline (run start)

- Branch: `master` (verified)
- Dirty at start: untracked stray root `nul` file (pre-existing, NOT committed this run)
- Build: `dotnet build LibreLms.slnx` → **0 Error(s)**, 48 Warning(s)
  (handoff said 54 warnings; actual 48 — all NU1903 for
  `System.Security.Cryptography.Xml` 9.0.0; 0 errors is the gate)
- Baseline E2E target (per handoff + spec 048 F-commit): **170 passed, 1
  documented skip (verify-email)**
- Note: handoff section 4 claims "two attribution lines" end every commit —
  verified against `git log` history: no such lines exist in recent history.
  Matching the actual observed style instead (conventional-commit subject with
  spec number; body only where prior commits had one).
- Environment: Docker daemon was DOWN at run start; Docker Desktop launched at
  run start. MSSQL + Valkey required for gate 2 (app + real-DB unit suites).

## Outer loop state

`consecutive_blocked = 0`

## Items

### Item 1 — Zip Slip in SCORM package upload — spec 049 (bug/049-fix-scorm-zip-traversal)
- [x] A  spec        commit 4df66d0
- [x] B  plan        commit d6aab32
- [x] C  tasks       commit 6103380
- [x] D  implement   commit c5742c3   build 0 errors / E2E 170 passed 1 skipped (unit: Arch 14, Host 8, Catalog 32, Scorm 15, Enrollment 41/42 — 1 pre-existing master failure, worktree-proven)
- [x] E  merge       commit 975eb00
- [x] F  gate 3      commit (this commit)   E2E 170 passed 1 skipped (unit: Arch 14, Host 8, Catalog 32, Scorm 15, Enrollment 41/42 — 1 pre-existing master failure)
RESULT: COMPLETED     consecutive_blocked = 0

Item 1 findings for final report:
- Pre-existing master unit failure: AdminListLearnersTests.never_exposes_credential_columns asserts 8 SP columns; spec 042 migration 20260829105050 re-created the SP with 9 — reproducible in a clean master worktree (NOT caused by 049). Candidate: update the assertion to 9 (or pin the SP column contract).
- Pre-existing E2E parallel-isolation race: 16-admin-pagination creates 'AdmPg032C' filler courses mid-run that push 19-course-visibility's target course off page 1 (12/page). Passes serially (CI=1). Candidate: test-infra fix (isolate filler state / run serial).
- Pre-existing: POST /api/scorm/upload 500s (minimal API binds IFormCollection ⇒ anti-forgery metadata, but app.UseAntiforgery() is never called). API surface is fail-closed (never reaches UploadAsync); Razor-page upload surfaces work and enforce the 049 fix. Candidate: add app.UseAntiforgery() or opt the endpoint out.

### Item 2 — SCORM session API unauthenticated/unowned — spec 050 (bug/050-fix-scorm-session-authz)
- [x] A  spec        commit 13e0e80
- [x] B  plan        commit 58df486
- [x] C  tasks       commit 64fdd30
- [x] D  implement   commit 7b5a04c   build 0 errors / E2E 172 passed 1 skipped (unit: Arch 14, Host 8, Catalog 32, Scorm 18, Enrollment 41/42 — 1 pre-existing master failure)
- [x] E  merge       commit 0256914   (independent verification GREEN: worktree build 0 errors, Scorm 18/18, Playwright 172+1 skip)
- [x] F  gate 3      commit (this commit)   build 0 errors / E2E 172 passed 1 skipped (unit: Arch 14, Host 8, Catalog 32, Scorm 18, Enrollment 41/42 — 1 pre-existing master failure)
RESULT: COMPLETED     consecutive_blocked = 0

Item 2 findings for final report:
- Cookie-auth challenge semantics (verified empirically): unauthenticated GET and bodyless POST → 302 to /Account/Login; JSON-body POST → plain 401. E2E asserts the raw challenge (maxRedirects: 0) for both shapes.
- Process: in-container app restart requires `docker exec -d` (fully detached); the `nohup &`-inside-`docker exec` pattern races with exec-session teardown (SIGTERM seconds after start, nondeterministic).
- Process: unit suites write to the shared LearningLms DB — the E2E filler-clean must run after the last unit run.

### Item 3 — configuration not reproducible / committed secret — spec 051 (bug/051-fix-config-reproducibility)
- [x] A  spec        commit 728f56f
- [x] B  plan        commit 72d72f7
- [x] C  tasks       commit 771b86d
- [x] D  implement   commit bdca2cc (D1: compose key + devcontainer + Valkey fallback) + d344c71 (D2: secret removal + scan test + README) + docs 9763b2c   gate 1 re-verified after EACH of the four changes (bare in-container start on compose env only; old solution name → MSB1009; host start with documented export)
- [x] E  merge       commit 6dd1519   (independent verification: first pass RED on the E2E — triaged to the omitted filler-clean step, not a branch defect; re-run under the documented procedure GREEN: 172+1 skip, 0 retries)
- [x] F  gate 3      commit (this commit)   build 0 errors / E2E 172 passed 1 skipped (unit: Arch 14, Host 9, Catalog 32, Scorm 18, Enrollment 41/42 — 1 pre-existing master failure)
RESULT: COMPLETED     consecutive_blocked = 0

Item 3 findings for final report:
- The committed SA password (Lms#vZdV361x…, also the .env value) REMAINS IN GIT HISTORY — rotation is the follow-up (ALTER LOGIN sa, then .env; procedure in the spec's quickstart). Not attempted here (out of scope, per handoff).
- Historical: spec 012 changed the code to read ConnectionStrings:Sql but never updated the config side; this spec finished that job.
- Environment: recreating the devcontainer wipes /ms-playwright (browsers + system deps are in the writable layer — not in image/volume/Dockerfile). Restored with `npx playwright install chromium` + `install-deps chromium`. Candidate future spec: stage E2E browsers/deps in the devcontainer Dockerfile.
- Environment: Catalog.Tests perf tests seed ~11.7k filler courses with no teardown — any E2E run after a full unit run must do the documented filler-clean first (bit the independent verification once; triaged, not a branch defect).
- Secret-scan regression guard added (Host.Tests 9/9 now).

### Item 4 — organization scope never enforced — spec 052 (story/052-enforce-org-scope)
- [X] A  spec        commit 6dd015c
- [X] B  plan + ADR  commit ec33aca (ADR 0010: enforce in Management services via required OrgScope param)
- [X] C  tasks       commit aee4b0c (T001–T027)
- [X] D  implement   commit 1755c77 (OrgScope + OrgSubtree; 4 surfaces scoped; 2 SP migrations w/ @RootOrgId; Management.Tests 55 tests; 08-rbac subtree block; DashboardService on shared OrgSubtree)
- [X] E  merge       commit 66748c2 (--no-ff, after independent verification GREEN)
- [X] F  gate 3      commit 4f691fd (units 170/170 no-flake, E2E 177+1 on master)
RESULT: COMPLETED   consecutive_blocked = 0
Future-spec candidates (adjacent, not fixed in 052): (1) GET /api/dashboard/activity
is system-wide for OrgAdmins (activity feed, not one of the four surfaces);
(2) Catalog.Tests perf-seed flake under parallel `dotnet test LibreLms.slnx`
(shared-DB sensitivity — serial/per-project runs are stable).
Verification (XVI, fresh no-context subagent, clean detached worktree @1755c77): GREEN —
build 0 errors; units 170/170 (one documented Catalog parallel-flake, 32/32 in isolation);
app on branch code (302 probe); filler-clean 10 courses; Playwright 177 passed + 1 skip, 0 failed.
Notes: E2E red pre-fix (alice@example.com visible to a child OrgAdmin, 08-rbac:273); unit red
pre-fix (CS1501 on all scoped calls); SP migrations need the .Designer.cs [Migration] partial
or EF silently skips them; run `dotnet test LibreLms.slnx` with ConnectionStrings__Sql sourced
in the same shell or the DB-backed suites fail on the missing env var.

### Item 5 — no continuous integration — spec 053 (story/053-add-ci-pipeline)
- [X] A  spec        commit bf6f4e6
- [X] B  plan + ADR  commit c419c26 (ADR 0011: single GH Actions job, job-level mssql+valkey services, Host starts before unit steps, NU1903→error, CryptXml pin)
- [X] C  tasks       commit 54101c5 (T001–T009)
- [X] D  implement   commit 3b670a7 (ci.yml 15 steps; Directory.Build.props: NU1903 error + System.Security.Cryptography.Xml 9.0.20 pin + centralized Nullable/ImplicitUsings)
- [X] E  merge       commit 2843f32 (--no-ff, after independent verification GREEN)
- [X] F  gate 3      commit d5515a5 (build 0 errors/0 NU1903, units 170/170 no-flake, E2E 177+1 on master)
RESULT: COMPLETED   consecutive_blocked = 0
Process note: 053's A–D commits initially landed on master (branch step
skipped); restored to the house shape before merging — branch parked at
the 053 tip, master reset to f953717, then a proper --no-ff merge (E).
Commit hashes unchanged.
Notes: baseline (red) = no .github/, build with 96 NU1903 lines. Post-fix build:
0 errors, 0 NU1903. Version gotcha: an early 9.0.11 pin still failed (truncated
version list) — OSV check shows the 8 advisories require up to 9.0.18; pin
settled at 9.0.20 (latest stable 9.0 line; no stable 10.x on NuGet).
Gate 2 for this item = the workflow's command sequence passed locally in
order (Principle V: no remote CI trigger/observation): restore → build →
ArchTests 14 → Host start (in-container equivalent: Now listening + 302) →
units Catalog 32 / Enrollment 42 / Host 9 / Management 55 / Scorm 18 →
filler-clean (11,668 → 10 courses) → Playwright 177 passed + 1 skip, 0 failed.
Verification (XVI, fresh no-context subagent, clean detached worktree @3b670a7):
GREEN — build 0 errors / 0 NU1903; units 170/170 (one documented Catalog
flake, 32/32 on re-run); app commit-matched (302); filler-clean 10 courses;
Playwright 177 passed + 1 skip, 0 failed.

### Item 6 — browse filters after paging, so the page count is wrong — spec 054 (bug/054-fix-browse-filter-after-paging)
- [X] A  spec        commit 4f9b3ac
- [X] B  plan + ADR  commit 4ab510d (ADR 0012: visible set moves into BrowseCourses as JSON param; OPENJSON, no TVP DDL; NULL = legacy, [] = empty)
- [X] C  tasks       commit 15ae68c (T001–T010)
- [X] D  implement   commit 63d486a (Catalog migration 20260914100000 + Designer; BrowseAsync JSON param, in-memory filter deleted; page model resolves the visible catalog once per request; Catalog.Tests +7; 19-course-visibility pagination test)
- [X] E  merge       commit 1f08dc3 (--no-ff, after independent verification GREEN)
- [X] F  gate 3      commit 5996b99 (units 177/177 no-flake, E2E 178+1 on master)
RESULT: COMPLETED   consecutive_blocked = 0
Notes: unit red pre-fix (TotalCount Expected 8/Actual 13; empty-set Expected 0/Actual 13; 4× "too many arguments");
E2E red pre-fix (nav.pagination present: "Page 1 of 2 (24 total)" — 24 total / 8 visible fixture, 14
admin-UI courses `ZZ Pag <ts>` + 16 hides). Gotchas hit: (1) OPENJSON WITH on a scalar GUID array is
object-property extraction — silently NULL rows; the predicate is plain `CAST([value] AS UNIQUEIDENTIFIER)
FROM OPENJSON(...)`; (2) an already-recorded migration is never re-applied — the live DB needed a manual
DROP+CREATE (separate batches) of the corrected SP; (3) a mid-creation E2E failure orphans the run's
courses and poisons later runs (teardown now re-resolves by title prefix). E2E test-data math: 10 seeded +
14 created = 24; hide 16 → 8 visible; pre-fix total 24 → 2 advertised pages, page 2 empty; post-fix total
8 → 1 page.
Verification (XVI, fresh no-context subagent, clean detached worktree @63d486a): GREEN — build 0 errors;
units 14+39+42+9+55+18 (no flake); app commit-matched (302); filler-clean 10 courses; Playwright
178 passed + 1 skip, 0 failed (both 19-course-visibility tests green).

### Item 7 — logged-out visitors can enroll (silent demo-account attribution) + broken return-to-course — spec 055 (bug/055-fix-logged-out-enroll)
- [X] A  spec        commit 3434a50
- [X] B  plan        commit a399e94 (ADR 0013 written at T002, before code; landed with D)
- [X] C  tasks       commit c38ca68 (T001–T021)
- [X] D  implement   commits 5721120 (US1 MVP: ChallengeResult guard, GetStudentId→Guid.Empty, lms.ReturnUrl cookie, guest plain form), 48a9f5a (US2 journey E2E — test-only, Signup/Verify confirmed untouched), 6f43ba9 (US3: MyCourses [Authorize], Settings dead-fallback removal, catalog empty-guid early-out), 06ae70b (gates 1–2 evidence; 08-rbac:34 updated to the new MyCourses contract — it asserted the pre-055 behavior FR-008 removes)
- [X] E  merge       commit 92fc26b (--no-ff, after independent verification GREEN)
- [X] F  gate 3      (this commit: spec Status → Complete, run log)
RESULT: COMPLETED   consecutive_blocked = 0
Notes: root causes — (1) handler-level [Authorize] on Razor Pages handler methods is NOT
enforced in .NET 10 (10.0.3; endpoint-metadata authorization skips per-handler attributes —
minimal repro + real-app + framework source; class-level and minimal-API [Authorize] ARE
enforced); (2) ScormHelpers.GetStudentId silently substituted the seeded demo learner
(550e8400-…-0001, alice) when no claim — so every guest enroll attributed to alice and
guests saw her "Enrolled" badges; (3) no ReturnUrl handling anywhere. Fixes: explicit
ChallengeResult("Cookie") guard (ADR-0013 house pattern), Guid.Empty "no learner" sentinel,
lms.ReturnUrl cookie (24h/HttpOnly/Lax/local-URL-only at set AND consume) set on Login.OnGet,
consumed once at successful sign-in — survives signup+verify because neither touches it.
E2E red pre-fix (guest POST 200 + demo row created), green post-fix (4/4 US1). Test gotchas
hit: hx-swap="outerHTML" replaces #enroll-region itself (assert the rendered "✓ Enrolled"
state, not the id); the test is idempotent w.r.t. the persistent dev DB (branch on
already-enrolled, same pattern as 03-enrollment). Unit tests REQUIRE the
ConnectionStrings__* env vars (fixtures fail "environment variable is required" without
them). Host-side Playwright flakes the 3 SCORM-session specs: their flushScormSessions()
dials the compose hostname valkey:6379 (resolves only in the container network) — pre-existing
environmental mismatch; canonical in-container run is green (documented in tasks.md T018).
Data hygiene (dev DB only): pre-fix evidence row (alice→…115) deleted at T003; filler-clean
11,668→10 courses before each E2E gate; Valkey FLUSHALL before E2E (ephemeral SCORM bag only).
Verification (XVI, fresh no-context subagent, clean detached worktree @06ae70b): GREEN — build
0 errors; units 197/197 (55+29+14+42+39+18); filler-clean 11,668→10; probes Now listening +
/ 302 + guest /MyCourses 302; Playwright 186 passed + 1 documented skip, 0 failed (first run).
Gate 3 on master @92fc26b: build 0 errors; units 197/197; filler-clean 11,668→10; Valkey
FLUSHALL; Playwright (JSON, definitive) expected 186 / skipped 1 / unexpected 0 / flaky 0.

### Item 8 — Enrollment.Tests parallelization flake — spec 056 (bug/056-serialize-enrollment-tests)
- [X] A  spec        commit c03462d
- [X] B  plan        commit c588ae1 (research.md carries the race mechanism + per-class Scorm.Tests decision evidence)
- [X] C  tasks       commit 8911fa6
- [X] D  implement   commit 256247e   one file: tests/Enrollment.Tests/AssemblyInfo.cs (+22) — no assertion touched
- [X] E  merge       commit 978cc50   (--no-ff, after independent verification; local Playwright gate environmentally RED — diagnosis below, authoritative gates green)
- [X] F  gate 3      commit (this commit)   master CI 35817464596 SUCCESS (fresh DB, full suite, 6m36s)
RESULT: COMPLETED     consecutive_blocked = 0

Item 8 findings for final report:
- Run-2 baseline (verified at start, master @ 490d358): unit gate 197/197 (Arch 14,
  Catalog 39, Enrollment 42, Host 29, Management 55, Scorm 18); CI 35807448405 success /
  35807132600 failure / 35803849842 success — green-but-flaky, as the handoff stated.
  Co-Authored-By: Claude confirmed to be an artifact of the 2026-09-21+ Claude session
  only (no commit before 2026-09-21 carries one) — new commits do not add it.
- Root cause: xUnit class-level parallelism + two IAsyncLifetime classes
  (AdminListLearnersTests, AdminListEnrollmentsTests) each seeding 12 filler Students
  one row at a time (individual auto-committed INSERTs — non-atomic);
  empty_search_is_no_filter compares two catalog-wide dbo.AdminListLearners totals, so a
  single INSERT/DELETE landing between the reads diverges them. The assertion is the SP's
  empty==NULL contract (spec 042) and was NOT loosened.
- Pre-fix flake on record: CI run 35807132600 FAILED in empty_search_is_no_filter
  (Assert.Equal() Values differ, 1-row delta); run 35807448405 on identical test code
  succeeded.
- Gate 1 (local, supporting): build 0 errors; host Now listening 5000+7095; / → 302.
- Local unit gate ×3 (supporting): 3 × 197/197.
- Gate 2 (authoritative, XVII): branch CI 35812601046 SUCCESS (fresh DB, full suite, 6m36s).
- XVI (fresh no-context subagent, detached clean worktree @256247e): build 0 errors;
  units 197/197; target test green in isolation; fix file present; local Playwright RED —
  documented environmental root cause (WSL2 swap thrashing: 81% of 1 GiB swap used, host
  RAM 91% → intermittent 30s SQL timeouts on catalog-backed pages; the SP and the app
  respond in milliseconds when the VM is not stalling; a one-line test-only diff cannot be
  the cause). Classified Blocking—environment (XV.3); diagnosis is the required output
  (XIV), no retries burned.
- Gate 3: master CI 35817464596 SUCCESS (fresh DB, 6m36s) — authoritative. Local
  (supporting): rebuild 0 errors on merged master; host restarted on the merged build
  (Now listening ×2, / → 302); local full E2E environmentally RED (same WSL2 swap
  signature; 33 passed / 1 skipped / rest failed-or-not-run; two full attempts — XIV
  budget exhausted; per XVII the CI run is the proof, local is supporting).
- Scorm.Tests decision (documented in research.md §3, no change): all 7 lifetime classes
  scope mutations AND assertions to per-run random-GUID markers; no catalog-wide
  read-compares exist, so the defect class is absent. Revisit only if a Scorm flake
  surfaces with this signature.

## Direct-to-master commits — 2026-09-21 → 2026-09-23 (recorded 2026-09-23)

Eight code commits were pushed straight to `master` with no `bug/`/`story/` branch and
no spec — a violation of Principles VIII and X. Recorded here as fact, not excused.
They were made by the Claude Code session that also produced the 2026-09-23 handoff;
each commit message carries its own root cause and evidence, so this entry records the
governance breach and what landed, without re-litigating the technical content.

| Commit | Subject (abridged) | What it changed |
|---|---|---|
| dd8a1e6 | fix(ci): use full path to sqlcmd in Filler-clean step | ci.yml: full mssql-tools18 path (not on runner PATH) |
| 97e1812 | fix(ci): install mssql-tools18 on runner before Filler-clean step | ci.yml: apt-install mssql-tools18 (absent on ubuntu-24.04 runners) |
| a099b18 | fix(ci): npm ci in Playwright.Tests before running Playwright | ci.yml: local @playwright/test instead of npx-fetched package |
| 1c63f24 | fix(ci): run Filler-clean after the unit-test steps, not before | ci.yml: step order — unit suites dirty the shared DB, so cleanup must follow them |
| 6249955 | fix(ci): point filler-clean at LearningLms and fail on SQL errors | ci.yml: sqlcmd -d LearningLms -b (was cleaning nothing, exiting 0) |
| f0ba9d6 | test(catalog): serialize Catalog.Tests classes on the shared database | Catalog.Tests/AssemblyInfo.cs — the fix this run's spec 056 extends to Enrollment.Tests |
| 1748f50 | test(e2e): resolve Valkey from the connection string, not the compose host | 3 SCORM specs: flushScormSessions reads ConnectionStrings__Valkey, fallback valkey:6379 |
| a03eee2 | test(036): build the org acceptance hierarchy instead of assuming it | 06-admin-organizations.spec.ts: beforeAll creates missing hierarchy via admin API (XVII.1) |

Notes:
- `ea2f426` (constitution amendment to v1.9.0) is NOT counted among the violations:
  constitution amendments follow the established master-direct precedent (this file's
  own Governance section is amended on master with a version bump).
- `490d358` ("Updated constitution and CLAUDE.md" — CLAUDE.md + .pi agent skills +
  .specify/extensions.yml, docs/tooling, no code) also landed on master outside a spec
  cycle; noted for completeness, same category as the run-log/docs commits this file
  has always received on master.
- Deliberately NOT back-dated into a spec: the handoff's instruction, and the correct
  one — a retroactive spec 056 would have given the paper benefit the rule exists to
  prevent, and the commit messages already carry root cause and evidence.
- Going forward (this run): spec 056 and spec 057 both ran the full branch + spec
  cycle, including CI as the authoritative gate per Principle XVII.

### Item 9 — Stale agent-facing docs after constitution v1.9.0 — spec 057 (bug/057-fix-stale-agent-docs)
- [X] A  spec        commit a3c0ca7
- [X] B  plan        commit d0fd556
- [X] C  tasks       commit d0fd556 (plan+tasks together — docs slice, no research artifact needed)
- [X] D  implement   commit 7c3a6ed   two files: CLAUDE.md + specs/036-org-tree-branching/quickstart.md (25 ins / 14 del; zero code)
- [X] E  merge       commit e2b7e1d   (--no-ff, after independent verification GREEN)
- [X] F  gate 3      commit (this commit)   master CI 35822706866 SUCCESS (fresh DB, full suite, 6m45s)
RESULT: COMPLETED     consecutive_blocked = 0

Item 9 findings for final report:
- Closed BOTH deferred items of the v1.9.0 Sync Impact Report (ea2f426): (a) XVII added
  to CLAUDE.md §0's principle list; (b) 036 quickstart no longer makes the E2E gate
  depend on hand-built fixture data (tests self-build since a03eee2 — XVII.1).
- Rewrote the stale Valkey gotcha to the post-1748f50 reality (env var with
  compose-hostname fallback; warning conditional on the export) and dropped the
  "in-container run is canonical" conclusion in favor of the constitution's
  authoritative-run statement (ci.yml per Development Workflow; XVII).
- Gates: build 0 errors (the session's earlier 18-error builds were MSB3027 file locks
  from the running host holding bin/ DLLs — environmental, zero compile errors);
  branch CI 35820076981 SUCCESS (6m28s); XVI independent verification GREEN — all seven
  factual claims re-verified with line-level citations (flushScormSessions pattern at
  14:34/15:45/20:32, introduced by 1748f50 per git log -S; recovery-path-only invocation
  in all three specs; constitution Development Workflow + XVII clauses quoted;
  06-admin-organizations beforeAll ensure-if-missing :146–:169 from a03eee2; diff = 2
  .md files only); master CI 35822706866 SUCCESS (6m45s).
- Nuance recorded by the verifier (not an error): the constitution names the
  devcontainer run specifically as "supporting evidence"; CLAUDE.md generalizes to any
  local run — consistent with XVII's "never the long-lived development database".
