# Tasks: SCORM Session API — Authentication + Ownership

**Input**: Design documents from `/specs/050-fix-scorm-session-authz/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, quickstart.md

**Organization**: US1 (ownership) and US2 (authentication) are independently
testable; US3 is the regression guard both must not break.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g. US1, US2, US3)

## Phase 1: Setup

- [x] T001 Create branch `bug/050-fix-scorm-session-authz` from `master` (Constitution VIII)

## Phase 2: Foundational

- [x] T002 [US1] In `src/Modules/Scorm/Application/ScormSessionService.cs`: add `public bool Forbidden { get; init; }` + `CreateForbidden()` factory to `SetValueResult`, `GetValueResult`, `CommitResult`, `FinishResult` (no behavior change yet — compiles green)

## Phase 3: User Story 1 — Ownership enforcement (Priority: P1) 🎯

**Goal**: A non-owner calling any of the four session methods gets the
forbidden result and no state change; the owner is unaffected.

**Independent Test**: `dotnet test tests/Scorm.Tests --filter
"FullyQualifiedName~ScormSessionOwnershipTests"` — B-rejected tests green,
owner-path green, not-found regression green.

### Tests for User Story 1 ⚠️ (written FIRST, red-verified)

- [x] T003 [P] [US1] In `tests/Scorm.Tests/ScormSessionOwnershipTests.cs` (house pattern: real MSSQL via `ConnectionStrings__Sql` + real Valkey, own rows cleaned up): create student A + student B (fresh learners, enrolled in the seeded SCORM course or via store-direct session), launch A's session; (a) B's `SetValueAsync`/`GetValueAsync`/`CommitAsync`/`FinishAsync` → forbidden result + A's CMI bag/attempt untouched; (b) A's own setValue→getValue round-trip, commit, finish all succeed
- [x] T004 [US1] RED-VERIFY: run T003's B-rejected tests against the unmodified service (B currently succeeds — capture the failing output in Verification Notes)

### Implementation for User Story 1

- [x] T005 [US1] In `ScormSessionService`: add `Guid studentId` to `SetValueAsync`/`GetValueAsync`/`CommitAsync`/`FinishAsync`; each starts with `ReadSessionAsync` → null → existing not-found result; `!Guid.TryParse(sessionData.StudentId, out ownerId) || ownerId != studentId` → `CreateForbidden()`; before element validation/writes for SetValue, before the store read for GetValue, after the null check for Commit/Finish

**Checkpoint**: US1 green at unit level; the four call sites in Program.cs
updated to pass the caller id (compile fix — endpoint behavior complete in US2)

## Phase 4: User Story 2 — Authentication on the session group (Priority: P2)

**Goal**: Unauthenticated callers get 401; the `api.js` shim stays anonymous.

**Independent Test**: new E2E spec over real HTTP — unauthenticated 401 ×4,
bob 403 ×4, owner 200 path, api.js 200 anonymous.

### Tests for User Story 2 ⚠️ (red-verified)

- [x] T006 [P] [US2] In `tests/Playwright.Tests/tests/20-scorm-session-authz.spec.ts`: alice launches the seeded SCORM course (`POST /api/scorm/{courseId}/launch`, valkey-flush recovery pattern from 15-scorm-launch-ui); bob (second authenticated context) → 403 on setValue/getValue/commit/finish; unauthenticated context → 401 on all four; alice → setValue 200, getValue round-trip, finish 200 (doubles as cleanup); anonymous `GET /api/scorm/session/{id}/api.js` → 200

### Implementation for User Story 2

- [x] T007 [US2] In `src/Host/Program.cs`: `sessionGroup` gains `.RequireAuthorization()`; the four handlers gain `HttpContext httpContext` + `var studentId = GetStudentId(httpContext);` and pass it to the service; forbidden result → `Results.Json(..., statusCode: 403)` (NOT `Results.Forbid()` — house pattern: cookie auth 302s to AccessDeniedPath); `api.js` route untouched (stays anonymous)

**Checkpoint**: US2 green — E2E 20-scorm-session-authz passes (red first)

## Phase 5: User Story 3 — Owner flow regression guard (Priority: P3)

**Goal**: The legitimate SCORM flow (launch page iframe + shim + sendBeacon
commit) is unchanged.

**Independent Test**: full Playwright suite green — especially
`15-scorm-launch-ui.spec.ts` (real iframe launch records an attempt) and the
baseline 170 passed + 1 documented skip.

- [x] T008 [P] [US3] Run the full suite; if the shim's calls 401/403 (cookie not flowing — the handoff hazard), diagnose and fix the cookie flow as part of this spec (same-origin fetch should carry it; no shim change expected)

## Phase 6: Polish & Verification Gates

- [x] T009 Gate 1: `dotnet build LibreLms.slnx` (0 errors) + app restarted in the devcontainer (`Now listening on:` + HTTP 302 probe) — paste evidence in Verification Notes
- [x] T010 Gate 2: `dotnet test tests/ArchitectureTests`, `dotnet test LibreLms.slnx`, full E2E in the devcontainer (CI=1; DB filler-cleaned first) — paste evidence
- [ ] T011 Independent verification (Constitution XVI): fresh subagent re-runs build + Playwright from a clean worktree checkout of `bug/050-fix-scorm-session-authz` and reports independently; merge to master only after it is green (`git merge --no-ff`)
- [ ] T012 Gate 3 (post-merge, on master): rebuild, restart, re-run gate 2 — paste evidence; mark all tasks `[X]`, set spec Status to Complete, commit F

## Verification Notes

### T004 — red check (2026-08-31)

`dotnet test tests/Scorm.Tests --filter "FullyQualifiedName~ScormSessionOwnershipTests"`
against the unguarded service (signatures changed, no ownership check yet):

```
[xUnit.net] Scorm.Tests.ScormSessionOwnershipTests.Intruder_allFourOperations_refused_andOwnerStateUntouched [FAIL]
  ... line 69 (Assert.True(set.Forbidden, "an intruder's setValue must be forbidden"))
