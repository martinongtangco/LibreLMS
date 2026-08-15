# Transactional Email Messages: Formal Signup & Registration

**Feature**: [spec.md](../spec.md) | **Date**: 2026-08-15

Contract for the three transactional messages generated through
`ITransactionalEmailSender` (payload shape: see
[module-contracts.md](module-contracts.md) → `OutboundEmail`).

General:
- Plain-text bodies (the mock displays them verbatim; SendGrid can render plain text).
- All links are absolute URLs built from the current request's scheme/host
  (`http://<host>/...` in dev), so mock-delivered links work as-is.
- Tokens are 32-byte random values, base64url-encoded, single-use, hashed before storage
  (R4).
- Recipient is the normalized (lowercased) account email.
- No password or password fragment ever appears in any email body.

---

## 1. Verification email — `EmailPurpose.Verification`

| | |
|---|---|
| Sent when | successful self-service sign-up; verification resend |
| Subject | `Verify your email — Libre LMS` |
| Body (template) | `Hi {name},\n\nwelcome to Libre LMS. Please verify your email address by opening this link:\n\n{absoluteUrl}/Account/Verify?token={token}\n\nThis link expires in 24 hours and works once. If you did not create an account, you can ignore this email.` |
| Token semantics | sets/replaces `VerificationTokenHash` + expiry (24 h); consuming it sets `IsEmailVerified = true` and clears the token columns |

## 2. Welcome email — `EmailPurpose.Welcome`

| | |
|---|---|
| Sent when | successful self-service sign-up (together with the verification email, FR-008) |
| Subject | `Your Libre LMS account has been created` |
| Body (template) | `Hi {name},\n\nyour Libre LMS account for {email} has been created.\n\nAfter you verify your email, you can sign in at {absoluteUrl}/Account/Login.\n\n— The Libre LMS team` |
| Token semantics | none (informational) |

## 3. Password reset email — `EmailPurpose.PasswordReset`

| | |
|---|---|
| Sent when | forgot-password request for a registered email (never for unregistered) |
| Subject | `Reset your Libre LMS password` |
| Body (template) | `Hi {name},\n\nwe received a request to reset your password. Open this link to choose a new one:\n\n{absoluteUrl}/Account/ResetPassword?token={token}\n\nThis link expires in 30 minutes and works once. If you did not request a reset, you can ignore this email — your password will not change.` |
| Token semantics | sets/replaces `ResetTokenHash` + expiry (30 min); consuming it applies the new password, clears the token columns, and rotates `SecurityStamp` |

---

## Mock delivery behavior (this slice)

- Every message is appended to the dev outbox and logged in full (FR-020).
- Sending is non-blocking and never fails the originating flow; a failed/missing send is
  visible in the outbox absence, and every flow has a resend path (FR-022).
- Zero real outbound email (FR-021); provider swap to SendGrid is a DI change only
  (ADR-0004, out of scope for this slice).
