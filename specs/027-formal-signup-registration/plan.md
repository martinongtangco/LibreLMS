# Implementation Plan: Formal Signup & Registration

**Branch**: `story/027-formal-signup-registration` | **Date**: 2026-08-15 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/027-formal-signup-registration/spec.md`

## Summary

Formalize self-service registration: a public sign-up flow with strict, case-insensitively
unique email/password rules; email verification (login blocked until verified, 24 h
single-use links); a forgot-password flow (30 min single-use links, enumeration-safe,
invalidates all sessions on reset); a swappable transactional-email seam with a
developer-observable mock (zero real outbound, SendGrid-ready); and removal of the
demo-credentials hint from the sign-in screen.

Technical approach: account lifecycle lives in the **Enrollment** module
(`RegistrationService` + shared credential core: PBKDF2 hashing with a legacy-SHA256
upgrade path, strict password policy with a top-1000 blocklist, in-memory per-email
throttle); the email seam (`ITransactionalEmailSender`) lives in **SharedKernel** with a
`MockEmailSender` + dev outbox in **Host**; `Student` gains verification/stamp/token
columns (one migration); cookie auth gains a `SecurityStamp` re-validation for session
invalidation. **Prerequisite workstream**: the module-boundary gate is RED on master
(10 legacy violating types in Management) and must be made green via behavior-preserving
contract refactoring before this slice can be "done" (research R9).

## Technical Context

**Language/Version**: C# on .NET 10 (LTS, pinned via `global.json`)

**Primary Dependencies**: ASP.NET Core minimal APIs + Razor Pages (existing), EF Core
SqlServer (existing), NetArchTest (existing, gate), Playwright (existing, E2E). **No new
NuGet packages** — PBKDF2 via built-in `System.Security.Cryptography.KeyDerivation`.

**Storage**: MSSQL via existing 4 DbContexts; `Student` schema change in
`EnrollmentDbContext` (one new migration). In-memory only: dev email outbox (Host) and
per-email throttle windows (Enrollment) — both deliberately non-durable (Constitution VI).
No Valkey involvement.

**Testing**: xUnit (existing unit/architecture tests) + Playwright TS E2E (new specs:
signup, signup-validation, forgot-password; existing login spec unchanged).

**Target Platform**: Linux (devcontainer), single ASP.NET Core process (`src/Host`)

**Project Type**: web-service (modular monolith) + Razor web portal

**Performance Goals**: sign-in incl. PBKDF2 verify (210k iters) < 500 ms on dev
hardware; page loads < 1 s; dev scale only (dozens of users, no load targets)

**Constraints**: module-boundary gate (`ModuleBoundaryTests`) must end green; Valkey
reserved for SCORM runtime state (no new uses); no real outbound email; no new packages
without a specific problem; all durable state in MSSQL

**Scale/Scope**: ~4 new account pages + sign-in screen changes, 1 EF migration, 3 new
Enrollment contracts + 2 Catalog contract additions, 6 Management services refactored to
contracts, 2 ADRs, ~3 new E2E specs

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Verdict | Notes |
|---|-----------|---------|-------|
| I | Modular monolith | ✅ PASS | New code extends existing modules (Enrollment, Catalog contracts) + SharedKernel + Host; no new deployables, no new modules |
| II | Clean arch, one sentence | ✅ PASS | Every new abstraction is one-sentence explainable: `RegistrationService` ("self-service account lifecycle for Student"), `ITransactionalEmailSender` ("send transactional email without knowing the provider"), `PasswordHasher` ("PBKDF2 with legacy upgrade"), `EmailThrottle` ("per-email attempt caps"). No repository/mediator layers added; `EnrollmentDbContext` stays the unit of work |
| III | Compiled boundaries | ⚠️ BASELINE RED → remediated in-scope | New code is boundary-clean by design (R1). Pre-existing: 10 violating Management types (verified by running `ModuleBoundaryTests`: 12/14 pass). Development Workflow requires the gate green before a slice is done, so a behavior-preserving contract-refactoring workstream is included (R9) and ordered before feature tasks |
| IV | Human-legible + ADRs | ✅ PASS | Two ADRs planned: `docs/adr/0004-transactional-email-seam.md`, `docs/adr/0005-credential-security-baseline.md` (PBKDF2 + security-stamp session invalidation) |
| V | Sandbox | ✅ PASS | No change; no new outbound network (the mock explicitly sends nothing) |
| VI | Polyglot storage w/ reason | ✅ PASS | All durable state (verification flag, stamp, tokens) in MSSQL. Outbox + throttle are in-memory dev artifacts — "would losing this on restart be fine?" → yes, so not SQL, and certainly not Valkey (which stays SCORM-only) |
| VII | Spec-driven sliced thin | ✅ PASS | Spec 027 exists; vertical slice (user-visible registration lifecycle) |
| VIII | Branching discipline | ✅ PASS | Implementation on `story/027-formal-signup-registration` from master |
| IX | Plan on master only | ✅ PASS | Plan authored on `master` (verified) |
| X | No ad-hoc fixes | ✅ PASS | This spec/plan is the decision record, including the gate remediation (R9) |
| XI | Parallel subagents | ✅ PASS (forward) | Remediation workstream and feature workstreams have disjoint file sets — will be marked `[P]` in tasks.md |
| XII | Return to master | ✅ PASS (forward) | Enforced at implement time |
| XIII | Verification before claim | ✅ PASS (forward) | [quickstart.md](quickstart.md) defines the evidence: build output, 14/14 architecture tests, new + full Playwright runs, post-merge re-run |

**Pre-Phase 0 gate**: PASS — no blocking violations from this slice; the baseline-RED
item (III) is carried as an in-scope prerequisite workstream, not waived.

### Re-check after Phase 1 design

- Data model (data-model.md): no new tables; token/stamp/verification state on `Student`
  (MSSQL) — VI ✅; no Valkey — VI ✅.
- Contracts (contracts/module-contracts.md): every new cross-module surface is an
  interface + DTO in `*.Contracts`; Management's six violating services get contract-only
  dependencies — III ✅ (at completion).
- Web surface (contracts/http-surface.md): all new pages/endpoint additions live in Host;
  dev outbox is Development-gated — V/VI ✅.
- No new packages, no new projects (files only) — II/I ✅.
- **Result: PASS.** No design decision introduced a violation; Complexity Tracking
  below is empty.

## Project Structure

### Documentation (this feature)

```text
specs/027-formal-signup-registration/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (decisions R1–R10)
├── data-model.md        # Phase 1 output (Student extension + in-memory structures)
├── quickstart.md        # Phase 1 output (validation runbook)
├── contracts/           # Phase 1 output
│   ├── module-contracts.md   # C# boundary contracts (SharedKernel/Enrollment/Catalog)
│   ├── http-surface.md       # pages + dev-outbox API behavior contract
│   └── email-messages.md     # the 3 transactional messages
└── tasks.md             # Phase 2 output (/speckit.tasks - NOT created yet)
```

### Source Code (repository root)

```text
src/SharedKernel/
├── ITransactionalEmailSender.cs        # NEW seam + EmailPurpose + OutboundEmail record