Failed!  - Failed: 1, Passed: 2, Skipped: 0, Total: 3
```

The intruder test fails (B's setValue succeeds pre-fix); `Owner_fullFlow_succeeds`
and `UnknownSession_returnsNotFound_notForbidden` pass pre-fix (baseline
behavior preserved).

### T006 — E2E red check (2026-08-31)

`npx playwright test tests/20-scorm-session-authz.spec.ts` run in the
devcontainer against the **master build** (pre-fix):

```
> 95 | expect(setRes.status(), 'intruder setValue must be 403').toBe(403);
Received: 200
1 failed (intruder test), 1 did not run (serial)
```

bob's setValue on alice's session returned 200 pre-fix — the vulnerability is
reproduced end to end.

Note: post-fix, unauthenticated GETs and bodyless POSTs are challenged with a
302 to /Account/Login (cookie-auth semantics); JSON-body POSTs get a plain
401. The spec asserts the raw challenge (maxRedirects: 0) for both shapes.

### T009 — gate 1 (2026-08-31)

```
dotnet build LibreLms.slnx
Build succeeded.  54 Warning(s)  0 Error(s)
```

App rebuilt + restarted in the devcontainer: `Now listening on:
http://localhost:5000` (curl / → 302).

### T010 — gate 2 (2026-08-31)

```
dotnet test tests/ArchitectureTests → Passed! 14/14
dotnet test LibreLms.slnx:
  Host.Tests       8/8   ArchitectureTests 14/14
  Catalog.Tests    32/32 (one full-suite parallelism flake, green in isolation)
  Scorm.Tests     18/18 (15 baseline + 3 new ownership tests)
  Enrollment.Tests 41/42 (1 pre-existing master failure: AdminListLearnersTests
                         .never_exposes_credential_columns — documented in
                         item 1, NOT caused by 050)
```

Full E2E (devcontainer, CI=1 serial, filler courses cleaned first — 10 seeded
courses remain):

```
1 skipped
172 passed (2.9m)   ← baseline 170 + 2 new (20-scorm-session-authz)
```
