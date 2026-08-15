# Tasks: Formal Signup & Registration

**Input**: Design documents from `/specs/027-formal-signup-registration/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: E2E test tasks ARE included — the project constitution (Principle XIII) mandates
Playwright evidence before a feature can be claimed complete. No dedicated unit-test tasks
(were not requested); E2E specs are the verification surface.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing of each story.

**Branch**: All implementation tasks run on `story/027-formal-signup-registration`
(Constitution Principle VIII). Planning artifacts (spec/plan/tasks) already live on master.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Modular monolith: `src/Host/` (web host), `src/Modules/<Module>/` +
  `src/Modules/<Module>.Contracts/`, `src/SharedKernel/`, `src/Host/Migrations/<Module>/`
- Tests: `tests/ArchitectureTests/` (xUnit gate), `tests/Playwright.Tests/` (E2E)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Branch + shared resources that have no dependencies

- [X] T001 Create implementation branch `story/027-formal-signup-registration` from `master` at repo root (`git checkout -b story/027-formal-signup-registration master`) — Constitution Principle VIII
- [X] T002 [P] Write ADR `docs/adr/0004-transactional-email-seam.md` (context → decision → consequences: `ITransactionalEmailSender` seam in SharedKernel, mock implementation now, SendGrid as a future DI swap — per research R5)
- [X] T003 [P] Write ADR `docs/adr/0005-credential-security-baseline.md` (context → decision → consequences: PBKDF2-SHA256 210k with legacy-SHA256 verify-and-upgrade, `SecurityStamp` cookie re-validation for session invalidation — per research R2/R3)
- [X] T004 [P] Add top-1000 common-password blocklist as `src/Modules/Enrollment/Resources/common-passwords.txt` (one lowercase entry per line, trimmed from a standard public most-common-passwords list) and mark it an embedded resource in `src/Modules/Enrollment/Enrollment.csproj`

**Checkpoint**: Branch ready; blocklist + ADRs in place.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: (a) Restore the module-boundary gate to green (baseline is RED on master:
`ModuleBoundaryTests` 12/14 — research R9), and (b) build the shared credential/email
infrastructure every user story depends on. All changes in (a) are **behavior-preserving**.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete (T028 gate green).

### Boundary contracts (new files, all parallel)

- [X] T005 [P] Create `IUserProvisioning` + `StudentProvisionedDto` in `src/Modules/Enrollment.Contracts/IUserProvisioning.cs` per `contracts/module-contracts.md` (create with role/org/`isVerified`, get, list by org, update, delete, `ExistsByEmailAsync`)
- [X] T006 [P] Create `IUserLookup` + `UserScopeInfo`/`OrgLearnerCount` DTOs in `src/Modules/Enrollment.Contracts/IUserLookup.cs` per `contracts/module-contracts.md`
- [X] T007 [P] Create `IEnrollmentAdmin` + `AdminEnrollResult`/`AdminEnrollmentInfo`/`RecentEnrollmentInfo` DTOs in `src/Modules/Enrollment.Contracts/IEnrollmentAdmin.cs` per `contracts/module-contracts.md`
- [X] T008 [P] Extend `ICourseLookup` with `CountAsync()`, `CountByOrgAsync(Guid)`, `GetCoursesAsync(IEnumerable<Guid>)` in `src/Modules/Catalog.Contracts/ICourseLookup.cs`
- [X] T009 [P] Create `ICourseAdmin` with `DeleteAsync(Guid)` in `src/Modules/Catalog.Contracts/ICourseAdmin.cs`
- [X] T010 [P] Create `ITransactionalEmailSender` + `EmailPurpose` enum + `OutboundEmail` record in `src/SharedKernel/ITransactionalEmailSender.cs` per `contracts/module-contracts.md` (namespace `LibreLms.SharedKernel`)

### Shared domain + host infrastructure (parallel where marked)

- [X] T011 [P] Add `Student` fields per `data-model.md` — `IsEmailVerified` (required, DB default **true**), `SecurityStamp` (required, DB default `00000000-0000-0000-0000-000000000000`), `VerificationTokenHash`/`VerificationTokenExpiresAt`/`ResetTokenHash`/`ResetTokenExpiresAt` (nullable) — in `src/Modules/Enrollment/Domain/Student.cs` + EF configuration in `src/Modules/Enrollment/Infrastructure/EnrollmentDbContext.cs`, then generate migration `AddRegistrationFieldsToStudent` in `src/Host/Migrations/Enrollment/`
- [X] T012 [P] Implement `PasswordHasher` in `src/Modules/Enrollment/Application/PasswordHasher.cs`: PBKDF2-HMAC-SHA256 (210,000 iterations, 16-byte salt, 32-byte hash) with self-describing format `PBKDF2$<iter>$<saltB64>$<hashB64>`; `VerifyAsync` accepts the legacy unsalted-SHA256 format and signals "upgrade needed"; `UpgradeToPbkdf2` re-hash (research R2)
- [X] T013 [P] Implement `DevEmailOutbox` in `src/Host/Mail/DevEmailOutbox.cs`: thread-safe bounded ring (~200 newest-first `OutboxEntry(OutboundEmail, SentAtUtc)`), `Add`, `List`, `Clear` (research R5)

### Contract implementations + seam implementations

- [X] T014 Implement `CredentialPolicy` in `src/Modules/Enrollment/Application/CredentialPolicy.cs`: strict rules (≥12 chars, upper+lower+digit, no full name/email case-insensitively, not on blocklist) returning the specific failed rule(s); loads the T004 blocklist once into a `HashSet<string>` (FR-003/FR-004)
- [X] T015 Implement `UserProvisioningService` (IUserProvisioning) in `src/Modules/Enrollment/Application/UserProvisioningService.cs` over `EnrollmentDbContext`: create (normalize email case-insensitively, enforce `CredentialPolicy`, hash via `PasswordHasher`, random `SecurityStamp`), get/list/update/delete, `ExistsByEmailAsync` (FR-002/FR-003/FR-006; depends on T005, T012, T014)
- [X] T016 Implement `UserLookupService` (IUserLookup) in `src/Modules/Enrollment/Application/UserLookupService.cs` (scope by id, learner counts total/per-org; depends on T006, T011)
- [X] T017 Implement `EnrollmentAdminService` (IEnrollmentAdmin) in `src/Modules/Enrollment/Application/EnrollmentAdminService.cs`: enroll/enroll-many (existence + duplicate checks), unenroll, student enrollments with course titles via `ICourseLookup`, totals, recent enrollments with learner info (depends on T007, T008)
- [X] T018 Implement the `ICourseLookup` extensions + `ICourseAdmin.DeleteAsync` in the existing Catalog application service (`src/Modules/Catalog/Application/`) and register in `src/Modules/Catalog/Endpoints/CatalogModuleExtensions.cs` (depends on T008, T009)
- [X] T019 Implement `MockEmailSender` (ITransactionalEmailSender) in `src/Host/Mail/MockEmailSender.cs`: appends to `DevEmailOutbox` + logs the full message; never sends anything real; failures logged, never thrown to callers (FR-020/FR-021/FR-022; depends on T010, T013)
- [X] T020 [P] Create Development-only outbox viewer page in `src/Host/Pages/Dev/Outbox.cshtml` + `Outbox.cshtml.cs` (To/Purpose/Subject/Body table, clickable links, Clear action; 404 unless `IWebHostEnvironment.IsDevelopment()`) per `contracts/http-surface.md` §6 (depends on T013)
- [X] T021 [P] Add Development-only JSON endpoint `GET /api/dev/outbox` in `src/Host/Program.cs` (newest-first array per `contracts/http-surface.md` §7; 404 gate; this is the Playwright link-extraction surface) (depends on T013)
- [X] T022 Implement `SecurityStamp` cookie re-validation: sign-in claims include `SecurityStamp`; cookie `OnValidatePrincipal` re-checks stamp against the account with a ≤60 s in-process cache and signs out on mismatch/missing claim (FR-017 enforcement; research R3) in `src/Host/Program.cs` + `src/Host/ManagementAuth/` (depends on T011, T021)
- [X] T023 [P] Refactor `src/Modules/Management/Application/UserService.cs` to delegate to `IUserProvisioning` and `src/Modules/Management/Application/UserInfoLookup.cs` to `IUserLookup` — same public behavior, no Enrollment-internal references remain (depends on T015, T016)
- [X] T024 [P] Refactor `src/Modules/Management/Application/DashboardService.cs` and `src/Modules/Management/Application/OrganizationService.cs` to use `IUserLookup`/`IEnrollmentAdmin`/`ICourseLookup` only (depends on T016, T017, T018)
- [X] T025 [P] Refactor `src/Modules/Management/Application/AdminEnrollmentService.cs` and `src/Modules/Management/Application/CourseVisibilityService.cs` to use `IEnrollmentAdmin`/`ICourseLookup`/`ICourseAdmin` only (depends on T017, T018)
- [X] T026 Reorganize seeders: `ManagementSeeder` seeds organizations only; move the seeded SuperUser `Student` row into `EnrollmentSeeder` (`src/Modules/Enrollment/Infrastructure/EnrollmentSeeder.cs`) and switch ALL seeded users to PBKDF2 hashes + explicit `IsEmailVerified = true`; adjust the seeder call in `src/Host/Program.cs` (ManagementSeeder before EnrollmentSeeder, order preserved) (depends on T011, T012)
- [X] T027 Register new DI: `UserProvisioningService`/`UserLookupService`/`EnrollmentAdminService` + contract mappings in `src/Modules/Enrollment/Endpoints/EnrollmentModuleExtensions.cs`; `DevEmailOutbox` (singleton) + `MockEmailSender` (as `ITransactionalEmailSender`) + `RegistrationService`-supporting services in `src/Host/Program.cs` (depends on T015, T016, T017, T019)
- [X] T028 **GATE**: `dotnet build LibreLms.slnx` succeeds; `dotnet test tests/ArchitectureTests` is **14/14 green**; restart the app and run the FULL existing Playwright suite (`cd tests/Playwright.Tests && npx playwright test`) — all pre-existing specs pass, proving the boundary refactor is behavior-preserving (depends on T005–T027)

**Checkpoint**: Boundary gate green; shared credential core, email seam + dev outbox, and
stamp-based session invalidation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Self-Service Account Creation (Priority: P1) 🎯 MVP

**Goal**: A visitor can create their own learner account with strict, case-insensitively
unique email/password rules; a verification email and a welcome email are generated and
observable in the dev outbox; no auto sign-in.

**Independent Test**: Quickstart scenarios 2 (steps 1–3) + 3 — sign up via the UI with a
fresh email and strong password → confirmation screen (not signed in) → `GET /api/dev/outbox`
shows the two emails → every invalid input from scenario 3 is rejected with its specific
message and creates no account/emails.

### Implementation for User Story 1

- [X] T029 [US1] Implement `EmailThrottle` in `src/Modules/Enrollment/Application/EmailThrottle.cs`: thread-safe in-memory per-normalized-email sliding windows with caps — sign-up 10/24 h, reset requests 5/1 h, verification resends 3/1 h — plus opportunistic purge of expired entries; `Check(email, flow)` returns allowed/throttled (FR-010/FR-013/FR-018; research R6)
- [X] T030 [US1] Implement `RegistrationService` + `RegisterAsync` in `src/Modules/Enrollment/Application/RegistrationService.cs` (create the class; `RegisterAsync` for now): normalize email; validate name/email format/policy via `CredentialPolicy` with specific failure reasons; case-insensitive duplicate check; create account via `UserProvisioningService` (`role=Learner`, default/root org, `isVerified=false`, sets pending verification token + 24 h expiry); send Verification + Welcome emails through `ITransactionalEmailSender` per `contracts/email-messages.md`; apply sign-up throttle; never log the password (FR-001–FR-010; depends on T015, T019, T027, T029)
- [X] T031 [US1] Create sign-up page in `src/Host/Pages/Account/Signup.cshtml` + `Signup.cshtml.cs` per `contracts/http-surface.md` §1: fields name/email/password/confirmPassword, client-side policy hint list, server-side field-level errors, "check your email" confirmation screen, redirect if already signed in, no auto sign-in (FR-009) (depends on T030)
- [X] T032 [P] [US1] Write E2E `tests/Playwright.Tests/tests/signup.spec.ts`: sign up with a fresh unique email + strong password → confirmation screen (not signed in) → `GET /api/dev/outbox` contains newest `Verification` (with working link) + `Welcome` entries for that address (depends on T031, T021)
- [X] T033 [P] [US1] Write E2E `tests/Playwright.Tests/tests/signup-validation.spec.ts` covering every rejection in quickstart scenario 3: case-insensitive duplicate, too short, missing upper/lower/digit, name-in-password, email-in-password, blocklisted password, mismatched confirmation, malformed email, and the 11th-attempt throttle — each with its specific message and no outbox entries (depends on T031, T021)

**Checkpoint**: US1 fully functional and independently testable — self-service registration
with strict validation and observable mock emails (MVP).

---

## Phase 4: User Story 2 - Email Verification (Priority: P1)

**Goal**: Unverified accounts cannot sign in; the verification link (24 h, single-use)
activates the account; used/expired/invalid links are handled with a resend path.

**Independent Test**: Quickstart scenario 2 (steps 4–7) — sign-in blocked with
"please verify your email" + resend before verification; verify via the outbox link →
success; second use of the same link rejected; sign-in then succeeds.

### Implementation for User Story 2

- [X] T034 [US2] Extend `RegistrationService` (same file) with `VerifyEmailAsync(token)` and `ResendVerificationAsync(email)` in `src/Modules/Enrollment/Application/RegistrationService.cs`: hash incoming token → find not-expired match → set `IsEmailVerified=true` + clear token columns (single-use); resend overwrites the pending token (new hash/expiry) and re-sends the Verification email, resend-throttled, neutral no-op for unknown emails (FR-011/FR-012/FR-013) (depends on T030)
- [X] T035 [P] [US2] Create verify page in `src/Host/Pages/Account/Verify.cshtml` + `Verify.cshtml.cs` per `contracts/http-surface.md` §2: valid+unexpired → success screen with sign-in link; used / expired / invalid → distinct friendly errors each offering "request a new verification email" (FR-012; depends on T034)
- [X] T036 [P] [US2] Update sign-in for unverified accounts in `src/Host/Pages/Account/Login.cshtml` + `Login.cshtml.cs`: after a successful password check, if `IsEmailVerified=false` block sign-in with "please verify your email" + a resend-verification action (POST handler using `ResendVerificationAsync`); keep the generic "Invalid email or password." for bad credentials (FR-011/FR-025; depends on T034)
- [X] T037 [US2] Write E2E `tests/Playwright.Tests/tests/verify-email.spec.ts`: unverified sign-in blocked + resend works; verify via outbox link → can sign in; reused link rejected; expired/invalid/tampered link rejected with no account state change (depends on T035, T036)

**Checkpoint**: US1 + US2 both independently functional — a new user can go from first
visit to signed-in using only the UI + dev outbox (SC-001).

---

## Phase 5: User Story 3 - Password Recovery (Forgot Password) (Priority: P1)

**Goal**: A signed-out user can reset a forgotten password via a single-use 30-minute
link; the flow is enumeration-safe and all pre-existing sessions are invalidated on reset.

**Independent Test**: Quickstart scenario 4 — request reset (registered + unregistered
emails get identical on-screen responses, only registered gets an email); reset via outbox
link → old context's session is dead → sign in with the new password; used/expired link
rejected; 6th request within an hour throttled.

### Implementation for User Story 3

- [X] T038 [US3] Extend `RegistrationService` (same file) with `RequestPasswordResetAsync(email)` and `ResetPasswordAsync(token, newPassword)` in `src/Modules/Enrollment/Application/RegistrationService.cs`: reset request → throttle, neutral no-op for unknown emails, otherwise issue 30-min single-use token + send PasswordReset email per `contracts/email-messages.md`; reset → validate token, enforce `CredentialPolicy`, store new PBKDF2 hash, consume token, **rotate `SecurityStamp`** (FR-014–FR-018; depends on T034)
- [X] T039 [P] [US3] Create forgot-password page in `src/Host/Pages/Account/ForgotPassword.cshtml` + `ForgotPassword.cshtml.cs` per `contracts/http-surface.md` §3: email form, identical neutral confirmation for registered/unregistered/throttled (FR-015) (depends on T038)
- [X] T040 [P] [US3] Create reset-password page in `src/Host/Pages/Account/ResetPassword.cshtml` + `ResetPassword.cshtml.cs` per `contracts/http-surface.md` §4: GET validates token (used/expired/invalid → friendly error + "request a new reset"); valid → new-password form with strict policy; success → "password updated — sign in" (FR-016/FR-017; depends on T038)
- [X] T041 [US3] Write E2E `tests/Playwright.Tests/tests/forgot-password.spec.ts`: request → outbox email; unregistered email → identical response + no outbox entry; reset via link → new password works; a second browser context signed in before the reset is signed out afterwards (FR-017, via `SecurityStamp` re-validation); used/expired link rejected; old password now fails; 6th request throttled (depends on T039, T040, T022)

**Checkpoint**: All three P1 stories independently functional.

---

## Phase 6: User Story 4 - Login Page Cleanup (Priority: P2)

**Goal**: The sign-in screen shows no demo/test credentials and offers the formal
entry points (sign in / create account / forgot password).

**Independent Test**: Quickstart scenario 1 + 5 — login page renders with zero demo
credentials; *Create an account* and *Forgot your password?* links navigate correctly;
seeded users (`alice@example.com`, `admin@example.com` / `password123`) still sign in.

### Implementation for User Story 4

- [X] T042 [US4] Remove the "Demo credentials: …" paragraph and add "Create an account" (`/Account/Signup`) + "Forgot your password?" (`/Account/ForgotPassword`) links in `src/Host/Pages/Account/Login.cshtml` (+ `.cs` only if needed) (FR-023/FR-024; depends on T036 — same file)
- [X] T043 [US4] Extend E2E `tests/Playwright.Tests/tests/01-auth.spec.ts`: login page contains no "Demo credentials" text and no seeded emails; both new links are present and navigate; seeded learner + org-admin sign-ins still succeed (quickstart scenario 5) (depends on T042)

**Checkpoint**: All user stories complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Full validation, documentation reconciliation, and the Principle XIII merge gates

- [X] T044 [P] Run the complete validation: `dotnet build LibreLms.slnx` (show output), restart the app (restart-host-app skill; show "Now listening"), `dotnet test tests/ArchitectureTests` 14/14, FULL Playwright suite (`npx playwright test`) including the new signup/signup-validation/verify-email/forgot-password specs — walk all six quickstart scenarios manually and record results (Principle XIII gates 1+2; depends on all previous phases)
- [X] T045 [P] Reconcile documentation with the final implementation: mark all completed checkboxes in `specs/027-formal-signup-registration/tasks.md`, note any drift from `specs/027-formal-signup-registration/plan.md` (structure/decisions), confirm ADRs 0004/0005 match what shipped
- [X] T046 Merge `story/027-formal-signup-registration` into `master` (merge commit), switch back to `master` (Constitution Principle XII), then post-merge regression: rebuild, restart, re-run the full Playwright suite and show passing output (Principle XIII gate 3; depends on T044, T045)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — starts immediately (branch first)
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories** (T028 gate)
- **User Stories (Phases 3–6)**: All depend on Foundational completion
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: After Foundational only — no other story dependencies
- **US2 (P1)**: After Foundational; extends `RegistrationService` and the Login page
  created in US1's phase (same files → runs after US1, but its test criteria are independent)
- **US3 (P1)**: After US2 (same `RegistrationService` file → single writer); relies on the
  T022 stamp mechanism from Foundational
- **US4 (P2)**: After US2 (same Login page file → single writer)

> Note: US2–US4 share two files (`RegistrationService.cs`, `Login.cshtml/.cs`), so within
> the feature they run **sequentially in priority order** — the parallelism lives in the
> [P]-marked tasks (contracts, pages, E2E specs) and across the Foundational waves.

### Within Each User Story

- Service methods before pages (pages call the service)
- Pages before that story's E2E specs (specs exercise the UI)
- Story complete (incl. its E2E) before moving to the next priority

### Parallel Opportunities

- Setup: T002, T003, T004 together
- Foundational wave A (all new files, 8 parallel): T005, T006, T007, T008, T009, T010, T011, T013
- Foundational wave B (parallel where marked): T014, T016, T017, T018, T019, T020, T021 → then T015 (needs T014), T022 (needs T021), then T023/T024/T025 together, then T026, T027, T028
- US1: T032 + T033 together (distinct spec files)
- US2: T035 + T036 together (distinct files)
- US3: T039 + T040 together (distinct files)
- Polish: T044 + T045 together

---

## Parallel Example: User Story 1

```text
# After T029–T031 complete, launch both E2E specs together (distinct files, no shared writes):
Task: "T032 Write E2E tests/Playwright.Tests/tests/signup.spec.ts"
Task: "T033 Write E2E tests/Playwright.Tests/tests/signup-validation.spec.ts"
```

## Parallel Example: Foundational (largest parallelism)

```text
# Wave A — all new contract/domain/host files (8 subagents, disjoint files):
Task: "T005 IUserProvisioning in src/Modules/Enrollment.Contracts/IUserProvisioning.cs"
Task: "T006 IUserLookup in src/Modules/Enrollment.Contracts/IUserLookup.cs"
Task: "T007 IEnrollmentAdmin in src/Modules/Enrollment.Contracts/IEnrollmentAdmin.cs"
Task: "T008 Extend ICourseLookup in src/Modules/Catalog.Contracts/ICourseLookup.cs"
Task: "T009 ICourseAdmin in src/Modules/Catalog.Contracts/ICourseAdmin.cs"
Task: "T010 ITransactionalEmailSender in src/SharedKernel/ITransactionalEmailSender.cs"
Task: "T011 Student fields + migration (Student.cs, EnrollmentDbContext.cs, Migrations/Enrollment/)"
Task: "T013 DevEmailOutbox in src/Host/Mail/DevEmailOutbox.cs"