src/Modules/Enrollment/
├── Application/
│   ├── RegistrationService.cs          # NEW self-service lifecycle (register/verify/resend/reset/login-check/stamp)
│   ├── CredentialPolicy.cs             # NEW strict password rules + top-1000 blocklist
│   ├── PasswordHasher.cs               # NEW PBKDF2 hash/verify + legacy SHA256 upgrade
│   ├── EmailThrottle.cs                # NEW in-memory per-email sliding-window throttle
│   ├── UserProvisioningService.cs      # NEW IUserProvisioning impl (shared creation path, boundary fix)
│   ├── UserLookupService.cs            # NEW IUserLookup impl (boundary fix)
│   └── EnrollmentAdminService.cs       # NEW IEnrollmentAdmin impl (boundary fix)
├── Domain/
│   └── Student.cs                      # CHANGED +IsEmailVerified, +SecurityStamp, +4 token fields
├── Infrastructure/
│   ├── EnrollmentDbContext.cs          # CHANGED column config/defaults
│   └── EnrollmentSeeder.cs             # CHANGED PBKDF2 hashes, verified=true, +SuperUser row (moved from ManagementSeeder)
├── Resources/
│   └── common-passwords.txt            # NEW embedded top-1000 blocklist (~10 KB)
└── Endpoints/
    └── EnrollmentModuleExtensions.cs   # CHANGED register new services + contract impls

src/Modules/Enrollment.Contracts/
├── IUserProvisioning.cs                # NEW + StudentProvisionedDto
├── IUserLookup.cs                      # NEW + UserScopeInfo/OrgLearnerCount DTOs
└── IEnrollmentAdmin.cs                 # NEW + Admin*/RecentEnrollment DTOs

