# Quickstart Validation: Clickable Brand Link to Home

**Feature**: 029-clickable-home-brand
**Date**: 2026-08-16

End-to-end validation for the brand-link feature. Behavior contract: see
[contracts/brand-link.md](contracts/brand-link.md). Data model: no changes
(see [data-model.md](data-model.md)).

## Prerequisites

1. Docker services running: `docker compose up -d` (mssql, valkey)
2. Application rebuilt and restarted after the view/CSS changes. **Razor views
   do NOT hot-reload** — a running instance serves old compiled views. Use
   `./scripts/restart-app.sh --background` from the repo root, or launch
   detached manually:
   `cd /workspace/src/Host && setsid nohup dotnet run --urls "https://localhost:7095;http://localhost:5000" > /tmp/lms-host.log 2>&1 < /dev/null &`
   If ports 5000/7095 are held by a stale process, kill it first:
   `pkill -f 'bin/Debug/net10.0/Host'; pkill -f 'dotnet run'`
3. Verify the app is healthy: `curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/Courses` → `200`

## Manual Validation Steps

### 1. Signed-out: Login page is no longer a dead end (US1, SC-001)

**Action**: In a fresh browser session (signed out), go to
`http://localhost:5000/Account/Login`. Look at the navbar.

**Expected**:
- The "Libre LMS" brand is a link (pointer cursor; hover shifts its color to
  white; no underline).
- Clicking it lands on Browse Courses (`/Courses`) in exactly one click, with
  no sign-in prompt.

### 2. Signed-out: Home shows Browse Courses (US3, SC-003)

**Action**: Open `http://localhost:5000/` while signed out.

**Expected**: Browse Courses is displayed (no landing page, no sign-in wall).

### 3. Brand on Home is idempotent (edge case)

**Action**: While on Browse Courses, click the "Libre LMS" brand.

**Expected**: Still on Browse Courses — a clean reload, no error, no loop.

### 4. Signed-in learner: brand returns to Home (US2)

**Action**: Sign in as the learner test user (see
`tests/Playwright.Tests/utils/testUsers`), open My Courses, click the brand.

**Expected**: Lands on Browse Courses; signed-in state preserved (account
name still visible in the navbar).

### 5. Signed-in admin: brand is NOT role-aware (US2, FR-006)

**Action**: Sign in as the org-admin test user, switch the role pill to
**Admin**, open the admin Dashboard, click the brand.

**Expected**: Lands on Browse Courses — **not** the admin Dashboard.

### 6. Mobile: brand clickable in collapsed nav (FR-005)

**Action**: At a 375px-wide viewport, signed out, on the Login page, tap the
"Libre LMS" brand.

**Expected**: Brand is visible (not hidden by the hamburger) and tapping it
lands on Browse Courses.

### 7. Access-denied variant (edge case)

**Action**: Signed in as the learner, navigate directly to an admin URL
(e.g., `/Admin/Dashboard/Index`).

**Expected**: The "Access denied" variant of the login page renders **with**
the brand link; clicking it lands on Browse Courses.

### 8. No nav-state regressions (FR-007)

**Action**: With the mobile hamburger menu open (signed in), click the brand.

**Expected**: Lands on Browse Courses; the hamburger menu is closed (fresh
page load resets it). Account dropdown and role toggle behave as before.

## Automated Validation (Principle XIII)

Run the new E2E spec against the running app:

```bash
cd /workspace/tests/Playwright.Tests
npx playwright test tests/11-brand-home-link.spec.ts
```

Then the full suite to confirm no regressions:

```bash
npx playwright test
```

And the architecture gate:

```bash
dotnet test /workspace/tests/ArchitectureTests
```

**Expected**: all green. The new spec covers: brand link on Login (signed
out), Home (idempotent), My Courses (learner), admin Dashboard (admin role
view, targets Browse Courses), mobile 375px, the access-denied variant, and
root-URL → Browse Courses for anonymous and signed-in users.
