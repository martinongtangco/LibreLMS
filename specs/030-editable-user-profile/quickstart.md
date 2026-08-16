# Quickstart & Validation: Editable User Profile With Photo & Course History

**Feature**: [spec.md](spec.md) | **Date**: 2026-08-16

Runbook to prove the feature works end-to-end. Full implementation detail belongs in
`tasks.md`/implementation; this guide only runs and validates.

---

## Prerequisites

- Inside the devcontainer (`.devcontainer`), repo bind-mounted.
- Sibling services up: `docker compose up -d mssql valkey`
- .NET 10 SDK (pinned via `global.json`); Node (Playwright deps already in
  `tests/Playwright.Tests`).
- App running with `ASPNETCORE_ENVIRONMENT=Development` (required for `/Dev/Outbox` and
  `/Dev/Unverify`).

## Build & run

```bash
dotnet build LibreLms.slnx
dotnet run --project src/Host        # or use the restart-host-app skill after changes
```

Expect the "Now listening" log line and seeded startup logs (dev startup drops,
recreates, migrates, and seeds the DB — `Student.AvatarPath` column appears).

## Gate checks (run after build, before scenarios)

```bash
dotnet test tests/ArchitectureTests     # MUST be 14/14 green (module-boundary gate)
```

## E2E (Constitution XIII gate 2)

```bash
cd tests/Playwright.Tests
npx playwright test 12-profile-name 13-profile-verification-gate 14-profile-courses 15-profile-photo
npx playwright test                     # full suite — no regressions
```

---

## Scenario 1 — Edit name as a verified learner (US1, FR-001/003/004)

1. Sign in as the seeded learner `alice@example.com` / `password123`.
2. Open `/Account/Profile`.
3. **Expected**: editable Name field pre-filled `Alice Johnson`; read-only Email + Role;
   no photo → initials placeholder "A" on the profile and in the upper-right nav.
4. Change the name to `Alice J. Smith` and save.
5. **Expected**: success message; upper-right nav now reads `Alice J. Smith` **on the
   resulting page** (cookie re-issued — no re-login needed, FR-004).
6. Submit an empty name. **Expected**: field-level "name required" message, nothing
   saved. Submit a 150-character name. **Expected**: too-long message, nothing saved.
7. Open `/Admin/Learners` as `admin@librelms.local` and confirm the learner list shows
   the new name (single source of truth updated).

## Scenario 2 — Verification gate (US1 negative, FR-002, SC-002)

1. Signed in as `alice@example.com`, open `GET /Dev/Unverify?email=alice@example.com`
   (Development-gated; 404 outside dev).
2. **Expected**: "unverified alice@example.com".
3. Reload `/Account/Profile`; attempt to save a new name.
4. **Expected**: change **not** applied (nav name unchanged); verification banner:
   verified email required + **Resend verification link** button; no success message.
5. Click resend. **Expected**: neutral "verification email sent" message (check
   `/Dev/Outbox` for the new `Verification` entry).
6. Open the verification link from the outbox, then sign in again as alice.
7. **Expected**: name change now saves successfully (gate passed after verification).
8. Signed out, `GET /Dev/Unverify`. **Expected**: sign-in redirect (auth required).

## Scenario 3 — Enrolled & Completed courses (US2, FR-005/006/007)

Setup: as learner alice, enroll in two catalog courses via *Browse Courses* (enrollment
persists from the seed state; re-enrollment of already-enrolled courses is a no-op).

1. Open `/Account/Profile`.
2. **Expected**: "My Courses" area with two sections — **Completed** and **Enrolled**;
   every enrollment appears in exactly one section; Enrolled rows carry a status label
   (e.g., "Not started" / "In progress"); completed rows are visually distinct.
3. Launch one of the enrolled SCORM courses from *My Courses* and finish it in the
   SCORM shell (or use the seeded completed-attempt state if present for another
   learner, e.g. bob) and return to `/Account/Profile`.
4. **Expected**: that course moved to **Completed** (status `completed`/`passed`).
5. Start the completed course again (new attempt) and return.
6. **Expected**: it **stays** in Completed (edge case: retake never loses completion).
7. Sign in as a user with no enrollments (e.g. carol if she has none, or a fresh
   sign-up + verify). **Expected**: empty state "You haven't enrolled in any courses
   yet" — no error, personal details intact.

## Scenario 4 — Display photo + nav visibility (US3, FR-008/009/010/011, Q1=C)

Use a small test image (e.g. 64×64 PNG in `tests/Playwright.Tests/` or any local file).

1. Signed in as alice (Learner, no admin role): upload the PNG on `/Account/Profile`.
2. **Expected**: success; photo shown on the profile; photo shown **next to the name in
   the upper-right nav** on the resulting page.
3. Upload a second, different image. **Expected**: it replaces the first everywhere;
   old file no longer served (`/avatars/...old` → 404).
4. Upload a `.txt` file renamed to `.png` (or a >5 MB image). **Expected**: friendly
   rejection; previous photo unchanged in nav + profile.
5. Sign in as `admin@librelms.local` (OrgAdmin/SuperUser — has the Learner/Admin pill).
   Upload their own photo on `/Account/Profile`.
6. **Expected**: with the pill on **Admin** (default view) the avatar is **hidden**
   next to the name; switch the pill to **Learner** — the avatar **appears**; the
   profile page shows the photo in both views.
7. Sign in as a photo-less learner (fresh sign-up + verify). **Expected**: initials
   placeholder in the nav and on the profile — never a broken image.
8. `GET` the avatar URL from the page. **Expected**: 200 with the image bytes
   (static file); a made-up `/avatars/00000000-0000-0000-0000-000000000000.png` → 404.

## Scenario 5 — Unauthenticated access (FR-013)

1. Signed out, open `/Account/Profile`.
2. **Expected**: redirect to `/Account/Login`; no profile data in the response.

---

## Post-merge regression (Constitution XIII gate 3)

After merging `story/030-editable-user-profile` into `master`:

```bash
git checkout master && git pull
dotnet build LibreLms.slnx
dotnet run --project src/Host      # restart via restart-host-app skill
cd tests/Playwright.Tests && npx playwright test   # full suite, passing output shown
```

Evidence to show for the completion claim: build output, "Now listening" line,
ArchitectureTests 14/14, the four new E2E specs passing, full suite passing, and the
post-merge re-run passing.
