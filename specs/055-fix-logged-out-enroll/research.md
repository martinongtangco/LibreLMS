# Research: Fix Enrollment While Logged Out (055)

**Date**: 2026-09-20
**Spec**: [spec.md](./spec.md)

All technical unknowns from the spec's Technical Context were resolved during root-cause
investigation **against the running app** (devcontainer, `http://localhost:5000`, MSSQL
`LearningLms` DB) plus a minimal framework repro. Decisions below include rationale and the
alternatives that were rejected.

## R1 — Why does an anonymous enroll POST execute? (RC-1)

**Question**: `OnPostEnrollAsync` carries `[Authorize]`; why is a guest POST not challenged?

**Finding**: In .NET 10 (runtime `Microsoft.AspNetCore.App 10.0.3`; app built with SDK
`10.0.103` per `global.json`), an `[Authorize]` attribute placed on a **Razor Pages handler
method is not enforced** under endpoint routing. Only class-level/page-level attributes reach
the endpoint metadata the authorization middleware evaluates.

**Evidence** (reproducible):
1. Real app: guest (no cookie) `POST /Courses/Detail/{id}?handler=Enroll` → **200** with the
   enrollment result partial; an enrollment row was created (DB-verified).
2. Real app control group: guest `GET /Account/Profile` (class-level `[Authorize]`) → **302**
   to `/Account/Login?ReturnUrl=…`; guest `POST /api/enrollments` (minimal-API `[Authorize]`)
   → **401** + `Location: /Account/Login?ReturnUrl=…`. Class-level and API-level attributes
   enforce correctly.
3. Minimal repro (fresh web project, same runtime 10.0.3, no app-specific config): page with
   `OnGet` (anonymous) + `[Authorize] OnPost` → guest POST with valid antiforgery token →
   **200**, handler executed. Confirms this is framework behavior, not an app quirk.
4. Framework source (dotnet/aspnetcore `AuthorizationApplicationModelProvider`): when
   `EnableEndpointRouting` is true (the default), the attribute→filter conversion is skipped
   entirely — authorization is decided from **endpoint metadata**, and Razor Pages do not copy
   per-handler `IAuthorizeData` attributes into per-handler selector metadata.

**Decision**: Do not rely on handler-level `[Authorize]` anywhere. Enforce with an explicit
guard at the host boundary:
```csharp
// ADR-0013: handler-level [Authorize] is inert in .NET 10 — challenge explicitly.
if (User.Identity?.IsAuthenticated != true)
    return new ChallengeResult("Cookie");
```
`ChallengeResult` on a Razor Pages handler triggers the cookie handler's challenge: a 302 to
`LoginPath` (`/Account/Login`) with `ReturnUrl` = current request path+query — exactly the
required behavior. The misleading `[Authorize]` attribute is **removed** (kept, it implies a
guarantee the platform does not honor).

**Alternatives considered**:
- *Move the enroll POST to a minimal-API endpoint* (`POST /api/courses/{id}/enroll` with
  `[Authorize]`): works (API attributes are enforced), but splits one user action across two
  boundary styles and changes the HTMX flow (JSON → partial). Rejected: more moving parts for
  no behavioral gain (Principle II).
- *Custom `IAsyncAuthorizationFilter` / policy*: an extra abstraction for a one-line check.
  Rejected (Principle II).
- *Class-level `[Authorize]` on the detail page*: breaks public course browsing. Rejected.

## R2 — Which account does a guest enrollment land on? (RC-2)

**Question**: "I'm not sure which account it goes."

**Finding**: `ScormHelpers.GetStudentId(HttpContext)` falls back to the hardcoded first
seeded student when no identity claim parses:
`550e8400-e29b-41d4-a716-446655440001` (`alice@example.com`, `EnrollmentSeeder`). DB-verified:
the guest POST created `Enrollments (StudentId=550E8400-…-0001, CourseId=1111…113,
EnrolledAt=2026-09-20 03:32 UTC)`. A second copy of the same fallback exists privately in
`Account/Settings.cshtml.cs` (dead code there — the page is class-level `[Authorize]` — but the
same anti-pattern).

**Blast radius of the fallback (all guest-visible today)**:
- Course catalog cards + course page: show the demo learner's "✓ Enrolled" badges and, for
  enrolled SCORM courses, the "Launch SCORM Course" button.
- `/MyCourses` (no `[Authorize]` at all): shows the demo learner's **entire enrollment list**.

**Decision**: `GetStudentId` returns `Guid.Empty` (the codebase's existing "no learner"
sentinel — `TryEnrollAsync` already guards on it) instead of the demo GUID; the Settings
duplicate is removed. Consequences, verified in code:
- `Detail.OnGetAsync`: `IsEnrolledAsync(Guid.Empty, …)` → not enrolled; the latest-attempt
  block already requires `studentId != Guid.Empty`. Guests see the not-enrolled state (FR-008).
- `Courses/Index.cshtml.cs` `GetPagedCourses`: `GetEnrolledCourseIdsAsync(Guid.Empty, …)` →
  empty set → no badges. (The plan verifies this lookup handles `Guid.Empty` gracefully at
  implementation time; a `studentId == Guid.Empty` early-out is the safe fallback shape.)
- `MyCourses`: gets class-level `[Authorize]` (US3) so the page is never reached anonymously.
- API endpoints (`/api/enrollments`, `/api/scorm/…`): all already behind working
  `[Authorize]`/`RequireAuthorization` (specs 047–050), so the claim is always present —
  behavior unchanged; the fallback there was dead-but-dangerous code.
- The demo learner itself stays seeded (tests/demos use it, incl.
  `tests/Host.Tests/AuthClaimsTests.cs` and E2E `testUsers`) — only the *silent substitution*
  is removed (spec Assumptions).

