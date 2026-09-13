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

- [ ] T002 [US1] `docker-compose.yml`: `ConnectionStrings__DefaultConnection` → `ConnectionStrings__Sql` (devcontainer service env; value unchanged) — **GATE 1a**: recreate devcontainer, `env | grep ConnectionStrings` shows `__Sql`, bare `dotnet run` (only `ConnectionStrings__Valkey` exported) → `Now listening on:` + HTTP 302 probe; paste evidence
- [ ] T003 [P] [US1] `.devcontainer/devcontainer.json`: `postCreateCommand` → `dotnet restore LibreLms.slnx` — **GATE 1b**: run the exact command in the container → 0 errors; paste evidence
- [ ] T004 [P] [US1] `src/Host/Program.cs` line 74: Valkey fallback `localhost:6379` → `localhost:6380` (compose publishes 6380; 6379 belongs to another project) — **GATE 1c**: `dotnet build LibreLms.slnx` 0 errors + in-container app restart listening; paste evidence
- [ ] T005 [US1] Commit D1 (changes 1–3)

## Phase 3: User Story 2 — no committed secret (Priority: P2)

**Goal**: the tracked `appsettings*.json` carry no credentials; the value is
environment-sourced in both run modes; a test guards against regression.

**Independent Test**: `AppSettingsSecretScanTests` fails on the current tree
(red), passes after the deletion (green); the app still starts in both modes
without any tracked credential.

- [ ] T006 [P] [US2] In `tests/Host.Tests/AppSettingsSecretScanTests.cs`: walk up from the test base dir to the repo root (dir containing `.specify`), parse `src/Host/appsettings.json` + `appsettings.Development.json`, assert no `ConnectionStrings` value contains `Password=` (OrdinalIgnoreCase)
- [ ] T007 [US2] RED-VERIFY: run the scan test → FAILS on the committed password; paste evidence
- [ ] T008 [US2] `src/Host/appsettings.Development.json`: delete the `DefaultConnection` entry (keep the non-secret `Valkey` entry)
- [ ] T009 [P] [US2] `README.md` "Run the host": in-container path (devcontainer env supplies `ConnectionStrings__Sql`) + host path (export the documented `ConnectionStrings__Sql` from `.env`) + SA-password rotation note (secret is in git history)
- [ ] T010 [US2] **GATE 1d**: in-container app starts with compose env only (no appsettings fallback possible); host-side start with the documented export; scan test now PASSES; paste evidence
- [ ] T011 [US2] Commit D2 (secret removal + scan test + README)

## Phase 4: User Story 3 — full regression guard (Priority: P3)

**Goal**: the config changes break nothing at the gate level.

**Independent Test**: gates 1–3 green, including the clean-environment proof
from US1.

- [ ] T012 Gate 2: `dotnet test tests/ArchitectureTests`, `dotnet test LibreLms.slnx`, full E2E in the devcontainer (CI=1 serial; filler-clean AFTER the last unit run) — paste evidence
- [ ] T013 Independent verification (Constitution XVI): fresh subagent re-runs build + Playwright from a clean worktree checkout of `bug/051-fix-config-reproducibility` and reports independently; merge to master only after it is green (`git merge --no-ff`)
- [ ] T014 Gate 3 (post-merge, on master): rebuild, restart, re-run gate 2 — paste evidence; mark all tasks `[X]`, set spec Status to Complete, commit F

## Verification Notes

(filled during T002–T004, T007, T010, T012, T013, T014)
