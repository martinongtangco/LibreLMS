# Tasks: Fix Enrollment While Logged Out (Anonymous Enroll + Broken Return Flow)

**Branch**: `bug/055-fix-logged-out-enroll` | **Plan**: [plan.md](plan.md) | **ADR**: [0013](../../../docs/adr/0013-handler-level-page-authz-and-identity-fallback.md) (created in T002, before any code)

**Input**: Design documents from `/specs/055-fix-logged-out-enroll/` (contracts/ and
research.md carry the behavior matrices — read them first).

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Organization**: setup (branch + ADR before code — Principle X step 4), then US1 (the
security core + round trip — it is the foundation: the cookie mechanism US2 persists and
the `Guid.Empty` sentinel behavior US3 verifies both build on it), US2 (signup+verify
journey over the US1 mechanism), US3 (guest read paths), then the XIII/XVI gate sequence.
E2E red-verify (T003) runs BEFORE any fix lands.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to

## Phase 1: Setup

- [X] T001 Create branch `bug/055-fix-logged-out-enroll` from `master` (Constitution VIII)
- [X] T002 [P] `docs/adr/0013-handler-level-page-authz-and-identity-fallback.md`: ADR
      (Principle IV/X — before code) recording both structural findings with the evidence
      from research.md R1/R2: (1) handler-level `[Authorize]` on Razor Pages handler
      methods is not enforced in .NET 10 (runtime 10.0.3; endpoint-metadata-based
      authorization skips per-handler attributes — minimal repro + real-app evidence +
      `AuthorizationApplicationModelProvider` source) → house pattern: explicit
      `ChallengeResult` guards at the host boundary for handler-level authz; (2) silent
      identity fallbacks (claim → demo GUID) are an anti-pattern → house pattern: identity
      resolution yields `Guid.Empty` ("no learner") and callers decide. Context →
      decision → consequences, one page.

**Checkpoint**: branch + ADR in place; no code touched yet.

---

## Phase 2: User Story 1 — Signed-out visitor is redirected to sign-in and back (Priority: P1) 🎯 MVP

**Goal**: Anonymous enrollment is impossible (UI and raw HTTP); a signed-out Enroll click
is a full-page redirect to the sign-in page with the course as return address; after
sign-in the visitor lands back on that course and enrolls with one manual click. No
action ever runs on behalf of a demo identity (FR-001/002/003/004/009/010, SC-001/002/004).

**Independent Test**: quickstart.md Scenarios 1–2 (manual) + T003's US1 E2E block (auto):
guest raw POST → 302 to login + zero DB rows; guest UI click → full navigation to
`/Account/Login?ReturnUrl=…`; login → same course page → manual Enroll → enrolled under
the signed-in account.

### Tests for User Story 1 (TDD — written first, must FAIL pre-fix) ⚠️

