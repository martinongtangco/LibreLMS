# ADR-0006: Credential Security Baseline

**Status**: Accepted
**Date**: 2026-08-15
**Supersedes**: N/A

## Context

Spec 027 (formal signup & registration) must satisfy two security requirements the current implementation does not meet:

- **FR-006** requires passwords to be stored as a salted one-way value. Today passwords are stored as unsalted SHA-256 base64 (what the seeder and the login flow use), which fails the requirement.
- **FR-017** requires ALL pre-existing sessions to die after a password reset. ASP.NET Core cookie auth is stateless — there is no server-side session to revoke — so there is currently no way to invalidate a user's sessions from the server side.

The dev database also persists across runs (`Database.Migrate()`, no drop), so seeded and pre-existing accounts must keep verifying without re-seeding.

## Decision

### Password storage: built-in PBKDF2-HMAC-SHA256, self-describing, with legacy upgrade

`PasswordHasher` (Enrollment/Application) derives keys with built-in PBKDF2-HMAC-SHA256 — no new NuGet package — using 210,000 iterations, a 16-byte random salt, and a 32-byte derived hash. The derivation uses `Rfc2898DeriveBytes` (the .NET-recommended `KeyDerivation.Pbkdf2` static class is absent from this environment's .NET 10 runtime build; `Rfc2898DeriveBytes` is the identical algorithm). Hashes are stored in a self-describing format:

```
PBKDF2$<iterations>$<saltBase64>$<hashBase64>
```

`Verify` tries the new format first, then falls back to the legacy unsalted-SHA256 format; on a successful verification of a legacy hash it transparently re-hashes to PBKDF2 (password upgrade on next login or reset). Seeded and pre-existing accounts keep working while the database converges to the new format.

**Rejected alternatives**: BCrypt and Argon2id would require a new NuGet / native dependency without a specific problem the built-in API fails to solve. ASP.NET Core Identity's `PasswordHasher<T>` would drag Identity's model into a non-Identity app.

### Session invalidation: `SecurityStamp` re-validation

`Student` gets a `SecurityStamp` column (Guid, generated randomly at account creation). The sign-in cookie carries the stamp as a claim; cookie authentication's `OnValidatePrincipal` event re-checks the stamp against the database on each request and signs the user out on mismatch or a missing claim. A password reset rotates the stamp, invalidating all pre-existing sessions — the FR-017 guarantee.

**Rejected alternatives**: a server-side session-token table would add a new table, a per-request join, and cleanup logic for the same guarantee the stamp check already gives; cookie absolute-expiry tricks cannot target one user's cookie from the server side.

## Consequences

**Positive**:
- Logins cost ~100–200 ms of PBKDF2 — inside spec 027's <500 ms sign-in goal
- The self-describing hash format makes the legacy → PBKDF2 upgrade path safe and auditable
- Every account-creation path (self-service signup, admin creation, seeder) goes through the same `CredentialPolicy` + `PasswordHasher` core, so the rules stay consistent in one place

**Negative**:
- Pre-existing cookies issued without a stamp claim are invalidated at first validation: affected users sign in once after deployment — accepted and documented
- `OnValidatePrincipal` performs one indexed primary-key lookup per request (trivial at dev scale; a TTL cache was rejected because it would delay reset invalidation by up to the TTL and weaken the FR-017 guarantee)

## Related

- Spec 027 (formal signup & registration) — research R2 (password storage) and R3 (session invalidation on reset)