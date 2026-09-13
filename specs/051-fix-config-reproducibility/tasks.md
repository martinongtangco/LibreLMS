# Tasks: Configuration Reproducibility + Committed Secret

**Input**: Design documents from `/specs/051-fix-config-reproducibility/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, quickstart.md

**Organization**: one US per defect cluster, in the handoff-mandated sequence
(new configuration path verified BEFORE the committed value is deleted).
Gate 1 runs after EVERY change, not just at the end.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g. US1, US2, US3)

## Phase 1: Setup

- [ ] T001 Create branch `bug/051-fix-config-reproducibility` from `master` (Constitution VIII)

## Phase 2: User Story 1 — one key, reproducible start (Priority: P1)

**Goal**: `GetConnectionString("Sql")` is satisfied by documented
configuration in both run modes; a fresh clone starts the app per the README.

**Independent Test**: bare `dotnet run` in the devcontainer (compose env only,
no explicit Sql export) reaches `Now listening on:`; host start works with the
documented export.

- [x] T002 [US1] `docker-compose.yml`: `ConnectionStrings__DefaultConnection` → `ConnectionStrings__Sql` (devcontainer service env; value unchanged) — **GATE 1a**: recreate devcontainer, `env | grep ConnectionStrings` shows `__Sql`, bare `dotnet run` (only `ConnectionStrings__Valkey` exported) → `Now listening on:` + HTTP 302 probe; paste evidence
- [x] T003 [P] [US1] `.devcontainer/devcontainer.json`: `postCreateCommand` → `dotnet restore LibreLms.slnx` — **GATE 1b**: run the exact command in the container → 0 errors; paste evidence
- [x] T004 [P] [US1] `src/Host/Program.cs` line 74: Valkey fallback `localhost:6379` → `localhost:6380` (compose publishes 6380; 6379 belongs to another project) — **GATE 1c**: `dotnet build LibreLms.slnx` 0 errors + in-container app restart listening; paste evidence
- [x] T005 [US1] Commit D1 (changes 1–3) — bdca2cc

## Phase 3: User Story 2 — no committed secret (Priority: P2)

**Goal**: the tracked `appsettings*.json` carry no credentials; the value is
environment-sourced in both run modes; a test guards against regression.

**Independent Test**: `AppSettingsSecretScanTests` fails on the current tree
(red), passes after the deletion (green); the app still starts in both modes
without any tracked credential.

- [x] T006 [P] [US2] In `tests/Host.Tests/AppSettingsSecretScanTests.cs`: walk up from the test base dir to the repo root (dir containing `.specify`), parse `src/Host/appsettings.json` + `appsettings.Development.json`, assert no `ConnectionStrings` value contains `Password=` (OrdinalIgnoreCase)
- [x] T007 [US2] RED-VERIFY: run the scan test → FAILS on the committed password; paste evidence
- [x] T008 [US2] `src/Host/appsettings.Development.json`: delete the `DefaultConnection` entry (keep the non-secret `Valkey` entry)
- [x] T009 [P] [US2] `README.md` "Run the host": in-container path (devcontainer env supplies `ConnectionStrings__Sql`) + host path (export the documented `ConnectionStrings__Sql` from `.env`) + SA-password rotation note (secret is in git history)
- [x] T010 [US2] **GATE 1d**: in-container app starts with compose env only (no appsettings fallback possible); host-side start with the documented export; scan test now PASSES; paste evidence
- [x] T011 [US2] Commit D2 (secret removal + scan test + README) — d344c71

## Phase 4: User Story 3 — full regression guard (Priority: P3)

**Goal**: the config changes break nothing at the gate level.

**Independent Test**: gates 1–3 green, including the clean-environment proof
from US1.

- [x] T012 Gate 2: `dotnet test tests/ArchitectureTests`, `dotnet test LibreLms.slnx`, full E2E in the devcontainer (CI=1 serial; filler-clean AFTER the last unit run) — paste evidence
- [ ] T013 Independent verification (Constitution XVI): fresh subagent re-runs build + Playwright from a clean worktree checkout of `bug/051-fix-config-reproducibility` and reports independently; merge to master only after it is green (`git merge --no-ff`)
- [ ] T014 Gate 3 (post-merge, on master): rebuild, restart, re-run gate 2 — paste evidence; mark all tasks `[X]`, set spec Status to Complete, commit F

## Verification Notes

### GATE 1a — compose key (T002)

`docker compose up -d devcontainer` recreated the container; its env now shows
`ConnectionStrings__Sql=Server=mssql,1433;Database=LearningLms;...` (no more
`__DefaultConnection`). Bare `dotnet run` in the container with ONLY
`ConnectionStrings__Valkey` explicitly exported (Sql came solely from the
compose env) → `Now listening on: http://localhost:5000`, probe 302.

### GATE 1b — devcontainer postCreate (T003)

In-container, as root:
```
dotnet restore LearningLms.slnx → MSBUILD : error MSB1009: Project file does not exist.
dotnet restore LibreLms.slnx    → restored (0 errors)
```

### GATE 1c — Valkey fallback (T004)

`dotnet build LibreLms.slnx` → 0 Error(s); in-container app restart (bare,
compose env) → `Now listening on:`, probe 302.

### T007 — red check (secret-scan test)

```
[FAIL] AppSettingsSecretScanTests.Tracked_appsettings_files_contain_no_connection_string_credentials
  src\Host\appsettings.Development.json → ConnectionStrings:DefaultConnection
Failed! - Failed: 1, Passed: 0, Total: 1
```

### GATE 1d — secret removal (T010)

- Scan test post-removal: `Passed! 1/1` (with `JsonCommentHandling.Skip` —
  same leniency as ASP.NET Core's config provider, which also loaded the
  commented file at startup).
- Host-side start with the README's exact export
  (`ConnectionStrings__Sql=Server=localhost,1433;Database=LearningLms;...`):
  `Now listening on: http://localhost:5000`, probe 302, stopped cleanly.
- In-container app: starts with compose env only (no appsettings Sql
  fallback possible), probe 302.

### T012 — gate 2 (2026-08-31)

```
dotnet test tests/ArchitectureTests → Passed! 14/14
dotnet test LibreLms.slnx:
  ArchitectureTests 14/14, Host.Tests 9/9 (incl. new secret-scan test),
  Catalog.Tests 32/32, Scorm.Tests 18/18,
  Enrollment.Tests 41/42 (1 pre-existing master failure, documented item 1)
Full E2E (devcontainer, CI=1 serial, filler-cleaned after the last unit run —
10 seeded courses): 1 skipped, 172 passed (3.6m)
```

Environment note (not a code defect): the GATE 1a container recreation wiped
the writable-layer E2E setup (browsers + system deps at /ms-playwright —
neither is in the image, volume, or Dockerfile). Restored with `npx playwright
install chromium` + `npx playwright install-deps chromium`. Candidate: stage
the E2E browsers/deps in the devcontainer Dockerfile (future spec).
