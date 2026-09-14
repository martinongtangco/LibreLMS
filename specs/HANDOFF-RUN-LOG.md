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
- [ ] E  merge       commit
- [ ] F  gate 3      commit
RESULT: (pending)     consecutive_blocked = 0
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
