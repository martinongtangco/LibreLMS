# Implementation Plan: Fix SCORM Launch Page (Spec 031)

**Branch**: `bug/031-fix-scorm-launch-page` | **Date**: 2026-08-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/031-fix-scorm-launch-page/spec.md`

## Summary

The SCORM launch page (`/Scorm/Launch/{courseId}`) POSTs to its own launch API with a
relative URI on an `HttpClient` that has no `BaseAddress`, so the call throws
`InvalidOperationException` and every UI launch renders the generic "Launch Failed"
error page. Fix the page's server-side call (absolute URI from the incoming request +
forwarded `Cookie` header + `[Authorize]` + logging on the catch path) and add a
Playwright E2E test that exercises the real UI launch path — the gap that let this
defect ship.

## Technical Context

**Language/Version**: C# / .NET 10 (ASP.NET Core minimal APIs + Razor Pages), pinned via `global.json`
**Primary Dependencies**: none new (`IHttpClientFactory` is already registered; `System.Net.Http` in-box)
**Storage**: no schema changes. MSSQL (`CourseAttempts` read-only here); Valkey (ephemeral SCORM session state, untouched)
**Testing**: Playwright (`tests/Playwright.Tests`) for the E2E gate; existing `dotnet test` suites unchanged
**Target Platform**: Linux container (dev sandbox), app on `http://localhost:5000` + `https://localhost:7095`
**Project Type**: web app (modular monolith, single Host process)
**Performance Goals**: N/A (one extra same-origin POST per launch, already the design)
**Constraints**: constitution gates (branch + spec, module boundaries, verification with evidence)
**Scale/Scope**: 1 Razor Page model file + 1 new E2E test file

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I (Modular Monolith)**: fix stays inside Host (composition root); no module boundary crossed — PASS
- **II (Clean Architecture)**: no new abstractions; the page keeps calling the API over HTTP (existing seam) — PASS
- **III (Module Boundaries)**: no cross-module references added; `ArchitectureTests` must stay green — PASS
- **IV (Human-Legible Code)**: change is ~10 lines with a comment explaining why the URI is built from the request; no ADR needed (no structural decision) — PASS
- **V (Sandbox)**: all work in-container — PASS
- **VII/X (Spec-Driven, No Ad-Hoc Fixes)**: spec exists (this doc), work runs on `bug/031-fix-scorm-launch-page` — PASS
- **XIII (Verification Before Claim)**: gates = (1) `dotnet build` + restart + HTTP check with log evidence, (2) new Playwright E2E passing + existing SCORM-related specs passing, (3) post-merge rebuild/restart/re-run — MANDATORY, see tasks T003-T005

## Design

### The fix (`src/Host/Pages/Scorm/Launch.cshtml.cs`)

Replace the relative-URI `PostAsync` with an explicit `HttpRequestMessage`:

```csharp
// The launch API is same-origin; build an absolute URI from the incoming
// request (the app is reachable on both http://localhost:5000 and
// https://localhost:7095, so nothing may be hardcoded). The cookie header
// carries the caller's session so the API's [Authorize] resolves the same
// student the browser authenticated.
var baseUri = new Uri($"{Request.Scheme}://{Request.Host}", UriKind.Absolute);
var launchUri = new Uri(baseUri, $"/api/scorm/{CourseId}/launch");
var request = new HttpRequestMessage(HttpMethod.Post, launchUri);
request.Headers.TryAddWithoutValidation("Cookie", Request.Headers.Cookie.ToString());
var response = await _httpClient.SendAsync(request);
```

Plus:

- `[Authorize]` attribute on `ScormLaunchModel` (FR-004) — anonymous requests get the
  cookie challenge (login redirect) instead of reaching the error branch.
- The bare `catch` MUST log the exception (FR-003) before setting the generic message,
  so the next unexpected failure is visible in the app log instead of invisible.
- 403/400/other mapping and the success path are unchanged.

### The E2E test (`tests/Playwright.Tests/tests/15-scorm-launch-ui.spec.ts`)

Follows the conventions of `14-profile-courses.spec.ts`:

- `authFixture.loginAs(page, 'Learner')` (seeded Alice, already enrolled in the seeded
  SCORM course `11111111-1111-1111-1111-111111111111`).
- **Idempotency**: before launching, flush stale ephemeral SCORM sessions (reuse the
  `flushScormSessions` pattern — raw RESP `FLUSHALL` over `node:net`; Valkey holds only
  SCORM runtime state, Constitution VI).
- **Test 1 (US1.1/US1.2)**: `page.goto('/Scorm/Launch/' + SCORM_COURSE_ID)` →
  `iframe.scorm-frame` visible with a `src` containing `/scorm-content/`; the
  `.scorm-error-page` error branch is absent; the "SCORM Session Active" status bar is
  visible. Then assert a new attempt via `GET /api/scorm/attempts/my` (count for the
  seeded SCORM course increased by 1, latest status `in-progress`).
- **Cleanup**: finish the session via `POST /api/scorm/session/{id}/finish` (id taken
  from the launch API response — the test calls the same launch API the page calls,
  or re-reads it from the page's status bar/URL) so no active session blocks other
  tests. (If reading the session id from the page is brittle, the test may instead
  launch once via the API to obtain the id after asserting the page rendered — the
  page assertion is the primary subject.)
- **Test 2 (US1.3, optional but cheap)**: as an authenticated learner, launch a course
  they are NOT enrolled in → expect the "You are not enrolled in this course." message.
  (Requires a course id without an enrollment for the learner — the seeded catalog has
  many; pick one deterministically, e.g. enroll-free check via `/api/enrollments/my`.)

### Not changed (explicitly)

- `Program.cs` endpoint wiring, `ScormSessionService`, the API shim, `Launch.cshtml`
  view markup (shim injection is the next, user-owned spec).
- The `ToLowerInvariant` course-search 500 observed during investigation — separate
  defect, out of scope.

## Project Structure

### Documentation (this feature)

```
specs/031-fix-scorm-launch-page/
├── spec.md      # this feature's spec (root cause + evidence)
├── plan.md      # this file
└── tasks.md     # implementation tasks
```

### Source (changed)

```
src/Host/Pages/Scorm/Launch.cshtml.cs          # the fix
tests/Playwright.Tests/tests/15-scorm-launch-ui.spec.ts   # new E2E (new file)
```

## Phase Notes

- **Phase 0 (research)**: complete during investigation (root cause + runtime evidence
  captured in spec.md). No further research needed.
- **Phase 1 (design)**: this plan. No contracts/ or data-model changes.
- **Phase 2 (tasks)**: see tasks.md — T001 (fix) and T002 (test) are independent files
  → parallel subagent runs (Principle XI); parent integrates and runs the verification
  gates (T003-T005).
