# Tasks: SCORM Package Upload — Zip Slip + Zip Bomb

**Input**: Design documents from `/specs/049-fix-scorm-zip-traversal/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, quickstart.md

**Organization**: Tasks are grouped by user story; US1 (traversal) and US2 (caps)
are independently testable; US3 is the regression guard that both must not break.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g. US1, US2, US3)

## Phase 1: Setup

- [X] T001 Create branch `bug/049-fix-scorm-zip-traversal` from `master` (Constitution VIII)

## Phase 2: Foundational

(none — no new projects, packages, migrations, or infrastructure)

## Phase 3: User Story 1 — Traversal rejection (Priority: P1) 🎯

**Goal**: No archive entry — file or directory — can create content outside
`wwwroot/scorm-content/{packageId}`; a bad entry rejects the whole upload and
leaves no partial directory.

**Independent Test**: `dotnet test tests/Scorm.Tests --filter ScormUploadTraversalTests`
— traversal tests green; no file/directory exists outside the temp wwwroot.

### Tests for User Story 1 ⚠️ (written FIRST, red-verified against current code)

- [X] T002 [P] [US1] In `tests/Scorm.Tests/ScormUploadTraversalTests.cs` (house pattern: real MSSQL via `ConnectionStrings__Sql`, `MigrateAsync()` in `InitializeAsync`, own rows cleaned up, temp dir as wwwroot, in-memory `ZipArchive` with a passing `imsmanifest.xml`): (a) file entry `../escape.txt` → upload rejected + no `wwwroot/escape.txt` + no partial `scorm-content/{id}`; (b) directory entry `../evil-dir/` + file under it → rejected + no `wwwroot/evil-dir`; (c) rooted entry `C:\scorm-escape\evil.txt` → rejected + nothing at that path
- [X] T003 [US1] RED-VERIFY: run T002's tests against the unmodified `UploadAsync`, capture the failing output (files written outside / no error returned), record in this file under Verification Notes

### Implementation for User Story 1

- [X] T004 [US1] In `src/Modules/Scorm/Application/ScormPackageService.cs` `UploadAsync`: before the extraction loop, compute `contentRoot = Path.GetFullPath(contentFullPath) + Path.DirectorySeparatorChar`; inside the loop, for BOTH the directory-entry and file-entry branches, compute `resolved = Path.GetFullPath(Path.Combine(contentFullPath, entry.FullName))` and, if `!resolved.StartsWith(contentRoot, StringComparison.Ordinal)`, clean up (`Directory.Delete(contentFullPath, true)` best-effort) and `return (null, "SCORM package rejected: entry '<FullName>' escapes the content directory.")` — before any directory creation or file write for that entry

**Checkpoint**: US1 green — traversal tests pass, no partial dirs, error flows to `Results.BadRequest` via the existing `(package, error)` tuple (no endpoint change)

## Phase 4: User Story 2 — Decompression caps (Priority: P2)

**Goal**: Entry-count and uncompressed-size caps reject oversized packages with
clear messages and cleanup.

**Independent Test**: cap tests green with tiny constructor-supplied caps.

### Tests for User Story 2 ⚠️

- [X] T005 [P] [US2] In `tests/Scorm.Tests/ScormUploadTraversalTests.cs`: (a) size cap — constructor `maxUncompressedBytes: 1_000` with a zip totaling a few KB → rejected with the size message + partial dir cleaned; (b) entry-count cap — constructor `maxEntryCount: 2` with a 3-entry zip → rejected with the count message

### Implementation for User Story 2

- [X] T006 [US2] In `ScormPackageService`: add `DefaultMaxEntryCount` (5_000) and `DefaultMaxUncompressedBytes` (100_000_000) named constants; optional constructor params `int maxEntryCount = DefaultMaxEntryCount, long maxUncompressedBytes = DefaultMaxUncompressedBytes`; in `UploadAsync` — reject up front if `archive.Entries.Count > maxEntryCount` (before `Directory.CreateDirectory`, no cleanup needed), and while extracting track the running `entry.Length` sum, rejecting (with the US1 cleanup path) when it exceeds `maxUncompressedBytes`; thread both values through `src/Modules/Scorm/Endpoints/ScormModuleExtensions.cs` `ConfigureScormModule` as optional params (Host unchanged)

**Checkpoint**: US2 green — both cap tests pass with the same cleanup guarantees as US1

## Phase 5: User Story 3 — Legitimate packages unaffected (Priority: P3)

**Goal**: Well-formed nested SCORM packages still extract and load.

**Independent Test**: nested-package test green + full Playwright suite green
(seed packages under `wwwroot/scorm-content/` load; SCORM launch specs pass).

- [X] T007 [P] [US3] In `tests/Scorm.Tests/ScormUploadTraversalTests.cs`: well-formed zip with nested folders (`imsmanifest.xml`, `content/index.html`, `content/img/logo.png`) → upload succeeds, files present at expected relative paths under the content dir, `ScormPackages` row created (then cleaned up)

## Phase 6: Polish & Verification Gates

- [X] T008 Gate 1: `dotnet build LibreLms.slnx` (0 errors) + `./scripts/restart-app.sh --background` (`Now listening on:` line) — paste evidence in Verification Notes
- [X] T009 Gate 2: `dotnet test tests/ArchitectureTests`, `dotnet test LibreLms.slnx`, `cd tests/Playwright.Tests && npx playwright test` — expect 170 passed + 1 documented skip (verify-email) — paste evidence
- [X] T010 Independent verification (Constitution XVI): fresh subagent re-runs build + Playwright from a clean worktree checkout of `bug/049-fix-scorm-zip-traversal` and reports independently; merge to master only after it is green (`git merge --no-ff`)
- [X] T011 Gate 3 (post-merge, on master): rebuild, restart, re-run gate 2 — paste evidence; mark all tasks `[X]`, set spec Status to Complete, commit F

## Verification Notes

### T003 — RED verification (pre-fix code, 2026-08-31)

```
dotnet test tests/Scorm.Tests --filter "FullyQualifiedName~ScormUploadTraversalTests"

  Failed Scorm.Tests.ScormUploadTraversalTests.FileEntryWithDotDot_IsRejected_AndWritesNothingOutsideContentDir [213 ms]
  Error Message:
   Assert.NotNull() Failure: Value is null
  Failed Scorm.Tests.ScormUploadTraversalTests.DirectoryEntryWithDotDot_IsRejected_AndCreatesNoDirectoryOutsideContentDir [27 ms]
  Error Message:
   Assert.NotNull() Failure: Value is null
  Failed Scorm.Tests.ScormUploadTraversalTests.RootedEntryName_IsRejected_AndWritesNothingOutsideContentDir [19 ms]
  Error Message:
   Assert.NotNull() Failure: Value is null

