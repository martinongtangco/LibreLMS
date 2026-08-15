# ADR-0004: Transactional Email Seam with Development Mock Outbox

**Status**: Accepted
**Date**: 2026-08-15
**Supersedes**: N/A

## Context

Spec 027 (formal signup and registration) adds transactional email: account verification links, a welcome message, and password reset links. Two requirements shape how that gets built:

1. **FR-021**: this implementation MUST send zero real outbound email.
2. **FR-019**: all transactional email sending MUST go through a single, swappable email delivery boundary.

The operator plans to adopt a real provider (SendGrid) in the future. If "send this email" code were tangled into the signup, verification, and reset flows, swapping in a real provider later would mean touching those flows. The seam has to sit in front of the business logic, not inside it.

## Decision

One interface is the entire boundary — one sentence: *the place any code sends transactional email without knowing who delivers it.*

```csharp
// src/SharedKernel/ITransactionalEmailSender.cs (namespace LibreLms.SharedKernel)
public record OutboundEmail(string To, EmailPurpose Purpose, string Subject, string Body);

public enum EmailPurpose { Verification, Welcome, PasswordReset }

public interface ITransactionalEmailSender
{
    Task SendAsync(OutboundEmail email);
}
```

The interface lives in `SharedKernel` because any module may need to send email, and no module should learn the provider's name.

**This slice ships a mock, not a real sender.** `MockEmailSender` (in `src/Host/Mail`) appends each message to an in-memory `DevEmailOutbox` — a bounded ring keeping the ~200 newest entries, newest first — and logs the full message. It never touches the network.

**Links are retrievable, not just logged.** A developer-only viewer exposes the outbox two ways, so humans and Playwright E2E tests can read verification and reset links without scraping logs:

- `GET /Dev/Outbox` — a page listing the recorded emails
- `GET /api/dev/outbox` — the same data as JSON

Both endpoints return 404 outside the Development environment.

**A future SendGrid implementation** is one new class implementing the interface plus one DI registration change in `Host`. No signup, verification, or reset logic changes.

## Consequences

**Positive**:
- Signup, verification, and reset code depend only on `ITransactionalEmailSender`; the provider choice is a Host-level detail
- Zero real email is guaranteed by construction — the only registered implementation is the mock
- E2E tests get a deterministic, machine-readable way to fetch links (the JSON endpoint)
- Adopting SendGrid later is a new class plus one DI line, not a refactor

**Negative**:
- The outbox is a dev artifact: in-memory only, so it is lost on restart by design. It is in-memory rather than MSSQL precisely because losing it on restart is acceptable
- The mock does nothing about actual delivery (no retries, no delivery status) — correct for a dev tool, but it must not be mistaken for production behavior

**Guardrail**: the `/Dev/Outbox` page and `GET /api/dev/outbox` must stay Development-gated (404 everywhere else). They expose email addresses and reset links, so they are a security hole the moment they leak into staging or production.
