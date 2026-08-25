# Story 040: Auth-claim contract guard — prevent a dropped cookie claim from regressing

**Feature Branch**: `story/040-auth-claim-contract-guard`

## Problem
bug-039 was the **second** dashboard outage and the second time a silent
auth/claims defect reached a user. Root pattern, per the bug-039 postmortem:

1. The sign-in claim list was **duplicated** in two builders
   (`LoginModel.OnPostAsync` and `AuthCookieRefresher.RefreshAsync`). Spec 027
   rebuilt one and dropped the `OrganizationId` claim from both.
2. **No fast test** pinned the cookie's claim shape — unit suites cover modules
   and architecture only, so a dropped claim was invisible until a human opened
   the OrgAdmin dashboard.
3. The E2E dashboard assertion accepted `0` (weak symptom check, fixed in
   bug-039).

Per Constitution III's own reasoning — "a convention that relies on memory or
code review isn't a boundary; a failing build is" — the fix is mechanical
guards, not discipline.

## Goals
- **L1 — Single source of truth**: one claim builder
  (`LibreLms.Host.ManagementAuth.AuthClaims.Build(...)`) used by *both*
  `LoginModel.OnPostAsync` and `AuthCookieRefresher.RefreshAsync`. The two
  builders can no longer drift because there is only one.
- **L2 — Contract unit test**: new `tests/Host.Tests` project (mirrors the
  existing per-area test projects; Host is the composition root and may
  reference everything) asserting the **exact** claim set produced:
  NameIdentifier, Name, Email, SecurityStamp, **OrganizationId (parseable,
  equal to the account's org)**, Role (when non-empty), AvatarPath (when
  non-empty). A future drop fails `dotnet test` in seconds, no browser needed.
- **L3 — E2E claim probe**: `04-admin-dashboard.spec.ts` gains a test that
  signs in as OrgAdmin and calls `GET /api/dashboard` in the same browser
  context: before bug-039's claim existed this endpoint 401'd for OrgAdmin, so
  a 200 + `role: "OrgAdmin"` + non-zero learner count is a *direct probe of the
  cookie's claims*, end-to-end, with no new surface.

## Non-goals
- No new dev-only endpoints (the `/api/dashboard` probe reuses existing surface).
- No changes to claim values or authorization policies — only where claims are
  built and the tests around them.
- No fix for the pre-existing `waitForHtmxSettle` flake (separate concern).

## Constitution Principles
- **II. Clean Architecture, Applied Simply** — `AuthClaims.Build` takes
  primitives only (no cross-module entity leakage), explainable in one
  sentence: "both sign-in paths get their claims from this one method."
- **IV. Human-Legible AI-Authored Code** — short static class + named test
  cases; the doc comments on both call sites now point at the single source
  of truth instead of warning about manual sync.
- **X. No Ad-Hoc Fixes** — documented here before the code edit.
- **XIII. Verification Before Claim** — build output, `dotnet test` results,
  and full Playwright suite recorded below before the merge.

## Verification (Principle XIII evidence)
- Build: `dotnet build LibreLms.slnx` → 0 errors.
- Unit tests: `dotnet test` → Scorm 1/1, **Host.Tests 5/5 (new)**, Catalog
  19/19, Architecture 14/14, Enrollment 35/35 — 74 total, 0 failures.
- **Guard proof**: temporarily commented out the OrganizationId claim line in
  `AuthClaims.Build` and re-ran `dotnet test tests/Host.Tests` → **3 of 5 tests
  failed** (exactly the claim-contract tests); restored the line → 5/5 green.
  The build now fails the moment a claim is dropped.
- Live E2E (app restarted with the story-040 build): `npx playwright test`
  full suite → **151 passed, 1 skipped, 0 failed**, including the new
  "OrgAdmin cookie carries org-scope claim: /api/dashboard returns 200" probe.

## Result
- L1: `src/Host/ManagementAuth/AuthClaims.cs` — single claim builder;
  `LoginModel.OnPostAsync` and `AuthCookieRefresher.RefreshAsync` both delegate
  to it (claim behavior unchanged, verified by the full E2E suite).
- L2: `tests/Host.Tests/AuthClaimsTests.cs` + new `Host.Tests` project in
  `LibreLms.slnx`.
- L3: `tests/Playwright.Tests/tests/04-admin-dashboard.spec.ts` — API-level
  claim probe (200 + role OrgAdmin + learnerCount ≥ 5).
