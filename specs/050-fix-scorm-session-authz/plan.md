# Implementation Plan: SCORM Session API — Authentication + Ownership

**Branch**: `bug/050-fix-scorm-session-authz` | **Date**: 2026-08-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/050-fix-scorm-session-authz/spec.md`

## Summary

Close the two holes in the SCORM session surface: (1) require authentication on
the `/api/scorm/session/{sessionId:guid}` group (401 for anonymous callers),
and (2) make the four service methods owner-checked — the caller's student id
is passed in and compared to the session's `StudentId` before any read/write;
a mismatch returns a JSON 403 (house pattern, NOT `Results.Forbid()`). The
`api.js` shim stays anonymous.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: none new (cookie auth, minimal APIs, existing store)
**Storage**: Valkey session bag (read for the ownership check — no new keys); MSSQL untouched by the check itself
**Testing**: xUnit real-MSSQL+real-Valkey unit tests (Scorm.Tests house pattern) + new Playwright spec over real HTTP
**Constraints**: `15-scorm-launch-ui.spec.ts` and the whole E2E suite must stay green — the shim's same-origin fetch/XHR/sendBeacon calls carry the auth cookie by default, so the owner flow is unchanged; the launch page's `beforeunload` sendBeacon commit must keep working

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I/II**: change stays in Scorm Application (service + result records) and the Host's endpoint wiring; explicit checks, one-sentence explainable ("the service refuses to act on a session whose stored owner is not the caller"). ✅
- **III**: `ScormSessionService` is a concrete class with no Contracts surface (verified: no `IScormSessionService` exists); the four call sites are all in Program.cs. No boundary change. ✅
- **IV**: no architectural decision (authz enforcement location is dictated by the handoff: service-level ownership + endpoint-level authentication). No ADR required. ✅
- **XIII**: gates with evidence; red-verify the unit + E2E authz tests first. ✅
- **XIV/XV**: 2-attempt ceiling with triage. ✅

**Post-design re-check**: PASS (no new dependencies, no boundary changes).

## Project Structure

### Documentation (this feature)

```text
specs/050-fix-scorm-session-authz/
├── plan.md
├── research.md
└── quickstart.md
```

No `data-model.md` (no entity change) or `contracts/` (no interface change —
the method signatures change on a concrete class with 4 call sites, all in
Program.cs).

### Source Code (files touched)

```text
src/Host/Program.cs                                   # group RequireAuthorization() + studentId plumbing + 403 JSON mapping
src/Modules/Scorm/Application/ScormSessionService.cs  # ownership check in the 4 methods + Forbidden results
tests/Scorm.Tests/ScormSessionOwnershipTests.cs       # new: unit, red-verified
tests/Playwright.Tests/tests/20-scorm-session-authz.spec.ts  # new: E2E over HTTP, red-verified
```

## Design

### Endpoint layer (Program.cs)

1. `var sessionGroup = app.MapGroup("/api/scorm/session/{sessionId:guid}").RequireAuthorization().WithTags("Scorm Session");`
   The `api.js` route is a separate `app.MapGet` (not in the group) and keeps
   `.DisableAntiforgery()` — untouched, stays anonymous (spec: it serves
   static script text, no session data).
2. Each of the four handlers gains `HttpContext httpContext` and computes
   `var studentId = GetStudentId(httpContext);` (same helper the launch and
   attempts endpoints use; with `RequireAuthorization()` the demo fallback in
   `GetStudentId` is unreachable here — a 401 precedes it).
3. Forbidden mapping — **JSON 403, not `Results.Forbid()`** (documented house
   pattern from the launch endpoint: cookie auth turns `Forbid()` into a 302
   to AccessDeniedPath, which clients misparse):
   - setValue → `Results.Json(new { success = false, errorCode = "403", errorMsg = "Not authorized to access this session." }, statusCode: 403)`
   - getValue → `Results.Json(new { error = "Not authorized to access this session." }, statusCode: 403)`
   - commit → `Results.Json(new { success = false, error = "Not authorized to access this session." }, statusCode: 403)`
   - finish → same shape as commit
   The message is generic — it does not say whether the session exists.
4. Not-found behavior unchanged (404 via the existing result mapping).

### Service layer (ScormSessionService)

All four methods gain a `Guid studentId` parameter (after `sessionId`):

```text
SetValueAsync(Guid sessionId, Guid studentId, string element, string value)
GetValueAsync(Guid sessionId, Guid studentId, string element)
CommitAsync(Guid sessionId, Guid studentId)
FinishAsync(Guid sessionId, Guid studentId, string exitReason = "normal")
```

Each method starts with the ownership gate (SetValue/GetValue gain a session
read they didn't previously do; Commit/Finish already read the session — the
check slots in after the null check):

```text
var sessionData = await _sessionStore.ReadSessionAsync(sessionId);
if (sessionData is null)
    return <existing not-found result>;            // 404, unchanged