**Alternatives considered**:
- *Keep the fallback, only block the enroll action*: leaves the read-path data leak
  (guests see alice's courses). Rejected.
- *Delete the demo learner from the seeder*: breaks seeded E2E fixtures and demo usage.
  Rejected.

## R3 — Where does the pending return address live? (FR-005/007)

**Question**: persist the URL that encouraged sign-up across sign-up + email verification.

**Journey to cover**: course page → Enroll → (challenge) `/Account/Login?ReturnUrl=/Courses/Detail/{id}…`
→ "Create an account" → `/Account/Signup` → "check your email" → (email, minutes-to-hours
later) `/Account/Verify?token=…` → "Go to sign in" → `/Account/Login` → sign in → **course page**.

**Decision**: a short-lived **cookie** `lms.ReturnUrl`:
- Set by `Login.OnGet` when the `ReturnUrl` query value is a valid local URL
  (`Url.IsLocalUrl`); overwrites any existing value (single slot, most-recent-wins); when no
  query value is present the existing cookie is left untouched (so the verify→login hop keeps
  it).
- Attributes: `HttpOnly`, `SameSite=Lax`, `Path=/`, `MaxAge=24 h`, `Secure` in production
  only (dev runs on `http://localhost:5000`).
- Consumed **once** by `Login.OnPostAsync` on successful sign-in: re-validated with
  `Url.IsLocalUrl` (defense in depth — the value is client-storable), cookie deleted, user
  redirected there; absent/invalid/expired → existing `Redirect("/")`.
- Sign-up and Verify pages need **no code changes** — the cookie persists across them.

**Rationale**: the return address must outlive the current page (email verification is an
asynchronous, out-of-band step) and stay in the visitor's browser (the verification link is
typically opened in the same browser). A cookie is the standard, zero-infrastructure mechanism.

**Alternatives considered**:
- *TempData*: single-read semantics and request-chained; the verify link is a fresh entry point
  (email click) — fragile fit. Rejected.
- *Valkey + token embedded in the verify link*: server-side state + a token threaded through
  the email link + cleanup job. Constitution VI asks "would losing this on a cache flush be
  fine?" — yes, so it doesn't justify server infrastructure; a cookie is simpler and
  equally loss-tolerant. Rejected.
- *MSSQL row*: durable store for throwaway convenience state — wrong tool (Principle VI).
  Rejected.
- *Query-string chaining only* (login → signup → verify link): the verification email link is
  generated at sign-up and would need a token parameter threaded into `RegistrationService`
  email generation — a module change for a convenience redirect. Rejected.

## R4 — How does an HTMX enroll click become a full-page redirect for guests? (FR-002)

**Question**: htmx follows the 302 challenge and would swap the sign-in page's HTML into
`#enroll-region` — not the "redirect to the login page" the user asked for.

**Decision**: in `Detail.cshtml`, render the enroll form conditionally:
- **Authenticated**: current HTMX form (`hx-post="…?handler=Enroll"`, `hx-target="#enroll-region"`).
- **Guest**: a plain `<form method="post">` (Razor Pages `asp-page`/`asp-page-handler`
  helpers, antiforgery token included). A browser form POST receiving a 302 performs a full
  page navigation to the sign-in page — exactly the requested behavior.

**Alternatives considered**:
- *Return 401 to HX-Request + JS `htmx:responseError` → `window.location`*: extra client JS,
  extra failure modes (JS disabled, htmx version behavior). Rejected.
- *Keep HTMX for everyone and accept the swap*: violates the user's explicit UX requirement.
  Rejected.

**Edge case accepted**: an authenticated user whose session expires mid-page and then clicks
Enroll via HTMX gets the challenge 302 swapped into the card (login form inside the card).
Rare; recoverable (they can sign in or navigate away); documented, not engineered around.

## R5 — Open-redirect protection (FR-006)

**Decision**: `Url.IsLocalUrl` at **both** set-time (login OnGet) and consume-time (login
OnPost). It accepts same-site paths (`/Courses/…`) and rejects absolute foreign URLs and
protocol-relative (`//evil.com`) values. Rejected values fall back to the home page. This is
the accepted house pattern for cookie-auth redirect targets (no custom allow-list needed at
this scale).

## R6 — What else must not regress?

**Verified unchanged by design**:
- API enrollments + SCORM surfaces (specs 047–050) keep working `[Authorize]`/
  `RequireAuthorization` — untouched code paths, re-run as E2E regression.
- Signed-in learner enrollment UX (HTMX swap, duplicate warning, not-found error) —
  unchanged handler body after the guard (FR-009).
- Existing login tests (`01-auth.spec.ts`) expect login-without-ReturnUrl → `/Courses`; the
  consume logic falls back to `Redirect("/")` when no cookie is present → `/` → `/Courses`.
  Behavior preserved.
- Access-denied bounce (authenticated user on `/Account/Login` with `AccessDenied` state):
  the pending cookie may be set from that bounce's `ReturnUrl` and later consumed by a
  privileged sign-in in the same browser — correct, and the learner-gets-admin-URL case is
  handled by the existing AccessDenied state (no loop: `OnGet` shows the state, does not
  redirect).

## Toolchain note

`global.json` pins SDK `10.0.103` (non-preview, per the constitution's toolchain constraint).
The machine also carries SDK `10.0.200-preview.0.26103.119` (used by the /tmp repro because
it had no `global.json`); both run on runtime `Microsoft.AspNetCore.App 10.0.3`, and both
exhibit the R1 behavior — the finding is runtime-level, not an SDK-preview artifact.

## Open items

None. All NEEDS CLARIFICATION candidates were resolved with documented defaults in the spec's
Assumptions (24 h lifetime, single-slot return address, no auto-enroll).