Failed!  - Failed:     3, Passed:     0, Skipped:     0, Total:     3
```

All three fail at `Assert.NotNull(error)` — the pre-fix `UploadAsync` returns
no error: the traversal entries are extracted outside the content directory
(the `../escape.txt` file and `C:/scorm-escape-…/evil.txt` file are written to
their resolved absolute paths; the tests' finally-blocks remove them). Red
confirmed: the vulnerability is real and the tests catch it.

(Two local compile fixes to the new test file during this step — missing
`using LibreLms.Modules.Scorm.Domain;` and xUnit v2's `Assert.Empty`/
`Assert.Equal` taking no message argument. Production code untouched.)

### T008 — Gate 1 (2026-08-31)

```
dotnet build LibreLms.slnx → 48 Warning(s) (all NU1903, pre-existing), 0 Error(s)
App (in devcontainer, compose DNS, DB LearningLms):
  Now listening on: http://localhost:5000
  Application started. Press Ctrl+C to shut down.
  container-local probe: HTTP 302 (login redirect)
```

Environment note: the E2E suite is designed to run INSIDE the devcontainer
(14/15-scorm tests do `net.connect(6379, 'valkey')` — compose DNS only
resolves in-container; the app must be at localhost:5000 in-container). The
app was relaunched in the devcontainer as root (host-owned obj/ files are
not writable from the container via the Docker Desktop bind mount — stale
obj/bin deleted first). Browsers staged at /ms-playwright
(PLAYWRIGHT_BROWSERS_PATH). Unit suites run on the host with
ConnectionStrings__Sql pointing at localhost:1433.

### T009 — Gate 2 (2026-08-31)

Unit (host, DB LearningLms):
```
ArchitectureTests 14/14 · Host.Tests 8/8 · Catalog.Tests 32/32 ·
Scorm.Tests 15/15 (6 new traversal/cap tests) ·
Enrollment.Tests 41/42 — the 1 failure is PRE-EXISTING ON MASTER
(AdminListLearnersTests.never_exposes_credential_columns asserts 8 SP
columns; spec 042's migration 20260829105050 re-created the SP with 9 —
reproduced identically in a clean master worktree checkout). Recorded as
future spec candidate in the final report.
```

E2E (devcontainer, DB LearningLms after filler cleanup — see below):
```
170 passed, 1 skipped (documented verify-email skip)  ← baseline match
```

Environment forensics during this gate (all pre-existing, none caused by
this change):
- 11,668 filler courses + 96 dependent enrollments accumulated in
  LearningLms by repeated unit-suite runs (spec 048's documented "11.6K
  perf/count filler" class) broke 8 exact-count E2E tests. Removed with a
  GUID-precise delete (seeded courses carry fixed GUIDs 11111111-…; dry-run
  first, kept all seeded rows, the 1 seeded SCORM package, all orgs/users).
- 19-course-visibility failed once in a parallel run: 16-admin-pagination
  creates 'AdmPg032C' filler courses mid-run that push the target course
  off page 1 (12/page) — pre-existing parallel-isolation flake; passes
  standalone and in the clean final run.
- The app/tests only run if ConnectionStrings__Sql + ConnectionStrings__Valkey
  are supplied from the environment — neither key exists in any repo file
  (item 3 of the hardening queue).

### T010 — Independent verification (Constitution XVI, fresh subagent, 2026-08-31)

VERDICT: GREEN (independent report, no shared context):
- Clean worktree build: 0 Error(s).
- Unit from worktree: ArchitectureTests 14/14, Scorm.Tests 15/15; the 6
  ScormUploadTraversalTests all pass (file/directory/rooted traversal, size
cap, count cap, legitimate nested package).
- Behavioral probes against the running app: malicious `../` upload rejected
  with "entry … escapes the content directory", nothing written outside the
  content dir; 5,002-entry zip rejected with the count-cap message.
- Full E2E (in-container, serial CI=1 mode): 170 passed, 1 documented skip.
- The bare (non-CI) E2E command hits a pre-existing parallel-isolation race
  (16-admin-pagination filler courses vs 19-course-visibility small-catalog
  assumption) — reproduced 2× by the verifier, passes in serial; pre-existing
  infra defect, recorded as future spec candidate, NOT caused by 049.
- New finding (pre-existing, out of scope, future spec candidate):
  `POST /api/scorm/upload` 500s on every request — the minimal API binds
  IFormCollection (implicit anti-forgery metadata) but `app.UseAntiforgery()`
  is never called (absent from all commits); the API surface is fail-closed
  (it never reaches UploadAsync), the Razor-page upload surfaces work.
- Merged to master: `git merge --no-ff` → 975eb00.

### T011 — Gate 3 (post-merge, master, 2026-08-31)

```
dotnet build LibreLms.slnx → 0 Error(s)
App restarted in devcontainer (merged code): Now listening on: http://localhost:5000, HTTP 302
Unit: ArchitectureTests 14/14 · Host.Tests 8/8 · Catalog.Tests 32/32 ·
      Scorm.Tests 15/15 · Enrollment.Tests 41/42 (1 pre-existing master failure)
E2E (in-container, CI=1 serial, DB cleaned to 10 seeded courses):
      170 passed, 1 skipped (documented verify-email skip)  ← baseline match
```