# Wave B — implementations (disjoint files):
Task: "T014 CredentialPolicy"   Task: "T016 UserLookupService"   Task: "T017 EnrollmentAdminService"
Task: "T018 Catalog impl + DI"  Task: "T019 MockEmailSender"     Task: "T020 Dev outbox page"
Task: "T021 /api/dev/outbox"    → then T015 (needs T014), T022 (needs T021)

# Wave C — Management refactor (3 subagents, disjoint service pairs):
Task: "T023 UserService + UserInfoLookup"  Task: "T024 DashboardService + OrganizationService"
Task: "T025 AdminEnrollmentService + CourseVisibilityService"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories; ends with the 14/14 gate)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: quickstart scenarios 2 (steps 1–3) + 3 + `signup*.spec.ts` green
5. Demo: self-service sign-up with strict validation + mock emails in the dev outbox

### Incremental Delivery

1. Setup + Foundational → foundation ready (boundary gate green)
2. US1 → validate → **MVP** (registration works)
3. US2 → validate (SC-001: first visit → signed in < 3 min via dev outbox)
4. US3 → validate (SC-004: reset invalidates sessions; enumeration-safe)
5. US4 → validate (SC-005: no demo credentials; SC-008: zero real outbound email)
6. Polish → merge gates (Principle XIII) → master

### Parallel Team Strategy (subagents — Constitution Principle XI)

1. One subagent per [P]-marked task group with **disjoint files** (see Parallel Examples);
   single writer per file — `RegistrationService.cs` and `Login.cshtml/.cs` are
   intentionally serialized across US2 → US3 → US4
2. Parent session orchestrates, synthesizes, and applies integration fixes (never delegate
   the final merge — T046 runs in the parent)
3. After each wave: `dotnet build` + the T028 gate before proceeding to the next wave

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] labels map tasks to spec.md user stories for traceability (US1–US4)
- Every E2E spec extracts verification/reset links from `GET /api/dev/outbox`
  (Development-only) — see `contracts/http-surface.md` §7
- Commit after each task or logical group on `story/027-formal-signup-registration`
- Stop at any checkpoint and validate the story independently before proceeding
- The boundary-refactor tasks (T005–T027) are behavior-preserving: the T028 full-suite
  regression is the proof; if behavior drifts, fix drift before any US work
- Avoid: touching Valkey (SCORM-only, Constitution VI), new NuGet packages, real outbound
  email, and any edit on `master` outside the final T046 merge
