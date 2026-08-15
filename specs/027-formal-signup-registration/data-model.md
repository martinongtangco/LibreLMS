# Data Model: Formal Signup & Registration

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-15

No new tables. One existing entity is extended (`Student`), plus three
non-persisted (in-memory / record) structures that back the flows.

---

## 1. Student (Enrollment.Domain — extended)

System of record: `Students` table via `EnrollmentDbContext` (MSSQL). A single new
migration (`AddRegistrationFieldsToStudent`) adds the fields marked **NEW**.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| Id | Guid | PK | Existing |
| Name | string(200) | required | Existing |
| Email | string(320) | required, **unique index** | Existing. Stored normalized (`Trim().ToLowerInvariant()`) — R8 |
| PasswordHash | string(256) | required | Existing. New self-describing PBKDF2 format `PBKDF2$<iter>$<saltB64>$<hashB64>` (~80 chars); legacy unsalted-SHA256 (base64) accepted on verify and upgraded in place — R2 |
| Roles | string(100) | required | Existing. Self-service sign-up always `Learner` |
| OrganizationId | Guid | required | Existing. Self-service sign-up uses the platform's default (root) org |
| CreatedAt | DateTimeOffset | required | Existing |
| EmailNotificationsEnabled | bool | default true | Existing, untouched |
| ThemePreference | string(50) | default "System" | Existing, untouched |
| **IsEmailVerified** | bool | **required, DB default `true`** | NEW. `false` only for self-service sign-up until the verification link is used (R10). Pre-existing and seeded rows become `true` via the column default |
| **SecurityStamp** | Guid | **required, DB default `00000000-0000-0000-0000-000000000000`** | NEW. Random Guid at account creation; rotated on password reset. Carried as a cookie claim and re-validated per request (R3) |
| **VerificationTokenHash** | string(64)? | nullable | NEW. SHA-256 hex of the pending verification token (R4) |
| **VerificationTokenExpiresAt** | DateTimeOffset? | nullable | NEW. Set with the token (24 h); cleared with it |
| **ResetTokenHash** | string(64)? | nullable | NEW. SHA-256 hex of the pending reset token (R4) |
| **ResetTokenExpiresAt** | DateTimeOffset? | nullable | NEW. Set with the token (30 min); cleared with it |

### Validation rules (from spec FRs)

- **Email** (FR-002, FR-026): unique, case-insensitive; normalized at every boundary.
  Duplicate sign-up → "email already in use"; concurrent duplicates → exactly one account
  (DB unique index is the backstop).
- **Password** (FR-003, FR-004): ≥12 chars; ≥1 uppercase; ≥1 lowercase; ≥1 digit; does
  not contain the user's full name or email (case-insensitive); not on the top-1000
  common-password blocklist. Violations rejected with the specific failed rule(s).
- **Credential storage** (FR-006): PBKDF2-HMAC-SHA256, 210k iterations, 16-byte salt;
  never displayed or logged (R2).
- **Role/org on sign-up** (FR-007): `Learner` + default org; privileged roles
  admin-assigned only.

### State transitions

```text
                        sign-up (self-service)
                                │
                                ▼
                    ┌─────────────────────────┐
                    │        UNVERIFIED        │  IsEmailVerified = false
                    │ (verification token set) │
                    └───────────┬─────────────┘
                                │ verification link used (single-use, ≤24 h)
                                ▼
                    ┌─────────────────────────┐
                    │        VERIFIED          │  IsEmailVerified = true
                    │  (sign-in permitted)     │
                    └───────────┬─────────────┘
                                │ password reset completed
                                ▼
                    VERIFIED (unchanged) + SecurityStamp rotated
                    → all pre-existing sessions invalidated (R3)
```

- Admin-created and seeded accounts are created directly **VERIFIED**.
- Expired/used verification or reset tokens are treated as invalid; issuing a new token
  overwrites the pending one; using a token clears its columns (single-use).

---

## 2. OutboundEmail (SharedKernel — record, not persisted)

```text
OutboundEmail(
    string To,          // normalized recipient email
    EmailPurpose Purpose, // Verification | Welcome | PasswordReset
    string Subject,
    string Body)        // plain text, contains the action link where applicable
```

The single payload crossing the `ITransactionalEmailSender` seam. The verification,
welcome, and reset links are embedded in `Body` (see
[contracts/email-messages.md](contracts/email-messages.md)).

## 3. DevEmailOutbox entry (Host — in-memory, dev artifact)

```text
OutboxEntry(OutboundEmail Email, DateTimeOffset SentAtUtc)
```

- Bounded ring buffer, ~200 newest-first entries, thread-safe.
- Lost on app restart by design (Constitution VI: not durable state).
- Exposed (Development environment only) at `GET /Dev/Outbox` (HTML) and
  `GET /api/dev/outbox` (JSON) — see [contracts/http-surface.md](contracts/http-surface.md).

## 4. EmailThrottle state (Enrollment/Application — in-memory)

```text
email (normalized) → recent attempt timestamps (sliding window)
```

| Flow | Cap | Window |
|---|---|---|
| Sign-up attempts | 10 | 24 h |
| Password-reset requests | 5 | 1 h |
| Verification resends | 3 | 1 h |

- Thread-safe dictionary; expired entries purged opportunistically on each call.
- Lost on restart by design (dev safeguard, R6).

---

## Relationships

- `Student` 1 — 0..1 pending verification token (columns, not a table)
- `Student` 1 — 0..1 pending reset token (columns, not a table)
- `Student` 1 — N `Enrollment` (existing, unchanged)
- `Student` N — 1 `Organization` (existing; sign-up uses the default/root org)
- `Student` — `OutboundEmail` instances (transient, at sign-up/reset time only)
