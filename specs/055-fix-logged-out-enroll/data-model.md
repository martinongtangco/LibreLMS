# Data Model: Fix Enrollment While Logged Out (055)

**Date**: 2026-09-20
**Spec**: [spec.md](./spec.md)

## Scope

This fix changes **no durable schema**. MSSQL tables (`Students`, `Enrollments`,
`ScormPackages`, `ScormAttempts`, …) are untouched; no EF Core migration is required.
Valkey is not involved (Principle VI — it remains SCORM-runtime-only).

The only state introduced is one **transient, per-visitor** entity: the pending return
address.

## Entities

### PendingReturnAddress (transient, cookie-resident)

The local page a signed-out visitor was trying to reach when a sign-in-required action
challenged them. Survives sign-up + email verification; consumed by the next successful
sign-in.

| Attribute | Type | Rules |
|-----------|------|-------|
| `Value` | string (local URL, e.g. `/Courses/Detail/{guid}?handler=Enroll`) | MUST satisfy `Url.IsLocalUrl` at set-time **and** re-validated at consume-time. Non-local values are never stored/honored (FR-006). |
| Set time | implicit (cookie write) | Overwrites any prior value — **single slot, most-recent-wins** (FR-007, spec Edge Cases). |
| Lifetime | 24 hours | Cookie `MaxAge`; mirrors the existing email-verification link lifetime (Assumptions). Expired ⇒ treated as absent. |
| Consumed-once | — | Deleted from the cookie at the moment it is used for a post-sign-in redirect (FR-007). |

**Cookie shape** (transport, not a domain entity):

| Property | Value |
|----------|-------|
| Name | `lms.ReturnUrl` |
| Value | URL-encoded local URL |
| `HttpOnly` | true (JS cannot read or forge it from page scripts) |
| `SameSite` | Lax (sent on top-level navigations; not on cross-site subresource POSTs) |
| `Secure` | production only (dev runs plain `http://localhost:5000`) |
| `Path` | `/` |
| `MaxAge` | 86400 s (24 h) |

**Loss semantics**: losing the cookie (cleared, different browser for the verification
email, expired) degrades the journey to landing on the home page after sign-in. No data
correctness is affected — it is pure navigation convenience (Principle VI test: "would
losing this on a flush be fine?" → yes).

### Identity resolution (behavior change, no entity change)

`ScormHelpers.GetStudentId(HttpContext)` and the private duplicate in
`Account/Settings.cshtml.cs`:

| Before | After |
|--------|-------|
| claim → its GUID; **no/invalid claim → hardcoded demo GUID** `550e8400-…-0001` | claim → its GUID; **no/invalid claim → `Guid.Empty`** (the codebase's existing "no learner" sentinel) |

`Guid.Empty` is already the anonymous sentinel in the enrollment path
(`TryEnrollAsync` short-circuits with "Please log in…"), so read paths degrade to
"not enrolled" without schema or query changes:

- `EnrollmentLookup.IsEnrolledAsync(Guid.Empty, courseId)` → `false`
- `EnrollmentLookup.GetEnrolledCourseIdsAsync(Guid.Empty, ids)` → empty set
- `ScormAttemptService.GetMyAttemptsAsync(Guid.Empty)` → no rows (and the detail page already
  gates this on `studentId != Guid.Empty`)

The demo learner **account** (`550e8400-…-0001`, alice@example.com) remains a normal seeded
account used by tests/demos — it simply can no longer be impersonated by anonymous visitors
(FR-003, Assumptions).

## Relationships

- `PendingReturnAddress` — per browser/visitor, 1:0..1 (single slot); no relationship to
  `Student` (it exists precisely while the visitor has **no** authenticated `Student`).
- `Enrollment` (unchanged): `(StudentId → Student, CourseId → Course)`. The bug produced rows
  with `StudentId = 550e8400-…-0001` created anonymously; after the fix, **no** new row can
  ever be created without an authenticated, verified session. (Pre-existing demo-attributed
  rows from the bug are data, not schema — cleanup is out of scope for this fix and handled
  ad hoc if observed; see quickstart §Data hygiene.)

## State transitions

```
                 ┌─────────────┐
   (no cookie)   │   Absent    │◄──────────────────────────┐
                 └──────┬──────┘                           │
        valid local ReturnUrl query on sign-in page        │
                        │  (set/overwrite, 24 h)           │
                        ▼                                  │
                 ┌─────────────┐   > 24 h                  │
                 │   Pending   │──────────────► (treated as Absent)
                 └──────┬──────┘                           │
                        │  successful sign-in              │
                        │  (consume: re-validate, delete)  │
                        ▼                                  │
                 ┌─────────────┐                           │
                 │  Consumed   │ ── redirect to Value      │
                 └─────────────┘                           │
                        │                                  │
        invalid/non-local at set or consume time ──────────┘
        (never stored / dropped, fall back to home page)
```

Notes:
- **Pending** survives arbitrary intermediate pages (sign-up, verify, home) — it is only
  written by the sign-in page (from a validated query value) and only read by a successful
  sign-in.
- A sign-in page render **without** a `ReturnUrl` query value (e.g. the verify → "Go to sign
  in" hop) leaves an existing Pending value untouched.
- The `AccessDenied` bounce (already-authenticated visitor on the sign-in page) may set the
  cookie from its `ReturnUrl`; it is only consumed by a *subsequent successful sign-in*, so
  there is no same-request loop.

## Validation rules summary (from spec)

| Rule | Source | Where enforced |
|------|--------|----------------|
| Anonymous enroll creates/modifies nothing | FR-001/FR-010 | Enroll handler guard (challenge before any service call) |
| No action on behalf of a demo/fixed identity | FR-003 | `GetStudentId` fallback removal |
| Return address local-only | FR-006 | `Url.IsLocalUrl` at set **and** consume |
| 24 h max lifetime, single slot, consume-once | FR-007 | Cookie attributes + consume deletes the cookie |
| No auto-enroll after sign-in | FR-004 | Redirect only — the enroll form is re-rendered for a manual click |
| Guest read paths show not-enrolled; My Courses requires sign-in | FR-008 | `Guid.Empty` sentinel behavior + `[Authorize]` on `MyCoursesModel` |