if (!Guid.TryParse(sessionData.StudentId, out var ownerId) || ownerId != studentId)
    return <CreateForbidden result>;               // 403 — no CMI read, no write
```

- `SessionData.StudentId` is a string (GUID text); `Guid.TryParse` + equality
  is separator/case-insensitive by construction; a malformed owner string is
  treated as not-the-caller (fail closed).
- The gate runs **before** element validation and before any store write, so a
  non-owner learns nothing (no "unknown element", no partial state).
- `SetValueAsync`'s store call still returns the "404 session not found" error
  for the owner-only race (session expired between read and write) — unchanged.

Result records: add `public bool Forbidden { get; init; }` + a
`CreateForbidden()` factory to `SetValueResult`, `GetValueResult`,
`CommitResult`, `FinishResult`.

### Shim/iframe hazard (handoff note)

The shim (`ScormHelpers.ScormApiScriptContent`) calls the endpoints via
`fetch`/`XMLHttpRequest`/`sendBeacon` with relative same-origin URLs — browsers
send cookies on same-origin requests by default, and the launch page's
`beforeunload` beacon is same-origin too. No shim change expected; the E2E
guard is `15-scorm-launch-ui` (real iframe launch) plus the new spec 20. If
that spec or 15 fails after the change, the cookie is not flowing and fixing
that is in scope for this item.

## Testing Strategy

**Unit — `tests/Scorm.Tests/ScormSessionOwnershipTests.cs`** (house pattern:
real MSSQL via `ConnectionStrings__Sql` + real Valkey; own seed rows cleaned
up; a session is created via `LaunchAsync` for a freshly enrolled learner, or
directly via the store + attempt row):

1. RED (pre-fix, all four): student B's `SetValueAsync`/`GetValueAsync`/
   `CommitAsync`/`FinishAsync` against student A's session returns the
   forbidden result AND A's state is unchanged. (Pre-fix: B succeeds — red.)
2. Owner path: A's own calls succeed (setValue/getValue round-trip, commit
   updates the attempt, finish completes + deletes the session).
3. Malformed/absent session: random GUID → existing not-found results
   (regression: no behavior change for 404).

**E2E — `tests/Playwright.Tests/tests/20-scorm-session-authz.spec.ts`** (RED-
verified):
1. alice (Learner) launches the seeded SCORM course via
   `POST /api/scorm/{courseId}/launch` → sessionId. (Reuse the valkey-flush
   recovery pattern from 15-scorm-launch-ui if a stale active session blocks
   the launch; finish the session at the end.)
2. bob (Learner, second authenticated context): `setValue`, `getValue`,
   `commit`, `finish` on alice's sessionId → **403** on all four.
3. Unauthenticated context: same four calls → **401** on all four.
4. alice (owner): `setValue` → 200, `getValue` round-trips, `finish` → 200
   (owner flow intact + doubles as cleanup).

**Regression guard**: full Playwright suite green (baseline 170 passed + 1
documented skip; the suite grows to 174+ passed with the new spec).
