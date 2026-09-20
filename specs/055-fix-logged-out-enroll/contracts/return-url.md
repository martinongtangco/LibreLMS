# Contract: Pending Return Address (055)

**Date**: 2026-09-20
**Scope**: the sign-in/sign-up return-to-origin flow — when the pending return address is
set, carried, consumed, or dropped, and where each journey lands.

## Mechanism

A 24 h, `HttpOnly`, `SameSite=Lax` cookie `lms.ReturnUrl` (see
[data-model.md](../data-model.md) for the full attribute table and loss semantics).
Validation: `Url.IsLocalUrl` at **set** and **consume** (defense in depth).

| Operation | Trigger | Effect |
|-----------|---------|--------|
| **Set / overwrite** | `GET /Account/Login` with a `ReturnUrl` query value that passes `Url.IsLocalUrl` | Cookie written (URL-encoded), `MaxAge=24 h`; prior value replaced (single slot) |
| **No-op** | `GET /Account/Login` **without** a valid `ReturnUrl` query value | Existing cookie (if any) is left untouched — this is what preserves the address through the verify → "Go to sign in" hop |
| **Consume** | successful sign-in (`POST /Account/Login`) | Cookie read, re-validated, **deleted**; response redirects to its value |
| **Fallback** | successful sign-in with no/invalid/expired cookie | redirect to `/` (existing behavior → `/Courses`) |
| **Drop (set-time)** | `ReturnUrl` query value fails `Url.IsLocalUrl` | not stored; no error surfaced to the visitor |
| **Drop (consume-time)** | cookie value fails `Url.IsLocalUrl` | deleted; fallback redirect |

## Journey matrix

Legend: `C` = course page `/Courses/Detail/{id}` (the URL that encouraged the action).

### J1 — Returning user (spec US1)

| Step | Page | Cookie |
|------|------|--------|
| 1 | `C` (signed out) → click **Enroll now** (plain form POST) | — |
| 2 | challenge **302** → `GET /Account/Login?ReturnUrl=C` | **set** = `C` |
| 3 | sign in (credentials) | — |
| 4 | **redirect → `C`** | **consumed + deleted** |
| 5 | user clicks **Enroll now** (HTMX, now authenticated) → enrolled under own account | — |

### J2 — New user through sign-up + verification (spec US2)

| Step | Page | Cookie |
|------|------|--------|
| 1 | `C` → **Enroll now** → 302 → sign-in page | **set** = `C` |
| 2 | **Create an account** → `/Account/Signup` | untouched (persists) |
| 3 | sign-up submitted → "Check your email" | untouched |
| 4 | (email) verification link → `/Account/Verify?token=…` → success | untouched (persists — the critical step) |
| 5 | **Go to sign in** → `/Account/Login` (no query) | untouched (no-op set) |
| 6 | sign in | **consumed** |
| 7 | **redirect → `C`** → manual **Enroll now** → enrolled | deleted |

### J3 — No pending address (regression guard, existing behavior)

| Step | Page | Cookie |
|------|------|--------|
| 1 | `GET /Account/Login` (typed URL / nav link, no `ReturnUrl`) | — |
| 2 | sign in | — |
| 3 | **redirect → `/`** → `/Courses` | — |

Existing `01-auth.spec.ts` expectations hold (login → `/Courses`).

### J4 — My Courses (spec US3)

| Step | Page | Cookie |
|------|------|--------|
| 1 | `GET /MyCourses` (signed out) → class-level `[Authorize]` challenge **302** → `GET /Account/Login?ReturnUrl=/MyCourses` | **set** = `/MyCourses` |
| 2 | sign in | **consumed** |
| 3 | **redirect → `/MyCourses`** (now shows the signed-in learner's own list) | deleted |

### J5 — Tampered / foreign return address (FR-006)

| Step | Page | Cookie |
|------|------|--------|
| 1 | `GET /Account/Login?ReturnUrl=https://evil.example/` (or `//evil.example/`) | **not stored** (fails `Url.IsLocalUrl`) |
| 2 | sign in | — |
| 3 | **redirect → `/`** | — |

A forged cookie value planted by a malicious local page is likewise dropped at consume-time
(re-validation) — the redirect target can never be foreign.

### J6 — Access-denied bounce (existing behavior, must not loop)

| Step | Page | Cookie |
|------|------|--------|
| 1 | authenticated learner on protected admin page → 302 → `/Account/Login?ReturnUrl=/Admin/…` | **set** |
| 2 | sign-in page shows the signed-in-but-not-allowed state (no redirect — existing `AccessDenied` UI) | untouched |
| 3a | same learner signs out and a SuperUser signs in on the same browser → redirect to `/Admin/…` (allowed) | consumed |
| 3b | learner signs in again (same account) → redirect to `/Admin/…` → bounced to sign-in page's denied state again (no loop: `OnGet` renders state, never redirects) | consumed |

## Non-goals

- The return address does NOT carry state beyond the URL (no pre-filled enrollment
  form, no auto-submission).
- It does not survive browser-profile changes (verification opened in a different browser →
  fallback to home; accepted, documented in data-model loss semantics).
- It does not interact with forgot/reset password flows (cookie simply persists/expiry).
