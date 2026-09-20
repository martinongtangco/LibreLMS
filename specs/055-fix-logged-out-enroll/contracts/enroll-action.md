# Contract: Enroll Action (055)

**Date**: 2026-09-20
**Scope**: the learner self-enroll action — the HTMX/plain form on the course detail page and
the HTTP request behind it. This is a UI/HTTP behavioral contract (the app exposes web UI
plus JSON APIs; this contract covers the web action and its anonymous-call behavior).

## Endpoint under contract

- **Route**: `POST /Courses/Detail/{id:guid}?handler=Enroll` (Razor Pages handler
  `CourseDetailModel.OnPostEnrollAsync`)
- **Caller**: the course detail page's enroll form
  - authenticated visitor → HTMX form (`hx-post`, `HX-Request: true`)
  - signed-out visitor → plain form (full-page POST)
- **Auth requirement**: authenticated, verified learner session (cookie scheme `"Cookie"`).
  Enforced by an explicit guard in the handler (ADR-0013 — handler-level `[Authorize]` is
  inert in .NET 10).

## Behavior matrix

| Caller state | Request | Response | Data effect |
|--------------|---------|----------|-------------|
| Signed out | POST (browser, from course page) | **302** → `/Account/Login?ReturnUrl=<course page URL>` (full page navigation) | **none** — no enrollment row created or modified (FR-001) |
| Signed out | raw HTTP POST (no session cookie) | **302** with `Location: /Account/Login?ReturnUrl=…` (cookie challenge) | **none** (FR-010) |
| Signed out | POST with an *expired/invalid* session | treated as signed out → 302 to login | none |
| Signed in (learner, not enrolled) | POST | **200**, `_EnrollmentResult` partial: success ("Successfully enrolled!", enrolled state) | 1 new `Enrollment` row for the caller's own `StudentId` (from the session claim — never a fallback) |
| Signed in (learner, already enrolled) | POST | **200**, `_EnrollmentResult` partial: warning "You are already enrolled in this course." | none (duplicate guard) |
| Signed in (course doesn't exist) | POST | **200**, `_EnrollmentResult` partial: error "Course not found." | none |
| Signed in (non-learner role, e.g. admin) | POST | same as learner rows (role-agnostic self-enroll, unchanged behavior) | caller's own row |

## Invariants

1. **Identity source is exclusive**: the `StudentId` used is parsed from the session
   claims (`ClaimTypes.NameIdentifier` / `sub`). If no parseable claim exists, the handler
   challenges (it never proceeds with a substitute identity). `ScormHelpers.GetStudentId`
   returns `Guid.Empty` in that case and no code path enrolls on `Guid.Empty`'s behalf.
2. **Guard ordering**: the auth guard runs before any service call, before any model
   binding side effects, before the enrollment lookup.
3. **No automatic enrollment**: a 302 challenge never triggers an enrollment; the sign-in
   that follows it lands on the course page, and enrollment happens only on a subsequent,
   explicit POST (FR-004).
4. **Response shape unchanged for authenticated callers**: same partial view
   (`_EnrollmentResult`), same swap target (`#enroll-region`), same success/warning/error
   strings — FR-009 regression guard (existing E2E `03-enrollment.spec.ts` passes unchanged).
5. **Antiforgery**: class-level `[IgnoreAntiforgeryToken]` is retained (house pattern from
   spec 024 for the HTMX form); the guest's plain form includes an antiforgery token so the
   request is browser-originated; the challenge happens before any handler work regardless.

## Out of contract

- Admin enrollment surfaces (`/api/admin/enrollments*`, Admin Enrollments pages) — separate,
  role-authorized, untouched by this fix.
- SCORM launch/session endpoints — already authorized (specs 047–050), untouched.
- `POST /api/enrollments` (JSON API) — already `[Authorize]`d at the endpoint (enforced;
  verified), untouched.
