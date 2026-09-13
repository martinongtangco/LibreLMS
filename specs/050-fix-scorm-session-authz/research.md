# Research / Verified Facts — spec 050

No open unknowns; the handoff's analysis was re-verified against the code on
2026-08-31.

- **Anonymous group confirmed** (Program.cs): `var sessionGroup =
  app.MapGroup("/api/scorm/session/{sessionId:guid}").WithTags("Scorm Session");`
  — no `RequireAuthorization()`; the four handlers (`setValue`, `getValue`,
  `commit`, `finish`) take no identity at all.
- **No ownership check confirmed**: `ScormSessionService.SetValueAsync /
  GetValueAsync / CommitAsync / FinishAsync` never touch `SessionData.StudentId`;
  `CommitAsync`/`FinishAsync` already call `ReadSessionAsync` (the check slots
  in after the null check); `SetValueAsync`/`GetValueAsync` don't read the
  session up front (they gain a read).
- **`SessionData.StudentId` is a string** (GUID text) in the Valkey hash
  (`ScormSessionStore.SessionData` record) — compare via `Guid.TryParse` +
  equality (case/separator-insensitive); malformed owner fails closed.
- **No `IScormSessionService` contract exists** (Scorm.Contracts contains only
  `IScormPackageService` + marker): the four methods are called exactly from
  Program.cs lines 376/387/397/408 — signature changes are local to the Host.
- **403 house pattern**: the launch endpoint documents why
  `Results.Forbid()` is wrong here (cookie auth → 302 to AccessDeniedPath →
  client misparse) — use `Results.Json(..., statusCode: 403)`.
- **`GetStudentId(HttpContext)`** (ScormHelpers) is the established
  identity helper (NameIdentifier / sub claim). Its demo fallback (first
  seeded student) is unreachable behind `RequireAuthorization()`.
- **Shim is same-origin**: `ScormHelpers.ScormApiScriptContent` uses
  `fetch`/`XMLHttpRequest` with relative URLs (`/api/scorm/session/{id}/...`);
  the launch page's `beforeunload` uses same-origin `navigator.sendBeacon`.
  Browsers send cookies on same-origin requests by default → the owner flow
  keeps working. The `api.js` route is a separate `app.MapGet` outside the
  group — stays anonymous + `.DisableAntiforgery()`.
- **Test users** (tests/Playwright.Tests/utils/testUsers.ts): alice/bob/carol
  (Learner, `password123`), admin@example.com (OrgAdmin),
  admin@librelms.local (SuperUser). Seeded SCORM course id
  `11111111-1111-1111-1111-111111111111`.

## Decision: ownership check in the service, authentication at the endpoint

- **Decision**: `RequireAuthorization()` on the group (401) + owner
  comparison inside each of the four service methods (403 JSON), per the
  handoff's explicit design.
- **Rationale**: defense in depth — any future caller of the service (not just
  these endpoints) gets the ownership check; the endpoint stays a thin mapping
  of result → status.
- **Alternatives considered**:
  - Endpoint-level check only (read the session in Program.cs and compare):
    duplicates the read for Commit/Finish (which already read), leaves the
    service unsafe for future callers, and scatters security logic across 4
    handlers.
  - An authorization handler/requirement (like the Management module's
    OrgScope machinery): overkill for a single owner equality check on a
    Valkey-stored GUID.
