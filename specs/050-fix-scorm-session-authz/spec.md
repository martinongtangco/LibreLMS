# Bug Fix Specification: SCORM Session API Is Unauthenticated and Unowned

**Feature Branch**: `bug/050-fix-scorm-session-authz`

**Created**: 2026-08-31

**Status**: Complete

**Input**: Hardening-loop handoff, item 2 (verified against the code):

The SCORM runtime session endpoints carry no authentication and no ownership
check. In `src/Host/Program.cs` the group declared at line 369,
`/api/scorm/session/{sessionId:guid}`, maps `setValue` (371), `getValue` (382),
`commit` (393) and `finish` (403) with no `[Authorize]` attribute and no
`RequireAuthorization()` on the group. Independently,
`ScormSessionService.SetValueAsync`, `GetValueAsync`, `CommitAsync` and
`FinishAsync` never compare the session's owner to the caller, even though
`SessionData` already carries `StudentId` and `CourseId`. Any caller holding a
session GUID — authenticated as somebody else, or not authenticated at all —
can read another learner's CMI data, write their score, and mark their course
complete.

## Root Cause

Two independent holes, each sufficient on its own:

1. **No authentication**: the session group is anonymous. A session GUID is a
   bearer token in practice (122 random bits, only ever disclosed to the owner
   via the launch URL), so "anonymous + bearer GUID" means anyone who learns
   a GUID — even an unauthenticated visitor — can act on that session.
2. **No ownership check**: even an authenticated caller is never compared to
   the session's `StudentId`. `SessionData` already stores the owner; the four
   service methods simply never look at it.

Blast radius: cross-learner data access (CMI bag incl. `cmi.suspend_data`),
score tampering, and forced course completion for a victim learner.

## Fix (as specified by the handoff)

1. Require authentication on the session group (401 for unauthenticated
   callers).
2. Pass the caller's student id into the four service methods; each verifies it
   matches the session's `StudentId` **before acting**. A mismatch returns 403
   (not 404) and must not leak whether the session exists beyond what the
   owner already knows.
3. The `api.js` shim endpoint (`GET .../api.js`, `.DisableAntiforgery()`)
   stays anonymous — it serves static script text and contains no session
   data.
4. The shim's `fetch`/XHR calls are same-origin, so the auth cookie is sent by
   default — the legitimate owner flow (launch page iframe) must keep working.
   Verify this empirically: the existing SCORM E2E spec
   (`15-scorm-launch-ui.spec.ts`) must stay green. If the cookie is NOT sent
   on those calls, fixing that is part of this spec (handoff hazard note).

## Acceptance Scenarios

1. **Given** student A holds an active session, **When** student B (any other
   learner) calls `setValue`, `getValue`, `commit` or `finish` on A's session
   id, **Then** each returns 403 and no state changes (A's CMI bag and attempt
   are untouched).
2. **Given** an unauthenticated caller with a valid session id, **When** they
   call any of the four endpoints, **Then** they receive 401.
3. **Given** the session owner, **When** they run the normal SCORM flow
   (launch → setValue → commit/finish via the UI and via the API), **Then** it
   behaves exactly as before (E2E 15-scorm-launch-ui green).
4. **Given** a caller (owner or not) with a random/nonexistent session id,
   **When** they call the endpoints, **Then** the not-found behavior is
   unchanged (404) — no new error surface.
5. **Given** the anonymous `api.js` shim endpoint, **When** fetched without
   authentication, **Then** it still returns the script text (200).

## Testing Strategy

- **Unit (tests/Scorm.Tests, red-verified)**: real MSSQL + real Valkey (house
  pattern). Create a session for student A; student B calling the four service
  methods gets the forbidden result; student A still succeeds. Red-verify the
  B-is-rejected tests against the current code (B currently succeeds).
- **E2E (new `20-scorm-session-authz.spec.ts`, red-verified)**: over real
  HTTP — alice launches the seeded SCORM course (gets sessionId); bob gets 403
  on all four endpoints; an unauthenticated context gets 401; alice's owner
  calls work (200) and the session is finished for cleanup.
- **Regression guard**: full Playwright suite green (170 passed + 1 documented
  skip baseline; 15-scorm-launch-ui exercises the shim/iframe path).

## Out of Scope

- Session GUID rotation/expiry hardening, rate limiting.
- The pre-existing `POST /api/scorm/upload` anti-forgery 500 (found during
  spec 049 verification — recorded as a future spec candidate).
