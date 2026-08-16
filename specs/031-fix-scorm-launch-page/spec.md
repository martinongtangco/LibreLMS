# Bug Fix Specification: SCORM Launch Page Fails for Every Course

**Feature Branch**: `bug/031-fix-scorm-launch-page`

**Created**: 2026-08-16

**Status**: Draft

**Input**: User report: "I just uploaded a sample SCORM course from scorm.com which is a
scorm2004. I created a Golf Explained Course and associated this SCORM course. Launching
it fails."

## Root Cause

`ScormLaunchModel.OnGetAsync` (`src/Host/Pages/Scorm/Launch.cshtml.cs`) calls the launch
API with a **relative** URI:

```csharp
var response = await _httpClient.PostAsync($"/api/scorm/{CourseId}/launch", null);
```

The `HttpClient` comes from a bare `IHttpClientFactory.CreateClient()`. `Program.cs`
registers it via `builder.Services.AddHttpClient()` — no `BaseAddress`, no named-client
configuration. `HttpClient` requires either an absolute request URI or a `BaseAddress`;
with a relative URI it throws:

```
System.InvalidOperationException: An invalid request URI was provided. Either the
request URI must be an absolute URI or BaseAddress must be set.
```

The page's bare `catch` swallows the exception and sets
`Error = "An error occurred while launching the course."` — so **every** SCORM course
launched from the UI renders the "Launch Failed" error page.

### Runtime evidence (reproduced against the running app, 2026-08-16)

1. As an enrolled learner, `GET /Scorm/Launch/8fe78cbd-a3fb-41c5-876e-397ebd712a0d`
   (Golf Explained Course) → HTTP 200 rendering the error branch:
   *"Launch Failed — An error occurred while launching the course."*
2. The identical call made directly (`POST /api/scorm/8fe78cbd-.../launch` with the
   authenticated cookie) → HTTP 200 with a valid session JSON
   (`sessionId`, `contentUrl`, `entry`, `attemptNumber`). The API is healthy; only the
   page's server-side call is broken.
3. Standalone repro of the exact `HttpClient` pattern
   (`AddHttpClient()` → `CreateClient()` → `PostAsync("/relative/path", null)`) throws
   the same `InvalidOperationException`.
4. `LearningLms.CourseAttempts` contains 22 attempts — **all** created by direct API
   calls from the Playwright suite (which posts to `/api/scorm/.../launch` directly).
   Zero attempts were ever created via the UI. The defect has existed since the launch
   page's first commit (`f828566`); no E2E test ever exercised the page itself.

### Companion defect in the same call (fixed by the same change)

Even with a well-formed URI, the server-side call would fail authorization: the page's
`HttpClient` does not forward the caller's session cookie, so the API's `[Authorize]`
would challenge the anonymous server-side request with a 302 to `/Account/Login`
(handled as the generic failure branch). The fix MUST forward the request's `Cookie`
header (same-origin, server-to-server call).

### Out of scope for this spec (tracked separately)

- **Shim injection**: the SCORM API shim endpoint (`/api/scorm/session/{id}/api.js`) is
  never loaded by the launch page — `Launch.cshtml` contains no `<script src>` for it
  and the model's `ApiUrl` property is dead code. No SCORM content can reach the LMS
  runtime yet. → **Separate spec** (user-owned).
- **SCORM 2004 runtime support**: the uploaded package is the Rustici "Golf Explained —
  Run-time Basic Calls" sample (SCORM 2004 3rd Edition: `adlcp_v1p3`/`imscp_v1p1`).
  The constitution explicitly scopes the LMS to a simplified SCORM 1.2 shim; SCORM 2004,
  multi-SCO sequencing, and `cmi.interactions` are out of scope. After this fix the
  course renders but cannot record progress until a dedicated SCORM 2004 story exists.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enrolled learner launches an associated SCORM course from the UI (Priority: P1)

An enrolled learner opens a course with an associated SCORM package and clicks
"Launch SCORM Course". Instead of a "Launch Failed" error page, the launch page
completes the server-side launch call, renders the course content in the iframe, and a
new course attempt is recorded.

