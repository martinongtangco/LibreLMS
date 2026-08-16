# Tasks: Editable User Profile With Photo & Course History

**Input**: Design documents from `/specs/030-editable-user-profile/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: INCLUDED — mandatory in this project (Constitution XIII: a feature is not done
without E2E evidence; SC-002/SC-003/SC-004 require observable verification). New Playwright
specs follow the TDD order: write the spec, watch it fail, then implement.

**Organization**: Tasks are grouped by user story (US1/US2/US3 from spec.md) to enable
independent implementation and testing. Note: US1–US3 all extend the same Razor page
(`src/Host/Pages/Account/Profile.cshtml[.cs]`), so their page tasks run sequentially;
their E2E specs, dev page, CSS, and ADR are disjoint files and run in parallel `[P]`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths included in every task

## Path Conventions

Single modular-monolith project: `src/Host/...` (web portal + composition root),
`src/Modules/...` (modules + `*.Contracts`), `tests/Playwright.Tests/...` (E2E),
`docs/adr/...`.

---

## Phase 1: Setup

**Purpose**: Branch + shared scaffolding (Constitution VIII: implementation on a
`story/` branch from master)

- [ ] T001 Create implementation branch `story/030-editable-user-profile` from `master` (`git checkout -b story/030-editable-user-profile master`)
- [ ] T002 [P] Add `src/Host/wwwroot/avatars/` directory with a `.gitkeep` and ignore generated avatar files in `.gitignore` (pattern: `src/Host/wwwroot/avatars/*` with `!src/Host/wwwroot/avatars/.gitkeep`)

**Checkpoint**: Branch ready; avatar storage location exists

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema, DTO projection, and cookie infrastructure that every user story
depends on (per research.md R2/R3/R4)

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T003 Add nullable `AvatarPath` property (URL path, max 200 chars) to `src/Modules/Enrollment/Domain/Student.cs`; map it in `src/Modules/Enrollment/Infrastructure/EnrollmentDbContext.cs` (`entity.Property(e => e.AvatarPath).HasMaxLength(200)`, nullable); generate migration `AddAvatarPathToStudent` in `src/Host/Migrations/Enrollment/` (`dotnet ef migrations add AddAvatarPathToStudent --project src/Host` — verify `NVARCHAR(200) NULL` in the generated up-migration)
- [ ] T004 [P] Add `string? AvatarPath` to the `StudentProvisionedDto` record in `src/Modules/Enrollment.Contracts/IUserProvisioning.cs` and populate it from the `Student` row in the projection in `src/Modules/Enrollment/Application/UserProvisioningService.cs` (additive field; all existing call sites must keep compiling)
- [ ] T005 [P] Create `src/Host/ManagementAuth/AvatarClaimTypes.cs` (static class, `public const string AvatarPath = "AvatarPath"` — mirror `OrgClaimTypes.cs` style) and `src/Host/ManagementAuth/AuthCookieRefresher.cs` (single method `Task RefreshAsync(HttpContext context, StudentProvisionedDto student)`: builds claims `NameIdentifier`, `Name`, `Email`, custom `SecurityClaims.SecurityStamp`, `Role` (when non-empty), `AvatarClaimTypes.AvatarPath` (when non-null), creates a `ClaimsIdentity` for the `"Cookie"` scheme, signs in with `IsPersistent = true`; claim shape must be identical to `LoginModel.OnPostAsync` so the `OnValidatePrincipal` stamp check is unaffected)
- [ ] T006 Add the `AvatarPath` claim to the sign-in claim list in `src/Host/Pages/Account/Login.cshtml.cs` (after the Role claim: `if (!string.IsNullOrWhiteSpace(student.AvatarPath)) claims.Add(new Claim(AvatarClaimTypes.AvatarPath, student.AvatarPath));`) — depends on T004, T005

**Checkpoint**: Foundation ready — build green (`dotnet build LibreLms.slnx`), app starts and seeds with the new column; user stories can begin

---

## Phase 3: User Story 1 - Edit My Display Name (Priority: P1) 🎯 MVP

**Goal**: Self-service name editing on `/Account/Profile`, gated on the account's
email-verified state (FR-001/002/003/004), with the new name reflected in the
upper-right nav via cookie re-issue (R1/R2/R8).

**Independent Test**: Quickstart scenarios 1 + 2 (`specs/030-editable-user-profile/quickstart.md`): sign in as seeded learner `alice@example.com` / `password123` → change name on `/Account/Profile` → success + new name in the nav on the resulting page; then flip unverified via `/Dev/Unverify` → save refused with verification banner + working resend → re-verify via `/Dev/Outbox` link → save succeeds.

### Tests for User Story 1 (write FIRST, must FAIL before implementation)

- [ ] T007 [P] [US1] Create E2E spec `tests/Playwright.Tests/tests/12-profile-name.spec.ts` — covers: profile renders editable name pre-filled; valid save succeeds and the upper-right `.account-name` shows the new name without re-login; empty-name rejection; >100-char rejection (use `authFixture.loginAs` + `testUsers.learner` from `tests/Playwright.Tests/fixtures/authFixture.ts` and `tests/Playwright.Tests/utils/testUsers.ts`; assert admin learner list also shows the new name)
- [ ] T008 [P] [US1] Create E2E spec `tests/Playwright.Tests/tests/13-profile-verification-gate.spec.ts` — covers: sign in as learner → `GET /Dev/Unverify?email=...` flips state → name save refused (nav name unchanged, verification banner + resend button visible, no success message) → resend shows neutral "email sent" message → verify via the outbox link → re-sign-in → name save succeeds (SC-002)

### Implementation for User Story 1

- [ ] T009 [P] [US1] Create Development-gated verification-toggle page: `src/Host/Pages/Dev/Unverify.cshtml` + `src/Host/Pages/Dev/Unverify.cshtml.cs` — `[Authorize]` + `IWebHostEnvironment.IsDevelopment()` (non-dev → `NotFound()`), query param `email`, flips `IsEmailVerified = false` for the normalized-email match via the Enrollment module (e.g. through `RegistrationService` or `IUserProvisioning` + context — keep it one small method), renders "unverified {email}" or "no account for {email}" (mirror the `/Dev/Outbox` page pattern; per R7)
- [ ] T010 [US1] Rewrite `src/Host/Pages/Account/Profile.cshtml.cs`: `OnGetAsync` loads fresh account state via `IUserProvisioning.GetByIdAsync(studentId from NameIdentifier claim)` exposing `Name`, `Email`, `RoleLabel` (from role claim as today), `IsEmailVerified`; `OnPostNameAsync(string name)` — trim, validate non-empty/≤100 chars/no `\r\n` (field errors, persist nothing), gate on `IsEmailVerified` (unverified → set verification-banner state, call no update), else `IUserProvisioning.UpdateAsync(studentId, trimmedName, null, null)` then `AuthCookieRefresher.RefreshAsync(...)` with the updated row, success message; `OnPostResendAsync()` mirroring `LoginModel.OnPostResendAsync` via `RegistrationService.ResendVerificationAsync(email, $"{Request.Scheme}://{Request.Host}")`; friendly error messages on service/DB failure (per R1/R8)
- [ ] T011 [US1] Update `src/Host/Pages/Account/Profile.cshtml`: make the Name row an editable text input inside an anti-forgery form (`maxlength="100"`); add the verification banner (visible only when `!Model.IsEmailVerified`: "A verified email is required to save changes" + **Resend verification link** button posting to the resend handler) with its neutral result message; keep Email and Role rows read-only (FR-012); success/error message area (follow the Settings page message pattern)

**Checkpoint**: US1 complete — `npx playwright test 12-profile-name 13-profile-verification-gate` green; name change visible in nav without re-login (FR-004); **MVP VALIDATION POINT**

---

## Phase 4: User Story 2 - Enrolled & Completed Courses on Profile (Priority: P1)

**Goal**: A "My Courses" area on the profile listing every enrollment grouped into
**Completed** (any attempt `completed`/`passed`) and **Enrolled** (everything else, with
status labels) — FR-005/006/007, per R6.

**Independent Test**: Quickstart scenario 3: sign in as a learner with enrollments →
`/Account/Profile` shows both sections with correct titles + status labels, each course
in exactly one section; a finished SCORM attempt moves its course to Completed; a retake
keeps it in Completed; a user with no enrollments sees the empty state, never an error.

### Tests for User Story 2 (write FIRST, must FAIL before implementation)

- [ ] T012 [P] [US2] Create E2E spec `tests/Playwright.Tests/tests/14-profile-courses.spec.ts` — covers: enrolled courses appear under "Enrolled" with a status label; a course with a completed attempt appears under "Completed" (use seeded attempt state or finish a seeded SCORM course); a course appears in exactly one section; empty state text for a user with no enrollments; personal details (name/email) still render when the courses area is present

### Implementation for User Story 2

- [ ] T013 [US2] Extend `src/Host/Pages/Account/Profile.cshtml.cs`: `OnGetAsync` also loads `EnrollmentService.GetMyEnrollmentsAsync(studentId)` + `ScormAttemptService.GetMyAttemptsAsync(studentId)` (inject both, as MyCourses does); compute `CompletedCourses` (∃ attempt with status `completed`/`passed`) and `EnrolledCourses` (remainder, each labeled via `ScormHelpers.GetDisplayLabel(latest attempt status)` or neutral "Enrolled" when no attempt); wrap course loading in try/catch so a failure sets a friendly `CoursesError` message while personal details still render (FR-014)
- [ ] T014 [US2] Extend `src/Host/Pages/Account/Profile.cshtml`: render the "My Courses" area after the personal card — two visually distinct sections **Completed** and **Enrolled** (course title + status label per row), empty state "You haven't enrolled in any courses yet" when the user has no enrollments, and the inline `CoursesError` message when loading failed

**Checkpoint**: US1 + US2 both independently functional — `npx playwright test 12-profile-name 13-profile-verification-gate 14-profile-courses` green

---

## Phase 5: User Story 3 - Display Photo & Nav Avatar (Priority: P2)

**Goal**: Upload/replace a display photo on the profile; photo + name visible in the
upper-right nav for all users, hidden for admin-role users while the nav is in the
Admin view (Q1 = C) — FR-008/009/010/011, per R3/R4/R5.

**Independent Test**: Quickstart scenario 4 (+ scenario 5 for unauthenticated): learner uploads PNG → success + photo on profile AND next to the name in the nav on the resulting page; second upload replaces the first (old URL 404s); non-image/oversized upload rejected with previous photo intact; admin user sees the avatar only in the Learner view (hidden in Admin view), photo always on the profile page; photo-less user sees initials placeholder, never a broken image.

### Tests for User Story 3 (write FIRST, must FAIL before implementation)

- [ ] T015 [P] [US3] Create E2E spec `tests/Playwright.Tests/tests/15-profile-photo.spec.ts` — covers: upload a small PNG (add a fixture image, e.g. `tests/Playwright.Tests/fixtures/avatar-64.png`, via `page.setInputFiles`) → success + `.account-avatar` img visible with the new `/avatars/...` src; replacement upload changes the src and the old URL returns 404; uploading a text file (or >5 MB file) is rejected and the previous avatar src is unchanged; admin user (`testUsers.orgAdmin` or `superUser`): avatar hidden when the role pill is on Admin (default), visible after clicking the Learner segment; photo-less user shows the initials fallback element; anonymous `GET /Account/Profile` redirects to login (FR-013)

### Implementation for User Story 3

- [ ] T016 [US3] Extend `src/Host/Pages/Account/Profile.cshtml.cs`: `OnPostPhotoAsync(IFormFile avatar)` — validate file present, extension + MIME within {jpg/jpeg/png/webp/gif} (case-insensitive) and size ≤ 5 MB (friendly error, nothing written); target path `wwwroot/avatars/{studentId-lower}{ext}` via `IWebHostEnvironment.ContentRootPath` (temp file → move, per R4); delete the previous file when it exists and differs; update `Student.AvatarPath` to the URL path `/avatars/{file}` (via the Enrollment module, e.g. `IUserProvisioning` — extend the update path if no single-call mutation exists for this column, keeping the change inside the Enrollment module); re-issue the cookie via `AuthCookieRefresher`; friendly error on disk failure leaving the previous photo untouched (FR-010)
- [ ] T017 [US3] Extend `src/Host/Pages/Account/Profile.cshtml`: add the photo form (file input `accept=".jpg,.jpeg,.png,.webp,.gif"` posting to the photo handler) with the current photo (or initials placeholder) previewed next to the personal card, and success/error message area reuse
- [ ] T018 [US3] Edit `src/Host/Pages/Shared/_Layout.cshtml`: inside `.account-control`, render before `.account-name` an avatar element sourced purely from claims — `var avatarPath = User.FindFirstValue(AvatarClaimTypes.AvatarPath);` → `<img class="account-avatar" src="@avatarPath" alt="">` when non-empty, else `<span class="account-avatar account-avatar-fallback" aria-hidden="true">@(first letter of User.Identity.Name, uppercased)</span>`; do not add any service injection or DB access to the layout (R3)
- [ ] T019 [P] [US3] Add avatar styles to `src/Host/wwwroot/css/site.css`: `.account-avatar` (small circular image, aligned with `.account-name`, sizing consistent with existing nav spacing) and `.account-avatar-fallback` (same circle, centered initial); plus the Q1=C rule `.role-admin .account-avatar { display: none; }` (works because the role pill JS already sets `role-admin`/`role-learner` on `<body>` and pure Learners never get `role-admin`); check the mobile nav blocks (~lines 1348/1516) for consistent sizing
- [ ] T020 [P] [US3] Write ADR `docs/adr/0007-user-avatar-storage.md` (context → decision → consequences, ≤1 page per Constitution IV): avatar files on disk under `wwwroot/avatars/` with GUID-keyed filenames (no user-controllable path), `Student.AvatarPath` URL column in MSSQL, `AvatarPath` cookie claim + cookie re-issue (R2/R3), and the explicit trade-off that avatar URLs are effectively public (unguessable GUID names; acceptable for a non-confidential display photo)

**Checkpoint**: All three user stories independently functional — `npx playwright test 12-profile-name 13-profile-verification-gate 14-profile-courses 15-profile-photo` green

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Constitution XIII verification gates (with concrete evidence) and merge

- [ ] T021 Build + run verification (XIII gate 1): `dotnet build LibreLms.slnx` (show output), restart via `scripts/restart-app.sh` / restart-host-app skill, confirm the "Now listening" log line and a 200 from the app
- [ ] T022 Module-boundary gate: `dotnet test tests/ArchitectureTests` — must be 14/14 green (Constitution III)
- [ ] T023 Run the four new E2E specs: `cd tests/Playwright.Tests && npx playwright test 12-profile-name 13-profile-verification-gate 14-profile-courses 15-profile-photo` — show passing output (XIII gate 2)
- [ ] T024 Full E2E regression: `npx playwright test` (entire suite, no regressions)
- [ ] T025 Walk through the manual quickstart scenarios 1–5 in `specs/030-editable-user-profile/quickstart.md` against the running dev app (includes `/Dev/Unverify` 404-when-signed-out, static avatar 404, and admin Learners list showing the renamed user)
- [ ] T026 Merge to master + post-merge regression (XIII gate 3 + Constitution XII/XIII): from the branch, `git checkout master && git merge --no-ff story/030-editable-user-profile` with a descriptive merge message (spec 030); rebuild + restart on master; re-run the full Playwright suite on the merged code and show passing output; remain on `master` (Constitution XII)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 first (branch); T002 [P] after T001
- **Foundational (Phase 2)**: depends on Setup — T003/T004/T005 run in parallel [P]
  (disjoint files); T006 depends on T004 + T005. **Blocks all user stories.**
- **User Stories (Phases 3–5)**: all depend on Phase 2. US1 → US2 → US3 are ordered
  because they all extend the same page files (`Profile.cshtml[.cs]`); within each
  story, the E2E spec(s) + dev page/CSS/ADR are parallelizable with the page work
  (disjoint files).
- **Polish (Phase 6)**: depends on all stories; T021–T025 parallelizable [P] (all
  read-only validation) until T026 (merge, final).

### User Story Dependencies

- **US1 (P1)**: after Foundational only — no other-story dependency (MVP)
- **US2 (P1)**: after Foundational; page tasks sequential behind US1's page tasks
  (same files), but its E2E spec (T012) is independent
- **US3 (P2)**: after Foundational; page tasks sequential behind US1/US2's page tasks
  (same files); T018 (layout) depends on T005/T006 (claim infrastructure); T019/T020
  are independent files

### Within Each User Story

- E2E spec written and confirmed failing **before** implementation (TDD)
- Page model before page markup; page markup before story checkpoint
- Story checkpoint (its E2E specs green) before starting the next story's page work

### Parallel Opportunities

- Phase 2: T003, T004, T005 together (disjoint files)
- US1: T007, T008, T009 together (test files + dev page are disjoint from the Profile page)
- US2: T012 in parallel with T013/T014
- US3: T015, T019, T020 in parallel with T016/T017/T018
- Cross-story: with subagent worktrees (Constitution XI), the E2E specs (T007/T008/T012/T015), T009, T019, T020 form disjoint-file parallel lanes; only the Profile page tasks are a single-writer lane

---

## Parallel Example: User Story 1

```text
# After Phase 2 checkpoint, launch together (disjoint files):
Task: "T007 E2E spec 12-profile-name.spec.ts"
Task: "T008 E2E spec 13-profile-verification-gate.spec.ts"
Task: "T009 Dev/Unverify page"

# Then (single writer — same page files):
Task: "T010 Profile.cshtml.cs (name save + gate + resend)"
Task: "T011 Profile.cshtml (name form + banner)"

# Verify:
npx playwright test 12-profile-name 13-profile-verification-gate
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (branch + avatars dir)
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: quickstart scenarios 1–2 + the two US1 E2E specs green
5. Demo: verified learner changes name → new name in nav immediately; unverified save refused with working resend

### Incremental Delivery

1. Setup + Foundational → foundation ready (build green)
2. US1 → validate → **MVP** (editable name with verification gate)
3. US2 → validate → profile becomes a personal learning record
4. US3 → validate → photo + nav avatar with admin-view rule (Q1=C)
5. Polish → XIII gates with evidence → merge to master → post-merge re-run → back on master

### Parallel Team Strategy (Constitution XI — subagents)

1. Parent session completes Setup + Foundational
2. After the Phase 2 checkpoint:
   - Lane A (worktree): T007+T008+T009 (US1 E2E + dev page)
   - Lane B (single writer, main branch): T010+T011, then T013+T014, then T016+T017+T018 (the Profile page lane — one writer per file set)
   - Lane C (worktree): T012, T015 (remaining E2E specs)
   - Lane D (worktree): T019 (CSS), T020 (ADR)
3. Parent synthesizes, applies integration fixes, and owns the final merge (T026) —
   never delegated to a child

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] labels: US1 = name editing + verification gate, US2 = course history, US3 = photo + nav avatar
- The Profile page lane (T010→T011→T013→T014→T016→T017) is deliberately sequential —
  one writer per file set; E2E/CSS/ADR/dev-page tasks are the parallel lanes
- Commit after each task or logical group; branch message convention `feat(030): ...`
- Stop at any checkpoint to validate the story independently before continuing
- User instruction for this slice: planning artifacts (spec/plan/tasks) merge to master
  as documentation; **no code is implemented by the planning session** — implementation
  starts at T001 on the story branch during `/speckit.implement`
