# Quickstart / Validation Guide: Fix Enrollment While Logged Out (055)

**Date**: 2026-09-20
**Spec**: [spec.md](./spec.md) | **Contracts**: [enroll-action](./contracts/enroll-action.md), [return-url](./contracts/return-url.md)

Runnable validation scenarios that prove the fix end-to-end. No implementation code lives
here — this is the run/verify guide.

## Prerequisites

```bash
# from repo root (devcontainer or host with Docker)
docker compose up -d mssql valkey          # sibling services
dotnet build LibreLms.slnx                 # gate 1: compiles
# start the app (same way as usual) — http://localhost:5000, DB "LearningLms"
```

- Seeded demo learner: `alice@example.com` (id `550e8400-e29b-41d4-a716-446655440001`) —
  used below as the account a guest must NO LONGER be able to enroll as.
- Dev mail outbox API: `GET /api/dev/outbox` (newest-first; used to fetch verification links).

## Scenario 1 — Signed-out visitor cannot enroll; is redirected to sign-in (US1, P1)

**Manual (browser, incognito window):**

1. Open `http://localhost:5000/Courses/Detail/11111111-1111-1111-1111-111111111113`
   (a course the demo learner is NOT enrolled in — pick any that shows "Enroll now").
2. Click **Enroll now**.
   - **Expected**: full-page navigation to `/Account/Login?ReturnUrl=/Courses/Detail/1111…`
     (NOT a sign-in form swapped into the course card; NO "Enrolled" state).
3. Cancel out (navigate away). Reload the course page in the same window.
   - **Expected**: still "Enroll now" — no enrollment happened.

**Raw HTTP (the security core, FR-001/FR-010):**

```bash
# fresh cookie jar = no session
curl -s -c /tmp/guest.jar -o /dev/null -w "%{http_code} %{redirect_url}\n" \
  -X POST "http://localhost:5000/Courses/Detail/11111111-1111-1111-1111-111111111113?handler=Enroll"
# Expected: 302  http://localhost:5000/Account/Login?ReturnUrl=%2FCourses%2FDetail%2F1111...113
```

**Data check (no row may appear):**

```sql
-- mssql container, DB LearningLms (run BEFORE and AFTER the raw POST)
SELECT COUNT(*) FROM dbo.Enrollments
WHERE CourseId = '11111111-1111-1111-1111-111111111113'
  AND StudentId = '550e8400-e29b-41d4-a716-446655440001'
  AND EnrolledAt > '<run start time>';
-- Expected: 0
```

## Scenario 2 — Round trip: sign-in returns to the course; manual re-enroll (US1, P1)

**Manual (browser, incognito):**

1. Course page → **Enroll now** → sign-in page with `ReturnUrl` (as in Scenario 1).
2. Sign in as a learner (e.g. `bob@example.com` / seeded password — see
   `tests/Playwright.Tests/utils/testUsers.ts`).
   - **Expected**: redirected to the **same course page** (not `/`).
3. The page shows **Enroll now** (not auto-enrolled — FR-004). Click it once.
   - **Expected**: enrolled state (HTMX swap) under Bob's own account; the course appears in
     **My Courses**.

## Scenario 3 — New user: sign-up + verification returns to the course (US2, P2)

**Manual (browser, incognito):**

1. Course page → **Enroll now** → sign-in page → **Create an account**.
2. Complete sign-up → "Check your email".
3. Fetch the verification link:
   ```bash
   curl -s http://localhost:5000/api/dev/outbox | \
     python -c "import json,sys; [print(e['body']) for e in json.load(sys.stdin) if e['purpose']=='Verification']" | head -1
   ```
   open it → verified → **Go to sign in**.
4. Sign in with the new account.
   - **Expected**: redirected to the **original course page**; press **Enroll now** → enrolled.

**Automated**: `21-logged-out-enroll.spec.ts` covers this journey (and Scenarios 1/2/4/5).

## Scenario 4 — Signed-out visitors see only public state (US3, P3)

**Manual (incognito):**

1. `/MyCourses` → **Expected**: 302 to `/Account/Login?ReturnUrl=%2FMyCourses`; after
   signing in, back on `/MyCourses` showing the signed-in learner's own courses.
2. `/Courses` catalog and the course detail for `11111111-…-111111111112` (a course the demo
   learner IS enrolled in) → **Expected**: **no** "✓ Enrolled" badge and no
   "Launch SCORM Course" button for the guest; "Enroll now" is offered.
   *(Before the fix, guests saw the demo learner's enrolled state here.)*

## Scenario 5 — Tampered return address is dropped (FR-006)

```bash
curl -s -c /tmp/t.jar -o /dev/null "http://localhost:5000/Account/Login?ReturnUrl=https://evil.example/"
curl -s -b /tmp/t.jar -c /tmp/t.jar | grep -o 'name="lms.ReturnUrl"[^>]*' || echo "cookie not set  (expected)"
```
Then sign in with the same jar → lands on `/` (home), never on the foreign URL.

## Automated gates (Principle XIII)

```bash
# gate 1 — compiles
dotnet build LibreLms.slnx

# gate 2 — unit + architecture + module tests
dotnet test tests/ArchitectureTests      # boundary regression guard (Principle III)
dotnet test tests/Host.Tests             # incl. NEW ReturnUrlCookieTests (set/consume/validate/expiry)

# gate 3 — E2E against the running app
cd tests/Playwright.Tests
npx playwright test tests/21-logged-out-enroll.spec.ts   # NEW: US1–US3 journeys
npx playwright test tests/01-auth.spec.ts \
                    tests/03-enrollment.spec.ts \
                    tests/signup.spec.ts \
                    tests/verify-email.spec.ts \
                    tests/20-scorm-session-authz.spec.ts  # regression guards (FR-009, SC-004)

# gate 4 — post-merge (after merging to master): rebuild, restart, re-run the suite
```

**Pass criteria**:
- New spec green; all regression specs green **unmodified** (SC-004).
- Scenario 1's data check shows zero anonymous-attributed rows after the fix (SC-001).
- Scenarios 2–4 land exactly on the contracted pages (SC-002/SC-003/SC-005, contracts
  journey matrix J1–J4).

## Data hygiene (optional, out of scope)

The bug created demo-attributed enrollments before the fix (e.g. course
`11111111-…-111111111113` → alice, 2026-09-20). If a clean demo state is wanted:

```sql
DELETE FROM dbo.Enrollments
WHERE StudentId = '550e8400-e29b-41d4-a716-446655440001'
  AND EnrolledAt > '2026-09-20T03:30:00Z';
```

(Do this only on the dev DB, and note it in the run log — the seeder re-creates the baseline
rows on a fresh database.)
