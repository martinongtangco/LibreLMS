# Implementation Plan: Fix Enrollment While Logged Out (Anonymous Enroll + Broken Return Flow)

**Branch**: `bug/055-fix-logged-out-enroll` | **Date**: 2026-09-20 | **Spec**: [spec.md](./spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. This is a defect fix.

**Input**: Feature specification from `/specs/055-fix-logged-out-enroll/spec.md`

## Summary

A signed-out visitor can currently enroll in courses: the enroll action's per-handler
`[Authorize]` is silently ignored by .NET 10 (verified empirically — a minimal repro confirms
handler-level authorization attributes on Razor Pages handlers do not challenge; class-level
does), and the identity helper falls back to a hardcoded demo learner
(`550e8400-…-0001`, alice@example.com) when no signed-in identity exists, so every anonymous
enrollment is silently attributed to the demo account. Additionally, no return-to-origin flow
exists: login always lands on the home page, and sign-up/verification never carry the original
URL.

**Technical approach** (all changes at the Host composition root — no module changes):

1. **Enforce enrollment auth explicitly**: replace the inert handler-level `[Authorize]` on
   `OnPostEnrollAsync` with an explicit guard that challenges the cookie scheme
   (`ChallengeResult("Cookie")`) when no authenticated identity is present (FR-001/002/010).
   The course page renders a plain (non-HTMX) form for guests so the 302 challenge is a full
   page navigation to the sign-in page, not an in-place swap (FR-002).
2. **Remove the silent demo-identity fallback**: `ScormHelpers.GetStudentId` (and the private
   duplicate in `Settings.cshtml.cs`) returns `Guid.Empty` when no identity claim exists instead
   of the hardcoded demo GUID (FR-003). Existing read paths already treat `Guid.Empty` as
   "not enrolled" — guests then see the not-enrolled state everywhere (FR-008).
3. **My Courses requires sign-in**: class-level `[Authorize]` on `MyCoursesModel` (FR-008).
4. **Pending return address**: a 24 h, HttpOnly, SameSite=Lax cookie (`lms.ReturnUrl`) set on
   the sign-in page from the validated local `ReturnUrl` query value, carried through
   sign-up and email verification (cookie persists), and consumed once on successful sign-in to
   redirect the user back to the course page (FR-004/005/006/007). Enrollment is never
   automatic — the user presses "Enroll now" a second time, manually.
5. **ADR 0013** records the two structural findings (handler-level `[Authorize]` is inert in
   .NET 10; silent identity fallbacks are an anti-pattern) and the house pattern that replaces
   them (Principle X step 4 — before code).

## Technical Context

**Language/Version**: C# / .NET 10 (SDK pinned `10.0.103`, `rollForward: latestPatch`,
`allowPrerelease: false` via `global.json`; runtime `Microsoft.AspNetCore.App 10.0.3`)

**Primary Dependencies**: ASP.NET Core (minimal APIs + Razor Pages), HTMX (client), EF Core,
cookie authentication (scheme `"Cookie"`). No new dependencies.

**Storage**: MSSQL (system of record — unchanged by this fix; no schema changes). Pending
return address is client-side cookie state (ephemeral). Valkey is NOT involved (it remains
SCORM-runtime-only per Principle VI).

**Testing**: xUnit unit tests (`tests/Host.Tests` — new `ReturnUrl` helper tests), Playwright
E2E (`tests/Playwright.Tests` — new `21-logged-out-enroll.spec.ts` + regression of
`01-auth`, `03-enrollment`, `signup`, `verify-email`), `tests/ArchitectureTests` (boundary
regression guard).

**Target Platform**: Linux server (devcontainer, docker-compose: `mssql` + `valkey`); dev
host Windows with the same containerized services. Web portal, desktop-class browsers.

**Project Type**: Web application (modular monolith; this feature touches the `Host`
composition root only — `src/Host/Pages/**`, `src/Host/ScormHelpers.cs`, plus one new Host
helper file and E2E/unit tests).

**Performance Goals**: No new query patterns; the guest read paths skip the per-page
enrollment lookup entirely when `studentId == Guid.Empty` (small win). No measurable
regression target beyond existing page loads.

**Constraints**: No new abstractions beyond one small static helper (Principle II); no module
boundary crossings (Principle III); no schema migration; no new packages; dev app runs on
plain `http://localhost:5000` (cookie `Secure` flag must not break dev).

