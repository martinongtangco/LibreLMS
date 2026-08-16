# Implementation Plan: Clickable Brand Link to Home

**Branch**: `story/029-clickable-home-brand` | **Date**: 2026-08-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/029-clickable-home-brand/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command; its definition describes the execution workflow.

## Summary

Make the "Libre LMS" navbar brand a link to Home (the site root, which serves
Browse Courses) so no page — most critically the Login page for signed-out
users — is a navigation dead end. The brand is currently a plain
`<span class="brand">` in `_Layout.cshtml`; it becomes
`<a href="/" class="brand">`, with a small CSS addition to `site.css`
(`text-decoration: none` on the anchor plus a hover affordance consistent
with the existing nav links). The link is rendered in the shared navbar for
both the authenticated and anonymous branches, so it appears on every page
with no role awareness. The root URL already redirects to Browse Courses
(`app.MapGet("/", () => Results.Redirect("/Courses"))` in `Program.cs`), so
no routing changes are needed. A new Playwright spec
(`11-brand-home-link.spec.ts`) covers all acceptance scenarios per
Constitution Principle XIII.

Two files are the primary targets: `_Layout.cshtml` (one-line markup change)
and `site.css` (brand anchor styling). One new E2E test file validates the
change end-to-end.

## Technical Context

**Language/Version**: C# / ASP.NET Core Razor Pages (.NET 10), HTML/CSHTML, CSS, TypeScript (Playwright tests only)

**Primary Dependencies**: Lucide icons (CDN — already loaded in `_Layout.cshtml`); Playwright (already configured in `tests/Playwright.Tests`)

**Storage**: N/A — no data model changes; purely presentational

**Testing**: Playwright E2E (`tests/Playwright.Tests`, Chromium, `http://localhost:5000`); ArchitectureTests (`dotnet test tests/ArchitectureTests`)

**Target Platform**: Web browser (desktop and mobile), served via ASP.NET Core Razor Pages

**Project Type**: Web application (ASP.NET Core Razor Pages, modular monolith)

**Performance Goals**: No measurable change — one 302 redirect (root → /Courses) on brand navigation; no new JS or server work

**Constraints**: No new npm packages or build steps; no module boundary changes; no server-side code changes; brand must target Home for all roles (not the admin Dashboard); no changes to Login/Signup flows

**Scale/Scope**: 2 files modified (`_Layout.cshtml`, `site.css`); 1 new E2E test file; 1-line markup change plus 2 CSS rules

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | N/A | No module changes — pure presentation in Host |
| II. Clean Architecture | PASS | No new abstractions; one markup element changed from span to anchor |
| III. Module Boundaries Compiled | N/A | No cross-module references |
| IV. Human-Legible Code | PASS | One-line markup change + two CSS rules; no new JS needed |
| V. Sandbox Not Optional | N/A | No host filesystem access beyond repo |
| VI. Polyglot Storage | N/A | No storage changes |
| VII. Spec-Driven, Sliced Thin | PASS | Single vertical slice: universal "brand = Home" navigation |
| VIII. Branching Discipline | PASS | `story/029-clickable-home-brand` to be created before code changes begin (implementation phase; user initiates) |
| IX. Plan On Master Only | PASS | Currently on `master` |
| X. No Ad-Hoc Fixes | PASS | Spec exists (029-clickable-home-brand) |
| XI. Parallel Implementation | N/A | Evaluated during tasks phase |
| XII. Return to Master | N/A | Evaluated at end of implementation |

## Project Structure

### Documentation (this feature)

```text
specs/029-clickable-home-brand/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (N/A — no model changes)
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── brand-link.md    # Phase 1 output (UI contract for the navbar brand link)
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (files affected)

```text
src/Host/
├── Pages/Shared/
│   └── _Layout.cshtml   # <span class="brand"> → <a href="/" class="brand">
└── wwwroot/css/
    └── site.css         # .navbar .brand: text-decoration none + hover affordance

tests/Playwright.Tests/
└── tests/
    └── 11-brand-home-link.spec.ts   # NEW — E2E: brand link on every page (Principle XIII)
```

**Structure Decision**: Two-file change plus one new test file. `_Layout.cshtml`
provides the shared navbar rendered on every page (the brand sits outside the
authenticated/anonymous `@if` split, so a single edit covers both states).
`site.css` supplies the anchor styling. Consistent with the precedent set by
018-nav-design-alignment, 020-nav-account-menu, and 021-admin-menu-structure —
no new files, components, or build steps (Principle II).

## Complexity Tracking

No Constitution Check violations — section not applicable.
