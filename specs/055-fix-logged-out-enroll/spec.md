# Feature Specification: Fix Enrollment While Logged Out (Anonymous Enroll + Broken Return Flow)

**Feature Branch**: `bug/055-fix-logged-out-enroll`

**Created**: 2026-09-20

**Status**: Complete (2026-09-20 — merged to master @ 92fc26b; gates T017–T020 green: units 197/197, E2E 186+1 skip in-container, independent verification GREEN)

**Input**: User description: "i found a bug. if logged off, i can still enroll Courses. Im not sure which
account it goes — shouldnt allow enrollment. Maybe lets back track a bit, when signed off, the
enroll button should redirect you to the login page. Upon login, you are returned back to the
course you wanted to enroll and you press enroll again (actions must not be automatic). If the
user apparently doesnt have account, they will select sign up. we should be temporarily
persisting the url to what encouraged the user to sign up. after successful signup and
validation, the user is brought back to the course it wanted to enroll"

## Root Cause (verified against the running app, 2026-09-20)

Three compounding defects:

- **RC-1 — Anonymous enroll executes.** A signed-out visitor clicking "Enroll now" on a course
  page is actually enrolled. The enroll action's per-handler authorization marker is silently
  ignored by the platform (.NET 10, verified with a minimal repro: a handler-level `[Authorize]`
  does not challenge; class-level does), so the action runs with no identity.
- **RC-2 — Silent demo-identity fallback.** When no signed-in identity exists, the helper that
  resolves "who is the current learner" falls back to a fixed pre-seeded demo learner
  (the first seeded student, `alice@example.com`). Every anonymous enrollment is silently
  attributed to that demo account, and every anonymous read (catalog badges, course page
  "Enrolled" state, My Courses list) shows that demo learner's data.
- **RC-3 — No return-to-origin flow.** The sign-in page ignores the return address it is given
  (always lands on the home page), and the sign-up / email-verification flow never carries the
  return address at all — so neither returning nor new users ever get back to the course they
  were trying to enroll in.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Signed-out visitor is redirected to sign-in and back (Priority: P1)

