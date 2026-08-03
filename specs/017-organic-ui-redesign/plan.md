# Implementation Plan: Organic Design System Redesign

**Branch**: `story/017-organic-ui-redesign` | **Date**: 2026-08-03 | **Spec**: [spec.md](spec.md)

> **Branch naming** (Constitution Principle VIII): `bug/<id>-<desc>` for defects,
> `story/<id>-<desc>` for features. Example: `story/017-organic-ui-redesign`.

**Input**: Feature specification from `/specs/017-organic-ui-redesign/spec.md`

## Summary

Re-skin the six screens named in the design handoff (My Courses, Browse Courses, Course Detail,
Admin Dashboard, and two brand-new Profile/Settings pages) plus the shared nav shell, in the
"Organic" design system (warm cream ground, terracotta/sage accents, Caprasimo/Figtree type,
16px/pill radius) — reusing the site's existing CSS-custom-property token mechanism rather than
introducing a component library. All existing business logic (search/filter, enroll, SCORM
status, dashboard metrics) is reused as-is; the only new backend surface is two persisted
per-student preference fields backing the new Settings page and one new read query for the
dashboard's per-course enrollment counts.

## Technical Context

**Language/Version**: C# 13 / .NET 10 (GA), pinned via `global.json`

**Primary Dependencies**: ASP.NET Core Razor Pages, EF Core (MSSQL), HTMX 2.0.4 (already loaded in `_Layout.cshtml`) — no new client-side framework or component library

**Storage**: MSSQL via EF Core. No new tables — one migration adds two nullable-with-default columns (`EmailNotificationsEnabled`, `ThemePreference`) to the existing `Students` table (`EnrollmentDbContext`). All other data (courses, enrollments, attempts, dashboard metrics) is read from existing tables/services.

**Testing**: xUnit (`Catalog.Tests`, `Enrollment.Tests`, `Scorm.Tests`), `ArchitectureTests` (NetArchTest) for module-boundary enforcement. No new test project needed — new service methods land in existing module test projects.

**Target Platform**: Web browser (mobile-first through desktop). Razor Pages served by ASP.NET Core on Linux inside the devcontainer.

**Project Type**: Web application — visual redesign within the existing modular monolith (`Host` + `Catalog`/`Enrollment`/`Scorm`/`Management` modules).

**Performance Goals**: No new performance budget beyond current behavior — restyled pages must not regress existing page-load times (course grids, dashboard) since no new N+1 query patterns are introduced (enrollment counts computed with a single grouped query, not per-course round-trips).