**Why this priority**: this is the reported defect and the only way any learner can use
a SCORM course from the UI. Nothing else in the SCORM flow is reachable from the UI
until this works.

**Independent Test**: Log in as an enrolled learner, open
`/Scorm/Launch/{courseId}` for a course with a SCORM package, and verify the page
renders the iframe (not the error branch) and that a new attempt appears in
`/api/scorm/attempts/my`.

**Acceptance Scenarios**:

1. **Given** an authenticated learner enrolled in a course with an associated SCORM
   package, **When** they open `/Scorm/Launch/{courseId}`, **Then** the page renders
   the course iframe (`iframe.scorm-frame`) pointing at the package's content URL and
   the "SCORM Session Active" status bar — no error branch is shown.
2. **Given** the same launch, **When** the page has loaded, **Then** a new in-progress
   `CourseAttempt` exists for the learner/course (attempt number incremented,
   `Status = in-progress`, `StartedAt` set).
3. **Given** an authenticated learner NOT enrolled in the course, **When** they open
   the launch URL, **Then** the page shows "You are not enrolled in this course."
   (the API's 403 is mapped to this message, not a generic failure).
4. **Given** an unauthenticated request for the launch URL, **When** the page is
   requested, **Then** the request is redirected to the login page (the page model is
   `[Authorize]`d).

---

### Edge Cases

- **Stale active session**: a previous session for the learner/course still present in
  Valkey → the API returns 400 "A session for this course is already active..." and the
  page must display that API-provided message (existing 400 branch), not a generic
  failure. E2E tests must tolerate/recover from this (the suite's established pattern:
  flush the ephemeral Valkey SCORM state — Valkey holds ONLY SCORM runtime state,
  Constitution VI).
- **Either access scheme**: the app listens on both `http://localhost:5000` and
  `https://localhost:7095`. The server-side launch call must work whichever URL the
  browser used (URI is derived from the incoming request, never hardcoded).
- **Missing/empty Cookie header**: anonymous requests are handled by `[Authorize]`
  redirect before any server-side call is made; the code must not throw when the
  `Cookie` header is absent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The launch page MUST issue the launch API call as an **absolute URI**
  derived from the current request (`scheme` + `host`), so the `HttpClient` call
  succeeds regardless of which URL the browser used. No hardcoded host/port.
- **FR-002**: The launch page MUST forward the caller's `Cookie` header to the launch
  API call so the API's `[Authorize]` and student-claim resolution see the same
  session.
- **FR-003**: The launch page MUST preserve the existing error mapping: 403 → "You are
  not enrolled in this course."; 400 → the API-provided error message; any other
  non-success → the generic failure message. The bare `catch` MUST log the exception
  instead of silently swallowing it.
- **FR-004**: The launch page model MUST require authentication (`[Authorize]`), so
  anonymous requests redirect to the login page.
- **FR-005**: A Playwright E2E test MUST exercise the real UI launch path (open
  `/Scorm/Launch/{courseId}` in the browser as an authenticated, enrolled learner;
  assert the iframe renders with the package content URL, no error branch, and a new
  attempt is recorded). The test MUST be idempotent across runs (recover from stale
  active sessions; finish its own session at the end).

### Key Entities

- **CourseAttempt** (existing, unchanged): the row whose creation proves a successful
  UI launch. No schema or contract changes in this spec.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Launching a course with a SCORM package from the UI no longer shows
  "Launch Failed" for an enrolled learner (verified in the E2E test).
- **SC-002**: Each UI launch creates exactly one new `CourseAttempt` row for the
  learner/course (verifiable via `/api/scorm/attempts/my`).
- **SC-003**: The new E2E test passes against the running app and the existing
  Playwright suite remains green (Principle XIII gates).

## Assumptions

- The launch API contract (`POST /api/scorm/{courseId}/launch` and its response shape)
  is unchanged; only the page-side call is fixed.
- Bug #2 (shim injection) is a separate spec; the E2E test for this spec asserts page
  rendering + attempt creation, NOT LMS runtime communication from within the course
  content.
- The server-side call targets the same origin the browser used (no cross-origin
  considerations in this deployment).
- The `GetStudentId` claim extraction in the API works unchanged once the cookie is
  forwarded.