- [X] T003 [US1] `tests/Playwright.Tests/tests/21-logged-out-enroll.spec.ts` — US1 block
      (fresh context = no cookies; use `testUsers` from `tests/Playwright.Tests/utils/testUsers.ts`;
      per-run unique fixtures where state is created; teardown deletes created rows):
      (a) **raw HTTP**: `request.post('/Courses/Detail/{unseeded-course}/Enroll')`-style POST
      (use the course-detail handler route `POST /Courses/Detail/{id:guid}?handler=Enroll`
      via the page's form or `request.post` on the full URL) as guest → expect redirect to
      `/Account/Login` with `ReturnUrl` containing the course path, AND a DB-free UI proof:
      reopening the course page still shows "Enroll now" (no enrollment happened);
      (b) **UI**: guest opens a course that shows "Enroll now" → click → `page.waitForURL`
      on `/Account/Login` (full navigation — NOT an in-place swap: assert
      `#enroll-region` is gone / URL changed);
      (c) **round trip**: sign in as `learnerBob` → `expect(page).toHaveURL(/\/Courses\/Detail\//)`
      (the same course) → "Enroll now" still visible (NOT auto-enrolled — FR-004) → click →
      enrolled state visible; (d) **no demo attribution**: guest opens course
      `11111111-1111-1111-1111-111111111112` (demo learner IS enrolled) → must show
      "Enroll now", not "✓ Enrolled" (pre-fix it shows the demo learner's state).
      Run against the CURRENT (pre-fix) build: (a)/(b)/(d) must FAIL (guest enroll
      executes 200, no redirect, demo badge visible) — paste the RED evidence, then clean
      up any rows the pre-fix run created (quickstart.md §Data hygiene).
- [X] T004 [P] [US1] `tests/Host.Tests/ReturnUrlCookieTests.cs` — unit tests (written
      first; compile-level red until T005 exists), house fake `DefaultHttpContext`
      pattern: set with valid local URL writes cookie `lms.ReturnUrl` (URL-encoded,
      `HttpOnly`, `SameSite=Lax`, `MaxAge=24h`); set with `https://evil.example/` or
      `//evil.example/` writes nothing; set without query value leaves an existing cookie
      untouched (the verify → "Go to sign in" hop); second set overwrites (single slot);
      consume returns the value AND deletes the cookie; consume of a forged/non-local
      cookie value returns null and deletes it (re-validation at consume-time);
      `Secure` flag asserted true under a production host / false under
      `http://localhost:5000`.

### Implementation for User Story 1

- [X] T005 [US1] `src/Host/Pages/Courses/Detail.cshtml.cs`: in
      `OnPostEnrollAsync`, replace the inert handler-level `[Authorize]` attribute with an
      explicit guard as the FIRST statement:
      `if (User.Identity?.IsAuthenticated != true) return new ChallengeResult("Cookie");`
      + comment citing ADR 0013 (handler-level `[Authorize]` is inert in .NET 10 —
      verified). Keep the existing `Guid.Empty` guard in `TryEnrollAsync` as defense in
      depth. Remove the `[Authorize]` attribute (it implies a guarantee the platform does
      not honor).
- [X] T006 [P] [US1] `src/Host/ScormHelpers.cs`: `GetStudentId(HttpContext)` — remove the
      hardcoded demo fallback (`550e8400-…-0001`); no parseable claim → return
      `Guid.Empty` (the codebase's existing "no learner" sentinel — `TryEnrollAsync`
      already guards on it). Update the XML doc: this method never substitutes an
      identity (ADR 0013). API callers are all behind working `[Authorize]`/
      `RequireAuthorization` (verified research.md R2) — behavior unchanged for them.
- [X] T007 [US1] `src/Host/ReturnUrlCookie.cs` (NEW, one small static helper —
      Principle II): `const string CookieName = "lms.ReturnUrl";` +
      `SetPending(HttpContext, string? returnUrlQueryValue)` (validate `Url.IsLocalUrl`,
      write cookie: `HttpOnly`, `SameSite=Lax`, `Path=/`, `MaxAge=TimeSpan.FromHours(24)`,
      `Secure` when host is production; no value / invalid value → no-op) +
      `ConsumePending(HttpContext): string?` (read, re-validate with `Url.IsLocalUrl`,
      delete the cookie, return the value or null). No other members — anything more is
      over-engineering for this slice.
- [X] T008 [US1] `src/Host/Pages/Account/Login.cshtml.cs`: `OnGet` — call
      `ReturnUrlCookie.SetPending(HttpContext, Request.Query["ReturnUrl"].ToString())`
      (covers the challenge bounce; no-op when the query is absent so the verify → login
      hop keeps a pending value); `OnPostAsync` success path — replace
      `Redirect("/")` with `Redirect(ReturnUrlCookie.ConsumePending(HttpContext) ?? "/")`.
      No change to the `AccessDenied` (already-authenticated) branch.
- [X] T009 [US1] `src/Host/Pages/Courses/Detail.cshtml`: conditional enroll form inside
      `#enroll-region` — `@if (User.Identity?.IsAuthenticated == true)` → the existing
      HTMX form (`hx-post="…?handler=Enroll"`, `hx-swap="outerHTML"`, `hx-target`);
      else → plain `<form method="post">` with `asp-page`/`asp-route-id`/
      `asp-page-handler="Enroll"` (form tag helper emits the antiforgery token) and the
      same button (`btn btn-primary`, "Enroll now"). A guest's form POST receiving the
      302 challenge performs a full page navigation — the required UX (research.md R4).
      (Verified: a GET of the post-login URL `…?handler=Enroll` renders the course page
      normally — the handler query is inert on GET.)
- [X] T010 [US1] Restart the app (in-container); re-run T003 US1 block → GREEN; run
      quickstart.md Scenario 1 (raw HTTP: expect `302
      http://localhost:5000/Account/Login?ReturnUrl=%2FCourses%2FDetail%2F…`) and
      Scenario 2 (round trip + manual re-enroll); DB check (quickstart §Scenario 1 data
      check) shows zero new rows attributed to `550e8400-…-0001` after the fix. Paste
      evidence.

**Checkpoint**: US1 fully functional and independently testable — anonymous enroll is
impossible, and the signed-out → sign-in → course → manual-enroll round trip works. MVP.

---

## Phase 3: User Story 2 — New user's sign-up journey returns them to the course (Priority: P2)

**Goal**: the pending return address survives sign-up + email verification (the cookie
persists across Signup and Verify — neither page touches it) and is consumed by the
sign-in after verification, landing the new user on the original course page
(FR-005/006/007, SC-003).

**Independent Test**: quickstart.md Scenario 3 (manual, dev outbox for the link) + T011's
US2 E2E block: guest → Enroll → sign-in → Create account → verify → sign in → original
course page with "Enroll now" → manual enroll succeeds.

### Tests for User Story 2

- [X] T011 [US2] `tests/Playwright.Tests/tests/21-logged-out-enroll.spec.ts` — US2 block
      (append to the T003 file; reuse the `signUp`/`getVerifyLink` patterns from
      `tests/Playwright.Tests/tests/verify-email.spec.ts`, incl. `GET /api/dev/outbox`
      for the verification link; per-run unique email/name): guest on course
      `C = /Courses/Detail/{id}` → Enroll → `/Account/Login?ReturnUrl=C` → "Create an
      account" → sign up → "Check your email" → open the dev-outbox verify link →
      verified → "Go to sign in" → sign in with the NEW account → `expect(page).toHaveURL(C)`
      (NOT `/` — pre-fix this lands on home) → "Enroll now" visible → click → enrolled.
      Plus **J5 tamper case**: fresh context, `page.goto('/Account/Login?ReturnUrl=https://evil.example/')`
      → assert no `lms.ReturnUrl` cookie exists (`context.cookies()`) → sign in → lands on
      `/` (home), never the foreign URL. Run → GREEN (mechanism comes from US1; this
      block pins the journey contract). 24 h expiry is unit-pinned in T004 (`MaxAge`) —
      real-clock expiry is out of E2E reach (same note as spec 027's expired-link case).

### Implementation for User Story 2

- [X] T012 [US2] Verify no changes are needed on `src/Host/Pages/Account/Signup.cshtml.cs`
      and `src/Host/Pages/Account/Verify.cshtml.cs`: confirm neither reads nor clears
      `lms.ReturnUrl` (they don't — the cookie persists by design, data-model.md
      state-transition notes); record the confirmation in Verification Notes. If either
      file had been changed to touch the cookie, that would be a design deviation — stop
      and re-check the contract (contracts/return-url.md journey J2).

**Checkpoint**: US2 independently testable — the signup+verify journey lands on the
original course (journey J2 green).

---

## Phase 4: User Story 3 — Signed-out visitors see only public course info (Priority: P3)

**Goal**: guest read paths show the not-enrolled state (no other user's badges/progress),
and My Courses requires sign-in with a return to My Courses (FR-008, SC-005).

**Independent Test**: quickstart.md Scenario 4 (manual) + T016's US3 E2E block: guest
`/MyCourses` → 302 to `/Account/Login?ReturnUrl=%2FMyCourses` → sign in → back on
`/MyCourses` (own list); guest catalog + demo-enrolled course `…111111111112` show no
"Enrolled" badge, no "Launch SCORM Course" button, "Enroll now" offered.

### Implementation for User Story 3

- [X] T013 [US3] `src/Host/Pages/MyCourses/Index.cshtml.cs`: class-level `[Authorize]`
      on `MyCoursesModel` (class-level IS enforced — verified research.md R1 control
      group) → guests get the cookie challenge with `ReturnUrl=/MyCourses`; the signed-in
      visitor's own list renders after sign-in (journey J4).
- [X] T014 [P] [US3] `src/Host/Pages/Account/Settings.cshtml.cs`: remove the private
      `GetStudentId()` demo-fallback duplicate (lines ~107–114) — return the claim or
      `Guid.Empty` (the page is class-level `[Authorize]`d, so the claim is always
      present; the fallback was dead-but-dangerous code — ADR 0013).
- [X] T015 [US3] `src/Host/Pages/Courses/Index.cshtml.cs`: in `GetPagedCourses`, skip the
      enrollment lookup when `studentId == Guid.Empty` (early-out: `enrolledIds` stays
      empty) — guests render with zero "Enrolled" badges without a pointless query
      (research.md R2 blast radius). Confirm `Detail.cshtml.cs` `OnGetAsync` already
      degrades correctly with `Guid.Empty` (`IsEnrolled` false, attempt block gated on
      `studentId != Guid.Empty`) — record the confirmation.

### Tests for User Story 3

- [X] T016 [P] [US3] `tests/Playwright.Tests/tests/21-logged-out-enroll.spec.ts` — US3
      block (append): (a) fresh context `page.goto('/MyCourses')` → `waitForURL`
      `/Account/Login` with `ReturnUrl=%2FMyCourses` → sign in as `learnerBob` →
      `toHaveURL('/MyCourses')` with his own enrollments (no alice rows); (b) fresh
      context: `/Courses` catalog — assert zero `.tag-accent-2` "Enrolled" markers (or
      the exact badge text used by `_CourseCard.cshtml`) and course detail
      `11111111-1111-1111-1111-111111111112` shows "Enroll now" (not "✓ Enrolled"), no
      "Launch SCORM Course" link. Run → GREEN.

**Checkpoint**: all three stories independently functional; guest surfaces are public-only.

---

## Phase 5: Gates & Polish (Constitution XIII / XVI)

**Purpose**: the three verification gates with evidence, independent verification, and
post-merge regression. No new behavior.

- [ ] T017 Gate 1 — compiles and runs: `dotnet build LibreLms.slnx` 0 errors;
      in-container app restart (`Now listening on: http://localhost:5000` + 302 probe).
      Paste evidence.
- [ ] T018 Gate 2 — tests validate the change: `dotnet test tests/ArchitectureTests`
      (Principle III regression guard) + full unit suite (incl. new
      `ReturnUrlCookieTests`); filler-clean after the last unit run; full Playwright
      SERIAL: new `21-logged-out-enroll.spec.ts` all green + regression specs UNMODIFIED
      and green — `01-auth` (login-without-ReturnUrl → `/Courses` still holds, FR/SC-004),
      `03-enrollment` (signed-in HTMX flow unchanged, FR-009), `signup`, `verify-email`,
      `20-scorm-session-authz`. Paste evidence (counts vs baseline 178+1).
- [ ] T019 Independent verification (Constitution XVI): fresh no-context subagent, clean
      detached worktree on `bug/055-fix-logged-out-enroll` — build, units, app readiness,
      filler-clean, full Playwright; its instructions MUST include the filler-clean step.
      Verdict GREEN before merge. Paste verdict.
- [ ] T020 Gate 3 (post-merge, on master): `git merge --no-ff` after T019 GREEN; rebuild,
      restart, re-run Gate 2 suite on master; mark all tasks `[X]`, set spec Status to
      Complete (spec.md header), commit F. Paste evidence.
- [ ] T021 [P] Polish: quickstart.md final sweep — walk Scenarios 1–5 once against the
      merged build (manual, ~10 min), confirm the "Data hygiene" note is accurate for the
      current dev DB state (the pre-fix evidence rows from T003 may exist — document or
      clean per the note); update `HANDOFF-RUN-LOG.md` per house convention.

---

## Dependencies & Execution Order

```
T001 (branch)
  └─ T002 (ADR 0013, [P])
       └─ US1: T003 (E2E red) ── T005 (guard)
               T004 (unit red, [P]) ── T007 (helper)
               T006 (fallback, [P])      T008 (login wiring) ── T009 (guest form) ── T010 (green)
       └─ US2: T011 (journey E2E) ── T012 (verify no-changes)      [needs US1 merged-in]
       └─ US3: T013 (MyCourses)  T014 (settings, [P])  T015 (catalog)  T016 (E2E, [P])
       └─ Gates: T017 → T018 → T019 → T020; T021 [P] after T020
```

- **Story order**: US1 → US2 → US3 (US2 persists US1's cookie; US3 verifies US1's
  `Guid.Empty` sentinel; US13's MyCourses `[Authorize]` is independent of US1/US2 code
  and could start earlier if desired).
- **Critical path**: T003 → T005 → T007 → T008 → T009 → T010.
- **Within-story parallel pairs** (different files, no shared edits): T004 ∥ T005 ∥ T006;
  T014 ∥ T015 ∥ T013; T016 ∥ (US3 impl). Dispatch `[P]` tasks as parallel subagents per
  Constitution XI; the parent session integrates and keeps final write authority.

## Implementation Strategy

- **MVP = US1 only** (T003–T010): closes the security hole (no anonymous enrollments, no
  demo attribution) and delivers the core UX (redirect → sign in → back to course →
  manual re-enroll). Shippable on its own; US2/US3 are additive.
- **Increment 2 (US2)**: pins the signup+verify journey over the same cookie — expected
  to be test-only (no production code change) if T012 confirms.
- **Increment 3 (US3)**: guest read-path hygiene (one attribute, one dead-code removal,
  one early-out) + its E2E block.
- **Every increment ends at a checkpoint** with the story independently testable
  (Principle VII); the gate phase runs once at the end (single branch, single merge).

## Verification Notes

### T003 — US1 E2E RED (pre-fix build, 2026-09-20 05:28 UTC)

```
Running 4 tests using 1 worker
  ✘  1 [chromium] › 21-logged-out-enroll.spec.ts:43:7 › raw HTTP enroll POST as guest is rejected…
    Error: expect(received).toBe(expected)
    Expected: 302
    Received: 200            ← guest POST executed the enrollment (bug reproduced)
  1 failed, 3 did not run (serial)
```
Pre-fix run created one evidence row (demo learner `…0001` → course `…115`); deleted
per quickstart §Data hygiene (dev DB only).

### T010 — US1 E2E GREEN (post-fix build) + quickstart Scenarios 1–2

**Unit**: `dotnet test tests/Host.Tests` → 29/29 passed (incl. 8 new
`ReturnUrlCookieTests`).

**E2E** (`21-logged-out-enroll.spec.ts`, US1 block):
```
  ✓  1 [chromium] › raw HTTP enroll POST as guest is rejected and creates no enrollment (3.3s)
  ✓  2 [chromium] › guest Enroll click is a full-page redirect to sign-in with the course as return address (464ms)
  ✓  3 [chromium] › sign-in returns the visitor to the course; enrollment needs a second manual click (1.1s)
  ✓  4 [chromium] › guest sees no other user enrolled state on a course the demo learner is enrolled in (401ms)
  4 passed (7.4s)
```
Test 3 exercises the fresh branch end-to-end (bob not pre-enrolled): guest click → 302
→ login → back on `/Courses/Detail/…117` → "Enroll now" still visible (NOT auto-enrolled)
→ manual click → `✓ Enrolled` (htmx swap) → "Git Version Control" in bob's MyCourses.
Test 3 is idempotent: on re-runs with bob already enrolled it asserts the enrolled
state instead (persistent dev DB — same pattern as `03-enrollment.spec.ts`).

**Quickstart Scenario 1** (manual, curl):
```
$ curl -i -X POST "http://localhost:5000/Courses/Detail/…115?handler=Enroll"
HTTP/1.1 302 Found
Location: http://localhost:5000/Account/Login?ReturnUrl=%2FCourses%2FDetail%2F…115%3Fhandler%3DEnroll
```
Cookie set on the login GET (contract per data-model.md):
```
Set-Cookie: lms.ReturnUrl=%252FCourses%252F…%253Fhandler%253DEnroll; max-age=86400; path=/; samesite=lax; httponly
```
(24 h, HttpOnly, SameSite=Lax, path=/, URL-encoded value; no `secure` under dev http —
`Secure` is set only on https per plan; double-encoding is by design so the browser's
decoding at read time yields a still-encoded local URL.)

**Quickstart Scenario 2** (round trip): covered by E2E test 3 above (same steps, manual
equivalent).

**DB check** (post-green, `LearningLms.dbo.Enrollments` for courses `…115/…116/…117`):
- `…115`, `…116`: **zero rows** — guest raw POST (test 1) and guest UI click (test 2)
  created nothing.
- `…117`: bob `…0002` @ 10:57:41 UTC (test 3's MANUAL second click — expected);
  alice `…0001` @ 04:36:14 UTC is a pre-existing user row (untouched).
- **Zero new rows attributed to the demo learner `…0001`** by any guest action. ✓

**Test fix note (T010)**: the initial draft of test 3 asserted
`#enroll-region` contains "Enrolled" after the manual click — but the HTMX success
partial swaps with `hx-swap="outerHTML"`, which replaces `#enroll-region` itself, so
the id no longer exists in the DOM. Fixed to assert the rendered `✓ Enrolled` state
(`page.getByText('✓ Enrolled')`) — same class of assertion as the page's own enrolled
state. (The pre-existing `03-enrollment.spec.ts` avoids the issue by branching on
"already enrolled from a previous run".)

### T011 — US2 E2E GREEN (post-fix build)

```
  ✓  5 [chromium] › new user: Enroll → signup → verify → sign in → back on the original course; manual enroll (1.2s)
  ✓  6 [chromium] › J5: foreign ReturnUrl is rejected — no cookie, sign-in lands on home (639ms)
```
Journey J2 verified end-to-end with a per-run fresh account (`signupjourney{run}@example.com`,
dev outbox for the verification link — same pattern as `verify-email.spec.ts`): the
`lms.ReturnUrl` cookie set at the challenge bounce survives Signup → Verify ("Go to
sign in" goes to `/Account/Login` with NO ReturnUrl query) and is consumed by the
sign-in, landing on `/Courses/Detail/…117` (NOT home) with "Enroll now" still visible;
the manual click enrolls the new account. J5: `https://evil.example/` sets no cookie
(`context.cookies()` checked) and sign-in lands on home, never the foreign URL.

### T012 — Signup/Verify no-change confirmation

- `git diff master -- Signup.cshtml.cs Verify.cshtml.cs` → **empty** (unmodified).
- `grep -ri "returnurl"` over both `.cs` and `.cshtml` → **zero matches** (neither
  reads nor clears `lms.ReturnUrl`). The cookie persists across the journey by design
  (data-model.md state-transition notes, contracts/return-url.md J2). No design
  deviation — US2 was test-only, as the plan expected.

### T013–T016 — US3 GREEN

Implementation (dispatched as 3 parallel subagents per Constitution XI; parent
integrated the build): T013 `[Authorize]` on `MyCoursesModel` (+using, ADR 0013
comment); T014 private `GetStudentId()` in `Settings.cshtml.cs` now returns
`Guid.Empty` (demo GUID removed — page is class-level `[Authorize]`d); T015 catalog
`GetPagedCourses` skips the bulk enrollment lookup for `Guid.Empty`.
T015 required confirmation (Detail.cshtml.cs degrades correctly, NO change needed):
`IsEnrolled` resolves false for `Guid.Empty` (no matching row), the attempt/SCORM
block is already gated on `enrolled && studentId != Guid.Empty`, and `TryEnrollAsync`
early-returns a "please log in" message for `Guid.Empty`.

Post-rebuild E2E — full file, 8/8 green:
```
  ✓ 1-4 [chromium] › US1 (raw HTTP 302 / guest full redirect / round trip + manual click / no demo badge)
  ✓  5 [chromium] › US2 new-user journey lands on the original course (1.2s)
  ✓  6 [chromium] › US2 J5 foreign ReturnUrl rejected (648ms)
  ✓  7 [chromium] › US3 guest /MyCourses → /Account/Login?ReturnUrl=%2FMyCourses → sign in → own list (670ms)
  ✓  8 [chromium] › US3 guest catalog: zero "✓ Enrolled" badges; course …112 shows "Enroll now" (563ms)
  8 passed (8.9s)
```
Manual probe: `curl -i http://localhost:5000/MyCourses` as guest → `302 Found` (class-level
`[Authorize]` enforced — consistent with research.md R1 control group).
Unit suite re-run after the US3 changes: **29/29 passed** (Host.Tests).

