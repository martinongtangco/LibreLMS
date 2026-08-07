# Implementation Plan: Playwright Automated UI Tests

**Branch**: `story/022-playwright-ui-tests` | **Date**: 2025-08-05 | **Spec**: `specs/022-playwright-ui-tests/spec.md`

**Input**: Feature specification from `/specs/022-playwright-ui-tests/spec.md`

## Summary

Add a Playwright-based end-to-end test suite for the Libre LMS Razor Pages web portal. The suite covers authentication flows, course browsing, enrollment, admin pages, and role-based access control (RBAC). Tests use TypeScript with the Page Object Model pattern, run headlessly by default, and wait for the app to be healthy before executing.

## Technical Context

**Language/Version**: TypeScript 5.x (Playwright test authoring) alongside C# 10 / .NET 10 (application under test)

**Primary Dependencies**: `@playwright/test`, `playwright` (browser binaries), `typescript`

**Storage**: N/A (tests read from the live app; no separate test database — uses seeded data in MSSQL)

**Testing**: Playwright (`npx playwright test`) with globalSetup for app readiness checks

**Target Platform**: Linux (inside devcontainer), Chromium browser (default), with Firefox/WebKit in CI

**Project Type**: Web application (ASP.NET Core Razor Pages + HTMX)

**Performance Goals**: Full suite completes in under 2 minutes headlessly

**Constraints**: Tests must wait for app startup (EnsureDeleted + Migrate + Seed can take 15-30s); HTMX partial updates require explicit waits

**Scale/Scope**: ~30-40 test cases across 6-8 test files, covering all seeded pages and roles

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Monolith | PASS | Tests are external to the monolith — a separate `tests/Playwright.Tests` directory |
| II. Clean Architecture | N/A | Tests interact only with the web surface (Razor Pages + API), not internal modules |
| III. Module Boundaries | N/A | Tests do not reference any module project — they use HTTP only |
| IV. Human-Legible Code | PASS | Page Object Model pattern provides clear, readable test structure |
| V. Sandbox Isolation | PASS | Tests run inside the devcontainer against the same app process |
| VI. Polyglot Storage | PASS | Tests use no storage directly — they interact with the live app |
| VII. Spec-Driven | PASS | This plan follows from the spec in `specs/022-playwright-ui-tests/spec.md` |
| VIII. Branching Discipline | PASS | Branch: `story/022-playwright-ui-tests` |
| IX. Plan On Master | PASS | Planning running on `master` branch |
| X. No Ad-Hoc Fixes | PASS | Documented via SpecKit workflow |

## Project Structure

### Documentation (this feature)

```text
specs/022-playwright-ui-tests/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── test-contract.md # Test behavior contract
└── spec.md              # Feature specification
```

### Source Code (repository root)

```text
tests/
├── Playwright.Tests/                    # New: Playwright E2E test project
│   ├── package.json                     # NPM package with Playwright deps
│   ├── playwright.config.ts             # Playwright configuration
│   ├── tsconfig.json                    # TypeScript config
│   ├── global-setup.ts                  # Wait for app readiness
│   ├── pages/                           # Page Object Models
│   │   ├── LoginPage.ts
│   │   ├── CourseBrowsePage.ts
│   │   ├── CourseDetailPage.ts
│   │   ├── MyCoursesPage.ts
│   │   ├── AdminDashboardPage.ts
│   │   ├── AdminLearnersPage.ts
│   │   ├── AdminOrganizationsPage.ts
│   │   ├── AdminEnrollmentsPage.ts
│   │   ├── AccountPage.ts
│   │   └── BasePage.ts                  # Shared selectors, auth helpers
│   ├── fixtures/
│   │   └── authFixture.ts               # Authenticated fixture per role
│   ├── tests/
│   │   ├── 01-auth.spec.ts              # Login/logout flows
│   │   ├── 02-course-browse.spec.ts     # Browse, search, filter courses
│   │   ├── 03-enrollment.spec.ts        # Enroll in course, view my courses
│   │   ├── 04-admin-dashboard.spec.ts   # Admin dashboard metrics
│   │   ├── 05-admin-learners.spec.ts    # Admin learner management
│   │   ├── 06-admin-organizations.spec.ts # Admin org management
│   │   ├── 07-admin-enrollments.spec.ts # Admin enrollment management
│   │   ├── 08-rbac.spec.ts              # Role-based access control
│   │   └── 09-responsive.spec.ts        # Mobile viewport tests
│   └── utils/
│       ├── appHealth.ts                 # Health check utility
│       └── testUsers.ts                 # Seeded test user credentials
├── ArchitectureTests/                   # Existing
├── Catalog.Tests/                       # Existing
├── Enrollment.Tests/                    # Existing
└── Scorm.Tests/                         # Existing
```

**Structure Decision**: Playwright tests live in `tests/Playwright.Tests/` alongside existing .NET test projects, maintaining the `tests/` convention. The directory uses Node.js/TypeScript (standard for Playwright) rather than a .NET wrapper, because Playwright's native TypeScript API is the idiomatic approach and avoids adding a .NET Playwright wrapper layer.

## Complexity Tracking

N/A — no constitution violations. This is a standard test project addition.
