# Tasks: SCORM Package Upload — Zip Slip + Zip Bomb

**Input**: Design documents from `/specs/049-fix-scorm-zip-traversal/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, quickstart.md

**Organization**: Tasks are grouped by user story; US1 (traversal) and US2 (caps)
are independently testable; US3 is the regression guard that both must not break.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g. US1, US2, US3)

## Phase 1: Setup

- [ ] T001 Create branch `bug/049-fix-scorm-zip-traversal` from `master` (Constitution VIII)

## Phase 2: Foundational

(none — no new projects, packages, migrations, or infrastructure)

## Phase 3: User Story 1 — Traversal rejection (Priority: P1) 🎯

**Goal**: No archive entry — file or directory — can create content outside
`wwwroot/scorm-content/{packageId}`; a bad entry rejects the whole upload and
leaves no partial directory.

**Independent Test**: `dotnet test tests/Scorm.Tests --filter ScormUploadTraversalTests`
— traversal tests green; no file/directory exists outside the temp wwwroot.

### Tests for User Story 1 ⚠️ (written FIRST, red-verified against current code)

- [ ] T002 [P] [US1] In `tests/Scorm.Tests/ScormUploadTraversalTests.cs` (house pattern: real MSSQL via `ConnectionStrings__Sql`, `MigrateAsync()` in `InitializeAsync`, own rows cleaned up, temp dir as wwwroot, in-memory `ZipArchive` with a passing `imsmanifest.xml`): (a) file entry `../escape.txt` → upload rejected + no `wwwroot/escape.txt` + no partial `scorm-content/{id}`; (b) directory entry `../evil-dir/` + file under it → rejected + no `wwwroot/evil-dir`; (c) rooted entry `C:\scorm-escape\evil.txt` → rejected + nothing at that path
- [ ] T003 [US1] RED-VERIFY: run T002's tests against the unmodified `UploadAsync`, capture the failing output (files written outside / no error returned), record in this file under Verification Notes

### Implementation for User Story 1

- [ ] T004 [US1] In `src/Modules/Scorm/Application/ScormPackageService.cs` `UploadAsync`: before the extraction loop, compute `contentRoot = Path.GetFullPath(contentFullPath) + Path.DirectorySeparatorChar`; inside the loop, for BOTH the directory-entry and file-entry branches, compute `resolved = Path.GetFullPath(Path.Combine(contentFullPath, entry.FullName))` and, if `!resolved.StartsWith(contentRoot, StringComparison.Ordinal)`, clean up (`Directory.Delete(contentFullPath, true)` best-effort) and `return (null, "SCORM package rejected: entry '<FullName>' escapes the content directory.")` — before any directory creation or file write for that entry

**Checkpoint**: US1 green — traversal tests pass, no partial dirs, error flows to `Results.BadRequest` via the existing `(package, error)` tuple (no endpoint change)

## Phase 4: User Story 2 — Decompression caps (Priority: P2)

**Goal**: Entry-count and uncompressed-size caps reject oversized packages with
clear messages and cleanup.

**Independent Test**: cap tests green with tiny constructor-supplied caps.

### Tests for User Story 2 ⚠️

- [ ] T005 [P] [US2] In `tests/Scorm.Tests/ScormUploadTraversalTests.cs`: (a) size cap — constructor `maxUncompressedBytes: 1_000` with a zip totaling a few KB → rejected with the size message + partial dir cleaned; (b) entry-count cap — constructor `maxEntryCount: 2` with a 3-entry zip → rejected with the count message

### Implementation for User Story 2

- [ ] T006 [US2] In `ScormPackageService`: add `DefaultMaxEntryCount` (5_000) and `DefaultMaxUncompressedBytes` (100_000_000) named constants; optional constructor params `int maxEntryCount = DefaultMaxEntryCount, long maxUncompressedBytes = DefaultMaxUncompressedBytes`; in `UploadAsync` — reject up front if `archive.Entries.Count > maxEntryCount` (before `Directory.CreateDirectory`, no cleanup needed), and while extracting track the running `entry.Length` sum, rejecting (with the US1 cleanup path) when it exceeds `maxUncompressedBytes`; thread both values through `src/Modules/Scorm/Endpoints/ScormModuleExtensions.cs` `ConfigureScormModule` as optional params (Host unchanged)

**Checkpoint**: US2 green — both cap tests pass with the same cleanup guarantees as US1

## Phase 5: User Story 3 — Legitimate packages unaffected (Priority: P3)

**Goal**: Well-formed nested SCORM packages still extract and load.

**Independent Test**: nested-package test green + full Playwright suite green
(seed packages under `wwwroot/scorm-content/` load; SCORM launch specs pass).

- [ ] T007 [P] [US3] In `tests/Scorm.Tests/ScormUploadTraversalTests.cs`: well-formed zip with nested folders (`imsmanifest.xml`, `content/index.html`, `content/img/logo.png`) → upload succeeds, files present at expected relative paths under the content dir, `ScormPackages` row created (then cleaned up)

## Phase 6: Polish & Verification Gates

- [ ] T008 Gate 1: `dotnet build LibreLms.slnx` (0 errors) + `./scripts/restart-app.sh --background` (`Now listening on:` line) — paste evidence in Verification Notes
- [ ] T009 Gate 2: `dotnet test tests/ArchitectureTests`, `dotnet test LibreLms.slnx`, `cd tests/Playwright.Tests && npx playwright test` — expect 170 passed + 1 documented skip (verify-email) — paste evidence
- [ ] T010 Independent verification (Constitution XVI): fresh subagent re-runs build + Playwright from a clean worktree checkout of `bug/049-fix-scorm-zip-traversal` and reports independently; merge to master only after it is green (`git merge --no-ff`)
- [ ] T011 Gate 3 (post-merge, on master): rebuild, restart, re-run gate 2 — paste evidence; mark all tasks `[X]`, set spec Status to Complete, commit F

## Verification Notes

(filled during T003, T008, T009, T010, T011)