A visitor who is not signed in opens a course page and clicks "Enroll now". Instead of being
enrolled (or seeing anyone else's enrollment state), the visitor is taken to the sign-in page
as a full page navigation. After signing in, the visitor is returned to the exact course page
they were on. Enrollment is NOT performed automatically — the visitor sees the "Enroll now"
button again and presses it themselves; only then are they enrolled under their own account.

**Why this priority**: This is the security hole itself (anonymous enrollments attributed to a
demo account) plus the user-visible broken flow. Everything else hangs off it.

**Independent Test**: With no session cookie, attempt to enroll via the course page (and via a
raw HTTP POST to the enroll action). Verify no enrollment is created and the visitor is sent to
the sign-in page with the course as the return address. Sign in, verify the visitor lands back
on that course page, and complete enrollment with one manual click.

**Acceptance Scenarios**:

1. **Given** a visitor with no valid session, **When** they click "Enroll now" on a course
   page, **Then** they are redirected to the sign-in page (full page, not an in-place swap of
   the sign-in form into the course card) and no enrollment is created for any account.
2. **Given** the visitor from (1) signs in successfully on the sign-in page, **When** the sign-in
   completes, **Then** they are returned to the same course page they started from.
3. **Given** the visitor from (2) on the course page, **When** they press "Enroll now" again
   (manually), **Then** they are enrolled under their own account and see the enrolled state.
4. **Given** any client (browser or raw HTTP) with no valid session, **When** it POSTs directly
   to the enroll action, **Then** the request is rejected, no enrollment row is created, and the
   response points at the sign-in page.

---

### User Story 2 — New user's sign-up journey returns them to the course (Priority: P2)

A visitor without an account tries to enroll: Enroll → sign-in page → "Create an account" →
sign-up → confirmation ("check your email") → verification link → verified → sign in. The course
page that started the journey must be remembered through sign-up and email verification, and
after the new user signs in they land back on that course page and press "Enroll now" themselves.

**Why this priority**: The user explicitly asked for the URL that encouraged sign-up to be
persisted across signup + validation. It is the second half of the broken flow, but depends on
the P1 redirect/return mechanism.

**Independent Test**: In a fresh browser profile, drive Enroll → sign-in → sign up → (use the
verification link) → sign in, and verify the final destination is the original course page with
the "Enroll now" button available for a manual click.

**Acceptance Scenarios**:

1. **Given** a signed-out visitor on a course page, **When** they click "Enroll now", then
   "Create an account" on the sign-in page, and complete sign-up, **Then** the return address
   (the course page) is still pending after sign-up succeeds.
2. **Given** the pending return address from (1), **When** the user completes email
   verification and then signs in, **Then** they land on the original course page (not the home
   page) and can enroll with a manual click.
3. **Given** a pending return address, **When** it is older than its retention window (24
   hours), **Then** it is treated as absent and the user lands on the home page after sign-in.

---

### User Story 3 — Signed-out visitors see only public course info (Priority: P3)

A signed-out visitor browsing the site sees courses in a "not enrolled" state: no other
user's "Enrolled" badges on catalog cards or course pages, no progress/status tags, and the
My Courses page is not exposed — visiting it sends the visitor to sign-in (and back to My
Courses after signing in).

**Why this priority**: Same root cause (RC-2) as P1, but on read paths. It is a data leak
(a signed-out visitor currently sees the demo learner's full course list) and should be closed
in the same fix, but it is not the reported enrollment defect.

**Independent Test**: With no session cookie, load the catalog, a course page, and My Courses;
verify no other user's enrollment state or course list is visible, and My Courses redirects to
sign-in with a return to My Courses.

**Acceptance Scenarios**:

1. **Given** a signed-out visitor, **When** they browse the course catalog and open a course
   page, **Then** courses show the not-enrolled state (no "Enrolled" badge, no progress or
   status tags) and the "Enroll now" button is offered.
2. **Given** a signed-out visitor, **When** they navigate to My Courses, **Then** they are
   redirected to sign-in and, after signing in, are returned to My Courses.

## Edge Cases

- **Open-redirect via return address**: a crafted or tampered return address pointing at an
  external URL (or a protocol-relative URL) MUST be rejected; the user lands on the home page.
  Only local (same-site) paths are honored.
- **Return address to a page the user may not access**: a learner whose pending return address
  is an admin page lands there after sign-in and is then bounced by the existing
  access-denied handling (signed-in-but-not-allowed state on the sign-in page). No new handling
  required, but it must not loop.
- **Already enrolled after the round trip**: the visitor signs in and returns to the course,
  but is already enrolled (e.g., enrolled from another device). The course page shows the
  enrolled state; pressing nothing happens (no duplicate enrollment, no error).
- **Stale/missing pending return address** (never set, or expired): sign-in lands on the home
  page — the existing default behavior.
- **Pending return address is a single slot per visitor**: if the visitor starts two different
  protected journeys, the most recent one wins (documented, accepted simplification).
- **Signed-in learner enrolls**: unchanged end-to-end behavior (success, "already enrolled"
  warning, "course not found" error) — regression-guarded.
- **Course deleted between redirect and return**: returning to a deleted course shows the
  existing "Course Not Found" state; the pending return address is still consumed.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST NOT create, modify, or attribute any enrollment when the visitor
  has no valid signed-in identity.
- **FR-002**: When a signed-out visitor attempts to enroll (via the course page or directly),
  the system MUST send them to the sign-in page as a full page navigation, with a return
  address pointing at the course page they were on.
- **FR-003**: The system MUST NOT perform any learner action on behalf of a fixed or demo
  identity when no signed-in identity is present; identity resolution MUST yield "no learner"
  instead of a fallback account.
- **FR-004**: After a successful sign-in, the system MUST return the visitor to the pending
  return address when one exists and is valid, and to the home page otherwise. Enrollment MUST
  NOT be performed automatically as part of or after sign-in.
- **FR-005**: The sign-up journey MUST preserve the pending return address: a visitor who signs
  up from the sign-in page and completes email verification, then signs in, MUST be returned to
  the original course page.
- **FR-006**: The pending return address MUST be validated as a local (same-site) URL before it
  is stored or honored; non-local values MUST be discarded.
- **FR-007**: The pending return address MUST be temporary: it MUST expire no later than 24
  hours after being set, MUST be cleared after being used once, and MUST be a single value per
  visitor (most recent wins).
- **FR-008**: For a signed-out visitor, the course catalog and course page MUST present the
  not-enrolled state (no other user's enrollment badge, progress, or status), and the My
  Courses page MUST require sign-in (redirect to sign-in with a return to My Courses).
- **FR-009**: Enrollment by a signed-in, verified learner MUST be unchanged: success,
  duplicate-enrollment warning, and course-not-found error behave exactly as before.
- **FR-010**: Direct/programmatic anonymous attempts against the enroll action (raw HTTP POST
  without a session) MUST be rejected without creating any data, and the response MUST direct
  the caller to the sign-in page.

### Key Entities

- **Pending Return Address** (transient, per visitor): the local page a signed-out visitor was
  trying to reach when they hit a sign-in-required action. Attributes: the local URL, the time
  it was set, a 24-hour lifetime, single slot (most recent wins), consumed on the next
  successful sign-in (or discarded on expiry). It is ephemeral convenience state — losing it
  degrades the user to landing on the home page, nothing more.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of anonymous enrollment attempts (UI and raw HTTP) are rejected; after the
  fix, zero new enrollment rows are ever attributable to a demo/fallback identity.
- **SC-002**: A signed-out visitor who clicks "Enroll now" on a course page reaches the sign-in
  page on the first click, and after signing in lands back on that same course page and
  completes enrollment with exactly one additional manual "Enroll now" click.
- **SC-003**: A brand-new user who completes the Enroll → sign-in → sign up → verify → sign-in
  journey lands on the original course page and enrolls there; the return address survives the
  email-verification step.
- **SC-004**: Signed-in learner enrollment behavior is unchanged — the existing E2E enrollment
  scenarios (success, duplicate, not-found) all pass without modification.
- **SC-005**: A signed-out visitor sees no other user's enrollment state anywhere on the public
  surfaces (catalog, course page) and cannot reach My Courses without signing in.

## Assumptions

- "Logged off" means no valid session cookie; the logout flow itself (specs 010/011) works
  correctly and is out of scope for this fix.
- A 24-hour retention for the pending return address is sufficient (matches the existing
  verification-link lifetime); single-slot-per-visitor (most recent wins) is acceptable.
- Auto-enrollment after sign-in is explicitly NOT wanted (per the user) — the user always
  presses "Enroll now" a second time, manually.
- The demo learner account itself remains seeded (used by tests/demos); only the silent
  fallback that acts as that account on behalf of anonymous visitors is removed.
- The admin/SCORM API surfaces already enforce authentication (specs 047–050); this spec
  re-verifies them as regression guards but does not redesign them.