**Scale/Scope**: Dev/teaching scale. Surfaces touched: course detail page (view + handler),
catalog page (code-behind only — inherits the fallback fix), My Courses page (attribute),
sign-in page (OnGet/OnPost), Settings page (dead fallback removed). Sign-up and Verify pages:
no code changes (the cookie persists across them).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Modular monolith | PASS | No new modules; Host composition root only. |
| II | Clean Architecture, applied simply | PASS | One small static helper (`ReturnUrlCookie`), explainable in one sentence; no new layers/abstractions. |
| III | Module boundaries compiled | PASS | No changes to any module project; `ArchitectureTests` re-run as regression guard. |
| IV | Human-legible AI-authored code | PASS | Explicit guard + comment at the enroll handler; **ADR 0013** records both structural findings before code. |
| V | The sandbox | PASS | Work happens in the devcontainer against bind mount + sibling containers only. |
| VI | Polyglot storage with a reason | PASS | MSSQL unchanged; pending return address is ephemeral client-side cookie state — losing it degrades to landing on the home page, so it does not belong in SQL or Valkey. |
| VII | Spec-driven, sliced thin | PASS | Vertical slice: the signed-out enroll journey end-to-end; spec 055 exists before any code. |
| VIII | Branching discipline | PASS | `bug/055-fix-logged-out-enroll` created from `master` before code changes. |
| IX | Plan on master only | PASS | Planning executed on `master` (verified). |
| X | No ad-hoc fixes | PASS | Root cause stated (spec §Root Cause); ADR 0013 planned for the structural findings; spec + plan before code. |
| XI | Parallel implementation with subagents | N/A at plan time | Applied at `/speckit.implement` via `[P]` markers in tasks.md. |
| XII | Return to master after implementation | N/A at plan time | Enforced at merge. |
| XIII | Verification before claim | PLANNED | Build + Playwright (new spec + regression suite) + post-merge re-run are part of the task gates. |
| XIV | Bounded retry | N/A at plan time | Process rule for the implement phase. |
| XV | Triage before retry | N/A at plan time | Process rule for the implement phase. |
| XVI | Independent verification for merge | PLANNED | Fresh subagent (or human) re-runs gates before merge — recorded in tasks. |

**Gate result: PASS** — no violations, no Complexity Tracking entries required.
*Re-checked after Phase 1 design: still PASS (cookie-based return address confirmed as the
simplest compliant mechanism; no module changes; ADR 0013 scoped).*

## Project Structure

### Documentation (this feature)

```text
specs/055-fix-logged-out-enroll/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── enroll-action.md
│   └── return-url.md
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Host/
├── Pages/
│   ├── Courses/Detail.cshtml          # guest form: plain (non-HTMX) vs authenticated: HTMX
│   ├── Courses/Detail.cshtml.cs       # explicit challenge guard replaces inert [Authorize]
│   ├── Courses/Index.cshtml.cs        # inherits fallback removal (no edit expected — verify)
│   ├── MyCourses/Index.cshtml.cs      # class-level [Authorize]
│   ├── Account/Login.cshtml.cs        # set pending cookie (OnGet) / consume (OnPost)
│   └── Account/Settings.cshtml.cs     # remove dead demo-fallback duplicate
├── ReturnUrlCookie.cs                 # NEW: one small static helper (set/consume/validate)
└── ScormHelpers.cs                    # GetStudentId: Guid.Empty instead of demo GUID

docs/adr/
└── 0013-handler-level-page-authz-and-identity-fallback.md   # NEW (Principle X step 4)

tests/
├── Host.Tests/
│   └── ReturnUrlCookieTests.cs        # NEW unit tests (validation, lifetime, consume-once)
└── Playwright.Tests/tests/
    └── 21-logged-out-enroll.spec.ts   # NEW E2E (US1–US3 journeys)
```

**Structure Decision**: Single modular monolith (existing layout). The fix is confined to the
Host composition root — the module that owns the auth cookie, the pages, and the identity
helper. No `*.Contracts`, module, or infrastructure surface changes; `ArchitectureTests`
guards this.

## Phase 0 — Research

See [research.md](./research.md). All technical unknowns resolved; no NEEDS CLARIFICATION
markers remain in the spec.

## Phase 1 — Design & Contracts

- Entities & state: [data-model.md](./data-model.md) (pending return address; no schema change)
- Interface contracts: [contracts/enroll-action.md](./contracts/enroll-action.md),
  [contracts/return-url.md](./contracts/return-url.md)
- Validation guide: [quickstart.md](./quickstart.md)

## Complexity Tracking

> No Constitution Check violations — nothing to track.
