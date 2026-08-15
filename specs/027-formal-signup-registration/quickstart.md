# Quickstart & Validation: Formal Signup & Registration

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-15

Runbook to prove the feature works end-to-end. Full implementation detail belongs in
`tasks.md`/implementation; this guide only runs and validates.

---

## Prerequisites

- Inside the devcontainer (`.devcontainer`), repo bind-mounted.
- Sibling services up: `docker compose up -d mssql valkey`
- .NET 10 SDK (pinned via `global.json`); Node (Playwright deps already in `tests/Playwright.Tests`).

## Build & run

```bash
dotnet build LibreLms.slnx
dotnet run --project src/Host        # or use the restart-host-app skill after changes
```

Expect the "Now listening" log line and seeded startup logs. The app must run with
`ASPNETCORE_ENVIRONMENT=Development` for the dev outbox to be reachable.

## Gate checks (run after build, before scenarios)

```bash
dotnet test tests/ArchitectureTests     # MUST be 14/14 green (was 12/14 on master)
```

---

## Scenario 1 — Login page cleanup (US4)

1. Open `/Account/Login` signed out.
2. **Expected**: no text containing "Demo credentials" or any seeded email/password
   anywhere on the page; visible links to *Create an account* and *Forgot your password?*

## Scenario 2 — Full sign-up → verify → sign-in (US1 + US2)

1. Open `/Account/Signup`. Fill: name `Test Learner`, email `newlearner+{n}@example.com`
   (fresh each run), a strong password (≥12 chars, upper+lower+digit, e.g.
   `Sup3rSecret!x9`), matching confirmation. Submit.
2. **Expected**: confirmation screen ("check your email"); **not** signed in.
3. Open `/Dev/Outbox` (or `GET /api/dev/outbox`).
   **Expected**: two newest entries for that address — `Verification` (with
   `/Account/Verify?token=...`) and `Welcome`.
4. Try signing in with the new account before verifying.
   **Expected**: blocked with "please verify your email" + resend option.
5. Open the verification link from the outbox.
   **Expected**: success screen ("email verified").
6. Open the same link again.
   **Expected**: "already used" error with a request-new-link option.
7. Sign in with the new account.
   **Expected**: signed in; dashboard loads (default org, learner role).

## Scenario 3 — Validation rejections (US1)

Repeat sign-up with a fresh email each time; **each** must be rejected with its specific
message (and no account created — confirm via the outbox: no emails sent):

| Input | Expected rejection |
|---|---|
| email `NEWlearner+{n}@example.com` after signing up `newlearner+{n}@example.com` | "email already in use" (case-insensitive, FR-002) |
| password `short1A` (too short) | too-short rule |
| password `alllowercase12345` | missing uppercase |
| password `ALLUPPERCASE12345` | missing lowercase |
| password `NoDigitsHere!!xxxx` | missing digit |
| password `Test Learner` / `newlearner+{n}@example.com` as password | name/email-in-password rule |
| password `password12345` (blocklisted) | common-password rule |
| confirmation ≠ password | "passwords do not match" |
| malformed email `not-an-email` | format error |

Throttle check: 11th sign-up attempt for the same email within 24 h → "try again later".

## Scenario 4 — Forgot password (US3)

1. Sign in as the Scenario-2 user in **browser context A** (keep it open).
2. Signed-out (context B): `/Account/ForgotPassword` → submit the user's email.
   **Expected**: neutral confirmation. Outbox gains a `PasswordReset` entry.
3. Submit an **unregistered** email (e.g. `ghost+{n}@example.com`).
   **Expected**: identical on-screen confirmation; **no** new outbox entry (FR-015).
4. Open the reset link; set a new strong password.
   **Expected**: "password updated — sign in".
5. Back to context A: navigate/reload any page.
   **Expected**: session invalidated → redirected to login (FR-017).
6. Reopen the reset link (used) → "already used" error; sign in with the **new**
   password in context B → success; old password → "Invalid email or password."
7. Throttle check: 6th reset request for the same email within an hour → "try again
   later".

## Scenario 5 — Seeded users unaffected (regression)

1. Sign in as `alice@example.com` / `password123` (learner) and
   `admin@example.com` / `password123` (org admin).
   **Expected**: both sign in successfully (pre-existing accounts treated as verified,
   legacy hash still verifies and is upgraded in place on next login).

## Scenario 6 — Automated E2E (Principle XIII)

```bash
cd tests/Playwright.Tests
npx playwright test tests/signup.spec.ts tests/signup-validation.spec.ts tests/forgot-password.spec.ts
npx playwright test        # full suite — all pre-existing specs must still pass
```

The new specs extract verification/reset links from `GET /api/dev/outbox` (see
[contracts/http-surface.md](contracts/http-surface.md) §7).

## Done-when evidence (Principle XIII gates)

1. `dotnet build` output shown; app restarted; "Now listening" log shown.
2. Architecture tests 14/14; new + full Playwright suite passing output shown.
3. Post-merge to `master`: rebuild, restart, re-run Playwright; passing output shown.
