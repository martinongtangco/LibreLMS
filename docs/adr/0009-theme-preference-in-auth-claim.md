# ADR-0009: Theme Preference Carried in the Auth Cookie Claim

**Status**: Accepted
**Date**: 2026-08-29
**Supersedes**: N/A

## Context

Spec 042 adds a per-user theme preference (`System` | `Light` | `Dark`, default `System`) persisted in MSSQL `Students.ThemePreference` (Constitution VI: durable profile state belongs in the system of record). Every page render needs the value to set the visual theme, and `_Layout.cshtml` has a documented house convention of reading **only** from auth cookie claims — no service injection, no DB access in the layout. That convention was established by spec 030's `AvatarPath` claim (ADR 0007) and is what keeps the shared layout free of a per-request query.

## Decision

The preference travels as an **always-present `ThemePreference` claim** in the ASP.NET Core auth cookie. It is built exclusively by `AuthClaims.Build` (the pinned single source of truth for the claim set, tested by `tests/Host.Tests/AuthClaimsTests.cs`) and re-issued by `AuthCookieRefresher` on sign-in and after a successful theme save in the Settings page — the same RefreshSignIn pattern used for name, email, and avatar changes. Unknown or empty stored values normalize to `"System"` at claim-build time, so the layout never branches on null or unexpected values.

**Rejected alternatives**:

1. **Per-request `IActionFilter` DB lookup** — always fresh, but a query on every request and DB access leaking into the render pipeline; breaks the claim-only layout convention.
2. **Device-local browser storage** — not account-scoped; the spec requires the preference to persist as profile state across devices, with the account (MSSQL) as source of truth.
3. **Server-side session cache** — an extra mechanism for a value written through exactly one form; unexplained state fails Principle II.

Always-present (rather than present-only-when-non-default, as with `AvatarPath`) keeps the layout branch-free and makes the default explicit and testable — the value is a few bytes, so simplicity wins.

## Consequences

**Positive**:

- The layout stays 100% claim-driven, consistent with `AvatarPath` and `OrganizationId`; zero per-request DB cost
- A lost or stale cookie self-heals on the next sign-in: MSSQL is the source of truth and the cookie claim is only a derived copy (Principle VI)
- Anonymous visitors have no cookie → no claim → `System`, which falls out for free
- The claim set stays pinned and tested in one place (`AuthClaimsTests`)

**Negative**:

- The cookie grows ~30 bytes — negligible against the ~4KB limit (existing claims already carry GUIDs such as `OrganizationId` and the security stamp)
- A theme save takes effect from the **next** request, because the cookie is re-issued on save; the Settings page additionally applies the theme client-side immediately after a confirmed save, so the user sees no lag in practice

## Related

- Spec 042 (user theme preference) — research R1 (claim transport), R6 (always-present claim, pinned claim-set test)
- ADR 0007 (user avatar storage) — the claim + re-issue pattern this ADR follows
