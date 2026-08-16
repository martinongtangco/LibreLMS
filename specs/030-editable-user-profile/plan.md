# Implementation Plan: Editable User Profile With Photo & Course History

**Branch**: `story/030-editable-user-profile` | **Date**: 2026-08-16 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/030-editable-user-profile/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command; its definition describes the execution workflow.

## Summary

Make the existing `/Account/Profile` page a self-service profile: editable display name
gated on the account's email-verified state (with resend-verification affordance), a
"My Courses" area grouping all enrollments into Enrolled vs Completed (completed = any
attempt with status `completed`/`passed`), and a display-photo upload shown on the
profile and next to the name in the upper-right nav — for all users, but hidden for
admin-role users while the nav is in the Admin view (resolved Q1 = option C).

Technical approach: everything lives in **Host** (composition root) plus one nullable
`Student.AvatarPath` column (one Enrollment migration). Name updates reuse the existing
`IUserProvisioning.UpdateAsync` contract; the verification gate reuses
`RegistrationService.IsEmailVerifiedAsync`/`ResendVerificationAsync`; the course section
reuses the exact MyCourses join (`EnrollmentService.GetMyEnrollmentsAsync` +
`ScormAttemptService.GetMyAttemptsAsync`). Photos are static files under
`wwwroot/avatars/` (GUID-keyed filenames, no user-controllable paths). The nav shows the
photo/name from the **auth cookie**: after any successful change the cookie is re-issued
from the fresh `Student` row (RefreshSignIn pattern), adding one new `AvatarPath` claim —
so the layout never touches the database. Nav visibility for admins reuses the existing
`role-admin`/`role-learner` body-class mechanism with a single CSS rule. One
Development-gated dev endpoint (`/Dev/Unverify`) makes the verification-gate negative
branch E2E-observable (SC-002). One new ADR (0007) documents avatar storage + claim.
**No new NuGet packages, no new projects, no new cross-module contracts.**

## Technical Context

**Language/Version**: C# on .NET 10 (LTS, pinned via `global.json`)

**Primary Dependencies**: ASP.NET Core minimal APIs + Razor Pages (existing), EF Core
SqlServer (existing), NetArchTest (existing, gate), Playwright TS (existing, E2E).
**No new NuGet packages** — file upload via built-in `IFormFile`, image validation via
extension/MIME whitelist + size cap.

**Storage**: MSSQL via existing `EnrollmentDbContext` — one new nullable column
`Student.AvatarPath` (one migration, dev drop-recreate still applies). Avatar image
files on disk under `wwwroot/avatars/` (durable, mirrors the SCORM-content pattern),
served by the existing static-files middleware. No Valkey involvement.

**Testing**: xUnit (existing unit/architecture tests, gate must stay green) + Playwright
TS E2E (new specs: profile-name, profile-verification-gate, profile-courses,
profile-photo; existing specs unchanged).

**Target Platform**: Linux (devcontainer), single ASP.NET Core process (`src/Host`)

**Project Type**: web-service (modular monolith) + Razor web portal

**Performance Goals**: profile page (incl. course section) renders < 2 s on dev
hardware (SC-003); photo upload round-trip < 30 s user time (SC-004) with ≤ 5 MB cap;
dev scale only (dozens of users, no load targets)

**Constraints**: module-boundary gate (`ModuleBoundaryTests`) must end green; Valkey
reserved for SCORM runtime state; no real outbound email (mock outbox); no new packages
without a specific problem; all durable state in MSSQL; avatar filenames never
user-controllable (GUID-keyed)