**Constraints**: No new NuGet packages or JS libraries. Fonts (Caprasimo, Figtree) self-hosted or loaded the same way existing fonts are (check `_Layout.cshtml`/`site.css` — currently system font stack, so this adds the project's first web-font `@font-face`/link). Must preserve the existing responsive/hamburger-nav mechanics (spec 015/016) — restyled, not rebuilt. Pages out of this slice's scope (Login, SCORM Launch, Admin Organizations/Learners/Course-management/Enrollments/Upload) must keep working unchanged, only inheriting the shared nav restyle.

**Scale/Scope**: 6 screens (2 net-new: Profile, Settings) + 1 shared layout/nav. Single-tenant admin scale already established by prior specs (dozens of orgs/courses, hundreds of learners) — no scale change.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | ✅ PASS | All changes live in `Host` (Razor Pages, CSS, wwwroot) plus small `Application`-layer additions to `Enrollment` (preference fields) and `Management` (course-enrollment-count read query, itself computed by combining existing `Enrollment`/`Catalog` services in Host — see Research). No new modules. |
| II. Clean Architecture | ✅ PASS | New `Student` fields are plain properties (no repository wrapper added). New read query is a service method, not a new layer. |
| III. Module Boundaries Compiled | ✅ PASS | No new module reaches into another module's `Domain`/`Infrastructure`. The dashboard's per-course enrollment count is assembled in `Host` by calling `CourseVisibilityService` (Management) and a new `EnrollmentService` method (Enrollment) directly — the same pattern `MyCoursesModel` already uses to combine `EnrollmentService` + `ScormAttemptService`. No new `*.Contracts` interface is required. |
| IV. Human-Legible AI-Authored Code | ✅ PASS | The one non-obvious decision — mapping SCORM attempt status/score to a display percentage — reuses the existing `ScormHelpers` mapping helpers; no new abstraction introduced without a one-sentence justification here and in `research.md`. |
| V. Sandbox Not Optional | ✅ PASS | No change to devcontainer/sandbox setup. |
| VI. Polyglot Storage | ✅ PASS | No Valkey involvement; the two new preference fields are durable per-user data, so they belong in MSSQL (`Students` table), not the ephemeral SCORM cache. |
| VII. Spec-Driven, Sliced Thin | ✅ PASS | Scope is the 6 named screens + nav shell only (per spec Assumptions); other pages are explicitly deferred to a follow-up slice. |
| VIII. Branching Discipline | ✅ PASS | Branch: `story/017-organic-ui-redesign`, created at `/speckit.implement` time. |
| IX. Plan On Master | ✅ PASS | Planning executed on `master`. |
| X. No Ad-Hoc Fixes | ✅ PASS | Spec exists at `specs/017-organic-ui-redesign/spec.md`, created via `/speckit.specify` before this plan. |
| XI. Parallel Implementation With Subagents | N/A here | Applies at `/speckit.implement` time — tasks.md will mark independent page/CSS work `[P]`. |
| XII. Return to Master After Implementation | N/A here | Applies at `/speckit.implement` completion. |

No violations — Complexity Tracking table is empty.

## Project Structure

### Documentation (this feature)

```text
specs/017-organic-ui-redesign/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code Changes

```text
src/
├── Host/
│   ├── Pages/
│   │   ├── Shared/
│   │   │   └── _Layout.cshtml            # Modified: Organic nav markup + avatar/profile dropdown, hamburger kept
│   │   ├── MyCourses/Index.cshtml        # Modified: Organic card markup (view only — page model unchanged)
│   │   ├── Courses/Index.cshtml          # Modified: Organic toolbar + card grid (Browse Courses)
│   │   ├── Courses/Detail.cshtml         # Modified: Organic hero + enroll CTA
│   │   ├── Admin/Dashboard/
│   │   │   ├── Index.cshtml              # Modified: Organic stat tiles + all-courses table
│   │   │   └── Index.cshtml.cs           # Modified: add per-course enrollment counts to the view model
│   │   └── Account/
│   │       ├── Profile.cshtml            # New: View Profile page
│   │       ├── Profile.cshtml.cs         # New: page model (name/role/email)
│   │       ├── Settings.cshtml           # New: Settings page (notifications, theme, logout)
│   │       └── Settings.cshtml.cs        # New: page model (GET current prefs, POST to update)
│   └── wwwroot/
│       └── css/
│           └── site.css                  # Modified: Organic token values, Caprasimo/Figtree font-face, card/tag/button/nav shape rules
├── Modules/
│   ├── Enrollment/
│   │   ├── Domain/Student.cs             # Modified: + EmailNotificationsEnabled (bool), ThemePreference (string)
│   │   ├── Application/EnrollmentService.cs  # Modified: + GetPreferencesAsync/UpdatePreferencesAsync, + GetEnrollmentCountsByCourseAsync
│   │   └── Infrastructure/EnrollmentDbContext.cs # Modified: EF configuration for new columns
│   └── (Catalog, Scorm, Management)      # Unchanged — consumed as-is via existing Application services
└── Host/Migrations/                      # New: EF Core migration adding the two Student columns

tests/
└── Enrollment.Tests/                     # Modified: unit tests for GetPreferencesAsync/UpdatePreferencesAsync/GetEnrollmentCountsByCourseAsync
```

**Structure Decision**: This is a view-layer redesign living almost entirely in `Host/Pages` and `Host/wwwroot/css`. The only domain change is two new columns on the existing `Student` entity in the `Enrollment` module (which already owns `Student`), surfaced through two new `EnrollmentService` methods. The Admin Dashboard's new per-course enrollment count is computed in `Host`'s existing `Admin/Dashboard/IndexModel` by combining `CourseVisibilityService` (Management) and the new `EnrollmentService.GetEnrollmentCountsByCourseAsync` (Enrollment) — mirroring the established `MyCoursesModel` pattern of a Host page model combining two modules' Application services. No new module, no new `*.Contracts` interface, no new component library.

## Phase 0: Research

See [research.md](research.md) for resolved technical decisions (font loading, status→progress-percentage mapping, theme-preference scope).

## Phase 1: Design & Contracts

See [data-model.md](data-model.md), [contracts/](contracts/), and [quickstart.md](quickstart.md).

## Complexity Tracking

No constitution violations to justify. All principles satisfied.
