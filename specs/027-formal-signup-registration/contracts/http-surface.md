# HTTP / Web Surface: Formal Signup & Registration

**Feature**: [spec.md](../spec.md) | **Date**: 2026-08-15

User-facing web surface (Razor Pages in `src/Host/Pages`) plus the Development-only
observability endpoint. Behavior contract — markup/styling follows the existing
`_Layout` and form patterns.

---

## 1. GET/POST /Account/Signup (NEW)

| | |
|---|---|
| Auth | anonymous (redirect to `/` if already signed in) |
| Form fields | `name` (text), `email` (email), `password` (password), `confirmPassword` (password) |
| Validation | client hint list showing the strict policy; server re-validates everything (format, uniqueness case-insensitive, policy rules with the specific failed rule(s), confirmation match) |
| Success | confirmation screen: "account created — check your email to verify"; **no auto sign-in** (FR-009). Verification + welcome emails generated (FR-008) |
| Errors | inline field-level messages; duplicate email → "email already in use"; throttled → "too many attempts, try again later" |
| Rate limit | sign-up throttle: 10 attempts / email / 24 h (R6) |

## 2. GET /Account/Verify (NEW)

| | |
|---|---|
| Auth | anonymous |
| Query | `token` (base64url) |
| Valid + unexpired | account marked verified; success screen with a link to sign in |
| Used / expired / invalid | distinct friendly error screen, each offering "request a new verification email" (FR-012); invalid/tampered → no account state change (US2 scenario 5) |

## 3. GET/POST /Account/ForgotPassword (NEW)

| | |
|---|---|
| Auth | anonymous (redirect to `/` if signed in) |
| Form fields | `email` |
| Behavior | registered email → reset email with single-use 30-min link generated; unregistered email → **no email generated**. On-screen confirmation is **identical** in both cases (FR-015): "If an account exists for that address, a password reset link has been sent." |
| Rate limit | reset throttle: 5 requests / email / 1 h (R6) |

## 4. GET/POST /Account/ResetPassword (NEW)

| | |
|---|---|
| Auth | anonymous |
| Query (GET) | `token` |
| Valid + unexpired (GET) | new-password form: `password`, `confirmPassword` (strict policy applies, FR-017) |
| Used / expired / invalid (GET) | friendly error with "request a new reset" link |
| Success (POST) | password updated (PBKDF2), token consumed, `SecurityStamp` rotated → **all pre-existing sessions invalidated** (FR-017); screen: "password updated — sign in" |

## 5. /Account/Login (CHANGED)

| Change | Contract |
|---|---|
| Demo-credentials hint | **removed** — no demo/test credentials anywhere on the page (FR-023) |
| New links | "Create an account" → `/Account/Signup`; "Forgot your password?" → `/Account/ForgotPassword` (FR-024) |
| Unverified account sign-in | blocked with "please verify your email" message + "resend verification email" action (FR-011; resend uses the resend throttle) |
| Failed sign-in | generic "Invalid email or password." — no account-existence leakage (FR-025, unchanged behavior) |
| Verified sign-in | unchanged: cookie with claims incl. new `SecurityStamp` claim (R3) |

## 6. GET /Dev/Outbox (NEW — Development environment only)

| | |
|---|---|
| Gate | `IWebHostEnvironment.IsDevelopment()`; non-Development → 404 (FR-021 support) |
| Output | HTML table of recorded emails: To, Purpose, Subject, Body (links clickable), SentAtUtc; newest first; "Clear outbox" action |
| Purpose | human verification of FR-020 |

## 7. GET /api/dev/outbox (NEW — Development environment only)

```json
[
  {
    "to": "user@example.com",
    "purpose": "Verification",
    "subject": "Verify your email",
    "body": "Please verify your email by opening this link: http://host/Account/Verify?token=...",
    "sentAtUtc": "2026-08-15T12:00:00Z"
  }
]
```

- Newest first; same 404 gate as the page.
- **This is the deterministic link-extraction surface for Playwright E2E tests**
  (Principle XIII): tests sign up / request resets via the UI, then read this endpoint.

## 8. Cookie authentication (CHANGED — behavioral contract)

- Sign-in now includes a `SecurityStamp` claim copied from the account at sign-in time.
- On each request, the cookie's `OnValidatePrincipal` re-validates the stamp against the
  account (≤60 s in-process cache). Mismatch or missing claim → user is signed out and
  redirected to login (FR-017 enforcement).
- Existing cookies issued before this feature (no stamp claim) are invalidated at first
  validation — users sign in once (documented, accepted).