src/Modules/Catalog.Contracts/
├── ICourseLookup.cs                    # CHANGED +CountAsync/+CountByOrgAsync/+GetCoursesAsync(batch)
└── ICourseAdmin.cs                     # NEW DeleteAsync

src/Modules/Catalog/
├── Application/                        # CHANGED implement extended ICourseLookup + ICourseAdmin
└── Endpoints/CatalogModuleExtensions.cs # CHANGED registration

src/Modules/Management/
├── Application/
│   ├── UserService.cs                  # CHANGED delegate to IUserProvisioning (boundary)
│   ├── UserInfoLookup.cs               # CHANGED delegate to IUserLookup (boundary)
│   ├── DashboardService.cs             # CHANGED contracts only (boundary)
│   ├── OrganizationService.cs          # CHANGED contracts only (boundary)
│   ├── AdminEnrollmentService.cs       # CHANGED contracts only (boundary)
│   └── CourseVisibilityService.cs      # CHANGED contracts only (boundary)
└── Infrastructure/
    └── ManagementSeeder.cs             # CHANGED orgs-only (SuperUser Student moved out)

src/Host/
├── Program.cs                          # CHANGED cookie OnValidatePrincipal stamp check, DI (sender/outbox/throttle), seeder order
├── ManagementAuth/                     # CHANGED stamp claim + validate-principal handler
├── Mail/
│   ├── MockEmailSender.cs              # NEW ITransactionalEmailSender impl (outbox + log, no real send)
│   └── DevEmailOutbox.cs               # NEW bounded in-memory ring
├── Pages/Account/
│   ├── Login.cshtml / Login.cshtml.cs  # CHANGED hint removed, signup/forgot links, unverified message + resend
│   ├── Signup.cshtml / .cs             # NEW
│   ├── Verify.cshtml / .cs             # NEW
│   ├── ForgotPassword.cshtml / .cs     # NEW
│   └── ResetPassword.cshtml / .cs      # NEW
├── Pages/Dev/
│   └── Outbox.cshtml / .cs             # NEW Development-only outbox viewer
└── (GET /api/dev/outbox)               # NEW Development-only JSON endpoint (in Program.cs minimal API)

src/Host/Migrations/Enrollment/
└── <timestamp>_AddRegistrationFieldsToStudent.cs  # NEW migration

docs/adr/
├── 0004-transactional-email-seam.md    # NEW
└── 0005-credential-security-baseline.md # NEW

tests/
├── ArchitectureTests/                  # unchanged; gate 14/14
└── Playwright.Tests/
    └── tests/
        ├── signup.spec.ts              # NEW (US1+US2: sign-up → outbox → verify → sign-in)
        ├── signup-validation.spec.ts   # NEW (US1 rejections incl. case-insensitive duplicate + throttle)
        └── forgot-password.spec.ts     # NEW (US3: reset, enumeration-safe, session invalidation)
```

**Structure Decision**: No new projects or modules — the feature is realized as new
files inside the existing modular-monolith layout (Enrollment owns the account
lifecycle; SharedKernel hosts the one cross-cutting seam; Host hosts the web surface,
the mock, and migrations). This is the minimal structure the slice actually needs
(Constitution I/II); a dedicated Identity module would be premature scaffolding (R1).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations. The Constitution Check's only ⚠️ is the pre-existing baseline-RED
boundary gate, which this slice *fixes* (workstream R9) rather than justifies; no
principle is bent or waived.

## Drift Log (implementation vs. this plan)

Recorded at T045 (documentation reconciliation). "Plan" = the decisions/structure
above; "Shipped" = what actually landed on `story/027-formal-signup-registration`.

1. **ADR numbers 0004 + 0006, not 0004 + 0005.**
   Plan §Constitution Check named ADRs "0004 (email seam) / 0005 (credential
   baseline)". `docs/adr/0005-no-htmx-for-navigation.md` already existed on
   `master`, so per Constitution IV (sequential numbering) the credential-baseline
   ADR shipped as **0006-credential-security-baseline.md**. ADR-0004
   (transactional-email-seam) is unchanged.

2. **PBKDF2 via `Rfc2898DeriveBytes`, not `KeyDerivation.Pbkdf2`.**
   This environment's .NET 10 reference pack lacks
   `System.Security.Cryptography.KeyDerivation` (reproduced in a minimal
   net10.0 app). `PasswordHasher` uses `Rfc2898DeriveBytes`
   (PBKDF2-HMAC-SHA256, 210k iterations, 16-byte salt, 32-byte hash) with
   `#pragma warning disable SYSLIB0060`. Output format
   `PBKDF2$210000$<saltB64>$<hashB64>` is unchanged. Recorded in ADR-0006.

