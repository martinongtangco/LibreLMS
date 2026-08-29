# Implementation Plan: Per-User Theme Preference (System / Light / Dark)

**Branch**: `story/042-user-theme-preference` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/001-course-catalog-browse`.

**Input**: Feature specification from `/specs/042-user-theme-preference/spec.md`

## Summary

Finalize Settings > Theme: the selector (System/Light/Dark, System default) and its
account-level persistence already exist in the Enrollment module; this slice makes the
saved preference actually drive the app's appearance. The chosen theme is carried in the
auth cookie as a claim (the same pattern spec 030 established for the avatar — the layout
stays claim-only, no DB access in the layout), rendered server-side as a `data-theme`
attribute on `<html>`, with a small inline head script that resolves "System" from the
device setting before first paint (no flash) and follows live device changes. Two CSS
palettes are defined via design-token overrides: a warm paper Light (surfaces no longer
pure white) and a soft, WCAG-AA Dark balanced for night reading. The Settings page saves
via a no-reload AJAX POST; the cookie claim is re-issued on save so every subsequent page
reflects the choice.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS, pinned via `global.json`), Razor Pages in the `Host` project

**Primary Dependencies**: ASP.NET Core minimal APIs + Razor Pages (existing), EF Core (existing, no schema change — `Student.ThemePreference` already exists), Playwright (existing E2E suite)

**Storage**: MSSQL via existing `Student.ThemePreference` column (system of record, default `"System"`); auth cookie claim as a derived, re-issued cache (spec 030 `AuthClaims`/`AuthCookieRefresher` pattern). Nothing new in Valkey.

**Testing**: xUnit for `AuthClaimsTests` claim-set pinning (existing, `tests/Host.Tests`), Playwright E2E for theme behavior (Principle XIII gate)

**Target Platform**: Web — modern browsers (Chrome/Edge/Firefox/Safari) on desktop and mobile; the app already ships responsive layouts (spec 015/016)

**Project Type**: web-service (modular monolith, single `Host` deployable)

**Performance Goals**: theme resolution adds zero extra queries per request (claim read from cookie); first paint with correct theme — no visible flash (SC-004)

**Constraints**: WCAG AA contrast (≥ 4.5:1 body/secondary text) in both palettes; no DB access in `_Layout.cshtml` (established layout convention); Razor views are precompiled — app restart required after view edits; anti-forgery stays implicit (no `.DisableAntiforgery()` on the new save endpoint)

**Scale/Scope**: 1 account-level setting, ~20 CSS design tokens × 2 palettes, 1 new page handler, 1 claim, 1 E2E spec file

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Modular Monolith | ✅ Pass | No new projects; changes stay in `Host` (composition root) and `Enrollment` (Contracts + Application). |
| II | Clean Architecture, Applied Simply | ✅ Pass | No new abstractions — reuses existing `AuthClaims.Build`, `AuthCookieRefresher`, `EnrollmentService`. One new claim + one DTO field, each explainable in one sentence. |
| III | Module Boundaries Are Compiled | ✅ Pass | `Host` already references `Enrollment.Contracts`; the new `ThemePreference` field lands on the Contracts record `StudentProvisionedDto`. `ArchitectureTests` must pass before done. |
| IV | Human-Legible AI-Authored Code | ✅ Pass (ADR required) | The storage decision (theme in auth cookie claim vs. per-request DB read) is a non-obvious structural choice → new ADR `docs/adr/0009-theme-preference-in-auth-claim.md`. |
| V | The Sandbox Is Not Optional | ✅ Pass | No change to sandboxing; all work inside the devcontainer. |
| VI | Polyglot Storage With a Reason | ✅ Pass | Preference stays in MSSQL (durable). The claim is a derived copy re-issued on sign-in/save — losing the cookie just costs one re-sign-in, and the DB value is untouched. Nothing new in Valkey. |
| VII | Spec-Driven, Sliced Thin | ✅ Pass | Spec 042 exists; vertical slice (user-visible capability), no horizontal layering. |
| VIII | Branching Discipline | ✅ Pass (at implementation) | Implementation runs on `story/042-user-theme-preference` from `master`. |
| IX | Plan On Master Only | ✅ Pass | This plan was authored on `master`. |
| X | No Ad-Hoc Fixes | ✅ Pass | This spec + plan document the change before any code edit. |
| XI | Parallel Implementation With Subagents | ➡️ Defers to tasks phase | Independent work items (CSS palette, claim plumbing, Settings handler, E2E spec) will be marked `[P]` in tasks.md. |
| XII | Return to Master After Implementation | ➡️ Defers to implementation | |
| XIII | Verification Before Claim | ➡️ Gates implementation | Build output + `Now listening` + Playwright pass + post-merge re-run, per quickstart.md. |

**Gate result**: PASS — no violations, no Complexity Tracking entries required.

## Project Structure

### Documentation (this feature)

```text
specs/042-user-theme-preference/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── theme-ui.md      # Page + save-endpoint contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Host/
│   ├── ManagementAuth/
│   │   ├── AuthClaims.cs            # + ThemePreference claim (always present)
│   │   └── AuthCookieRefresher.cs   # re-issue includes theme (reads new DTO field)
│   ├── Pages/
│   │   ├── Shared/_Layout.cshtml    # data-theme on <html> + inline head script (System resolve + live follow)
│   │   └── Account/
│   │       ├── Settings.cshtml      # theme select saves via AJAX (no reload), alerts
│   │       └── Settings.cshtml.cs   # + OnPostThemeAsync handler (JSON for AJAX save)
│   └── wwwroot/css/site.css         # paper-Light token adjustments + [data-theme="dark"] palette block
├── Modules/
│   ├── Enrollment.Contracts/
│   │   └── IUserProvisioning.cs     # StudentProvisionedDto + ThemePreference field
│   └── Enrollment/Application/
│       └── UserProvisioningService.cs  # populate new DTO field
└── docs/adr/0009-theme-preference-in-auth-claim.md   # NEW (Principle IV)

tests/
├── Host.Tests/
│   └── AuthClaimsTests.cs           # claim-set pinning updated for ThemePreference
└── Playwright.Tests/
    └── tests/18-theme-preference.spec.ts   # NEW E2E spec (Principle XIII)
```

**Structure Decision**: Single-project (modular monolith) layout per existing repo
structure. All changes fit inside `Host` + the `Enrollment` module pair; no new
directories beyond the ADR file and one E2E spec file.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

None — no violations to justify.