**Scale/Scope**: 1 page rewritten (`Account/Profile`), nav + CSS edits in `_Layout` /
`site.css`, 1 EF migration, 2 small new Host files (avatar claim types, cookie
re-issue helper), 1 dev-only page, 1 ADR, 4 new E2E specs, ~150–250 lines of CSS

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Verdict | Notes |
|---|-----------|---------|-------|
| I | Modular monolith | ✅ PASS | Extends Host + Enrollment (one column); no new deployables, no new modules, no new projects |
| II | Clean arch, one sentence | ✅ PASS | Every new abstraction is one-sentence explainable: `AvatarClaimTypes` ("cookie claim carrying the avatar URL so the layout renders it without a DB hit"), `AuthCookieRefresher` ("re-issue the auth cookie from a fresh Student row after a profile change"), `/Dev/Unverify` ("dev-only toggle so the verification-gate E2E can reach the negative branch"). No repository/mediator layers added |
| III | Compiled boundaries | ✅ PASS | Host (composition root) consumes only pre-existing contracts/services (`IUserProvisioning`, `RegistrationService`, `EnrollmentService`, `ScormAttemptService`); no new cross-module references; gate re-run in quickstart |
| IV | Human-legible + ADRs | ✅ PASS | One ADR planned: `docs/adr/0007-user-avatar-storage.md` (disk storage under wwwroot + GUID-keyed filenames + avatar claim + cookie re-issue) |
| V | Sandbox | ✅ PASS | No change; no new outbound network (avatar files are local; no email beyond existing mock seam) |
| VI | Polyglot storage w/ reason | ✅ PASS | Durable relational state (avatar URL column) in MSSQL. Image bytes on disk next to existing SCORM content — "would losing this be fine?" → no, and it's a file, not relational/ephemeral; Valkey untouched (stays SCORM-runtime-only) |
| VII | Spec-driven sliced thin | ✅ PASS | Spec 030 exists; vertical user-visible slice (editable profile capability) |
| VIII | Branching discipline | ✅ PASS | Implementation on `story/030-editable-user-profile` from master |
| IX | Plan on master only | ✅ PASS | Plan authored on `master` (verified via `git branch --show-current`) |
| X | No ad-hoc fixes | ✅ PASS | This spec/plan is the decision record |
| XI | Parallel subagents | ✅ PASS (forward) | Name flow, photo flow, course section, and E2E specs have disjoint file sets — will be marked `[P]` in tasks.md |
| XII | Return to master | ✅ PASS (forward) | Enforced at implement time |
| XIII | Verification before claim | ✅ PASS (forward) | [quickstart.md](quickstart.md) defines the evidence: build output, architecture-test run, new + full Playwright runs, post-merge re-run |

**Pre-Phase 0 gate**: PASS — no violations to justify.

### Re-check after Phase 1 design

- Data model (data-model.md): one nullable `Student.AvatarPath` column in MSSQL; image
  bytes on disk (wwwroot pattern, precedent: SCORM content) — VI ✅; no Valkey — VI ✅.
- Contracts (contracts/module-contracts.md): **zero new** cross-module surfaces; the
  Host reuses four pre-existing ones — III ✅ (gate re-run is part of quickstart).
- Web surface (contracts/http-surface.md): all new pages/endpoints live in Host;
  `/Dev/Unverify` is Development-gated like `/Dev/Outbox` — V/VI ✅.
- No new packages, no new projects — II/I ✅.
- **Result: PASS.** No design decision introduced a violation; Complexity Tracking
  below is empty.

## Project Structure

### Documentation (this feature)

```text
specs/030-editable-user-profile/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── http-surface.md
│   └── module-contracts.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Host/
│   ├── ManagementAuth/
│   │   ├── AvatarClaimTypes.cs            # NEW — "AvatarPath" claim type constant
│   │   └── AuthCookieRefresher.cs         # NEW — rebuild claims from Student row + re-issue cookie
│   ├── Pages/
│   │   ├── Account/
│   │   │   ├── Profile.cshtml             # EDIT — name form, photo form, courses area, resend link
│   │   │   └── Profile.cshtml.cs          # EDIT — OnGet load, OnPostName, OnPostPhoto, OnPostResend
│   │   ├── Dev/
│   │   │   ├── Unverify.cshtml            # NEW — Development-gated verification toggle (E2E enabler)
│   │   │   └── Unverify.cshtml.cs         # NEW
│   │   └── Shared/
│   │       └── _Layout.cshtml             # EDIT — avatar element inside .account-control (img or initials placeholder)
│   ├── wwwroot/
│   │   ├── avatars/                       # NEW — static avatar files (GUID-keyed), gitignored
│   │   └── css/site.css                   # EDIT — avatar styles + ".role-admin .account-avatar {display:none}"
│   └── Migrations/Enrollment/             # NEW migration — AddAvatarPathToStudent
├── Modules/Enrollment/
│   ├── Domain/Student.cs                  # EDIT — +string? AvatarPath
│   └── Infrastructure/EnrollmentDbContext.cs  # EDIT — column mapping (max ~200 chars, nullable)

tests/
└── Playwright.Tests/tests/
    ├── 12-profile-name.spec.ts            # NEW — US1 positive flow + nav reflection + validation
    ├── 13-profile-verification-gate.spec.ts # NEW — US1 negative flow via /Dev/Unverify + resend
    ├── 14-profile-courses.spec.ts         # NEW — US2 grouping, empty state, retake rule
    └── 15-profile-photo.spec.ts           # NEW — US3 upload/replace/reject + nav visibility (Q1=C) + placeholder

docs/
└── adr/0007-user-avatar-storage.md        # NEW — avatar storage + claim + cookie re-issue
```

**Structure Decision**: Single modular-monolith project (existing). The feature is a
vertical slice inside Host + one schema column in Enrollment; no new directories beyond
`wwwroot/avatars/` and the four new E2E spec files. The Enrollment module's Domain and
Infrastructure change is limited to the one property + its mapping (migration generated
by the standard workflow).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

None — no violations to justify.