3. **No SecurityStamp re-validation cache (plan said "≤60 s in-process cache
   optional").**
   Shipped: one indexed primary-key lookup per request in
   `OnValidatePrincipal`. A TTL cache was rejected because it would delay
   FR-017 reset-invalidation by up to the TTL and make E2E nondeterministic.
   Documented in ADR-0006.

4. **Token-consumption state machine without a schema change (plan: "consume =
   null the token columns").**
   Shipped: consuming a link sets `*TokenExpiresAt = null` and **keeps** the hash,
   so `AlreadyUsed` stays detectable (hash alone is unguessable). The hash is the
   stable "was issued" marker; the nullable expiry is the "pending" marker.
   Applies to both verification (24 h) and reset (30 min) tokens.

5. **`Login` page gained an access-denied state (not in plan).**
   A signed-in user bounced to `/Account/Login` by an `[Authorize]`
   access-denied challenge now sees "Access denied — signed in as {name}" instead
   of the form or a redirect. A redirect-to-home would loop (denied → login →
   home) and break `08-rbac.spec.ts` expectations (URL must contain
   `/Account/Login`). Behavior-preserving for the pre-existing RBAC suite.

6. **Reset page token is an explicit `OnGet(string? token)` handler parameter.**
   Empirically, `[BindProperty]` on a parameterless `OnGet` did not bind the
   query string in this app (token arrived null, no DB query issued) while the
   same pattern on POST forms works. The Verify page (explicit parameter) was the
   working reference. Documented in the page-model XML doc.

7. **Contract surface grew beyond the plan's minimal list** (all additions are
   backward-compatible, existing members untouched):
   - `IUserProvisioning.ListAsync(roleFilter)`; `IUserLookup.GetUserNameAsync`,
     `GetUsersAsync`, `CountByRoleAsync`; `IEnrollmentAdmin.ListAsync(studentName,
     courseTitle)`, `AdminEnrollResult.EnrolledAt`; `ICourseLookup.CountAsync`,
     `CountByOrgAsync`, `GetCoursesAsync`, `ListByOrgsAsync`, `ListAllAsync`;
     `CourseSummary` gained `Category` + `OrganizationId`; new `ICourseAdmin`.
     These support the Management→contracts refactor (R9) and the admin pages.

8. **`/api/dev/outbox` + `/Dev/Outbox` (plan: "dev outbox view").**
   Both shipped: a JSON endpoint (used by the E2E specs to extract links) and a
   Razor page for humans. Both Development-gated.

9. **Two `02-course-browse` failures are pre-existing on `master`, not
   regressions.** "selecting Programming category shows 4 courses" and "selecting
   Tools category shows 2 courses" fail on this branch *and* on `master`
   (verified via a `git worktree` run of `master` on :5010 against the same
   accumulated dev DB — same 2 failures). They assert hardcoded category counts
   that the dev DB's accumulated manual-test courses no longer satisfy. Left
   untouched (out of scope for spec 027); full-suite delta vs. master is zero.

10. **E2E "expired link" tests are skipped with reasons.** The 24 h / 30 min
    expiry states require DB time manipulation; the Playwright project has no SQL
    driver and no `sqlcmd` in the environment. `verify-email.spec.ts` and
    `forgot-password.spec.ts` each carry a `test.skip` with the reason; the
    Expired branch shares its lookup path with the covered already-used/invalid
    cases.

11. **Execution strategy:** the parent agent wrote the production code in planned
    waves (subagent file-writing children were abandoned — fork-context `worker`
    exceeded context size and fresh-context `delegate` children on the local 27B
    model exceeded timeouts). The two US1 E2E specs (T032/T033) were delegated to
    `delegate` children with a 90 min timeout and completed green. T037/T041 E2E
    specs were written by the parent.

12. **Seeder reorg (plan: "EnrollmentSeeder seeds users").** `ManagementSeeder`
    is orgs-only; `EnrollmentSeeder` seeds the five users + SuperUser
    (`admin@librelms.local` / `Admin@12345`), PBKDF2 hashes, `IsEmailVerified=true`,
    and **enforces canonical roles** on all seed emails every start (fixes stale
    role state from prior runs). Root org id
    `00000000-0000-0000-0000-000000000001` is the default org for self-service
    sign-ups (`RegistrationService.DefaultOrganizationId`).
