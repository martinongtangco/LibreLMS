# Feature Specification: Playwright Automated UI Tests

**Feature Branch**: `story/022-playwright-ui-tests`

> **Branch naming** (Constitution Principle VIII): `story/<id>-<desc>` for features.

**Created**: 2025-08-05

**Status**: Complete (merged 2026-08-07)

**Input**: User description: "develop a playwright automated tests on existing UI"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run UI smoke tests against the live LMS (Priority: P1)

As a developer or CI pipeline, I want to run automated end-to-end tests against the Libre LMS web portal so that regressions in the core user flows are caught before merge.

**Why this priority**: This is the foundational test suite — without it, every UI change is manually verified. Covers login, course browsing, enrollment, and admin pages.

**Independent Test**: Can be fully tested by starting the app with `dotnet run`, running `npx playwright test`, and confirming all tests pass against the seeded data.

**Acceptance Scenarios**:

1. **Given** the LMS is running with seeded data, **When** Playwright tests execute, **Then** all core smoke tests pass (login, browse courses, view course detail, enroll in course, view my courses)
2. **Given** the LMS is running with seeded data, **When** a test logs in as a Learner, **Then** admin-only pages return 403 or redirect to login
3. **Given** the LMS is running with seeded data, **When** a test logs in as an OrgAdmin, **Then** admin pages (Dashboard, Learners, Organizations, Enrollments) are accessible and display expected content
4. **Given** invalid credentials are provided, **When** the login form is submitted, **Then** an error message is displayed and the user remains on the login page

---

### User Story 2 - Test role-based access control (RBAC) via UI (Priority: P2)

As a security-conscious team, I want automated tests that verify different user roles see only the pages and content they're authorized to access.

**Why this priority**: RBAC is critical for a multi-tenant LMS. Automated verification prevents accidental permission leaks when new pages or routes are added.

**Independent Test**: Can be tested independently by running only the RBAC test file with `npx playwright test --grep "RBAC"` and verifying that unauthorized access is denied for each role.

**Acceptance Scenarios**:

1. **Given** an unauthenticated user, **When** they navigate to `/Admin/Dashboard/Index`, **Then** they are redirected to `/Account/Login`
2. **Given** a Learner is logged in, **When** they navigate to `/Admin/Dashboard/Index`, **Then** they are denied access (403 or redirect)
3. **Given** an OrgAdmin is logged in, **When** they navigate to `/Admin/Dashboard/Index`, **Then** the dashboard loads with metrics
4. **Given** a SuperUser is logged in, **When** they navigate to any admin page, **Then** they have full access

---

### User Story 3 - Test course browsing and search flows (Priority: P3)

As a learner, I want automated tests to verify that the course catalog browsing, search, and category filtering work correctly.

**Why this priority**: Course discovery is the primary learner workflow. Ensuring search and filters work is important but less critical than auth and RBAC.

**Independent Test**: Can be tested independently by running `npx playwright test --grep "course"` and verifying search/filter results match expected courses.

**Acceptance Scenarios**:

1. **Given** the course catalog page loads, **When** I type "C#" in the search box, **Then** results are filtered to show only C#-related courses
2. **Given** the course catalog page loads, **When** I select a category from the dropdown, **Then** only courses in that category are displayed
3. **Given** search results are displayed, **When** I click on a course card, **Then** the course detail page loads with the correct course information

---

### User Story 4 - Test responsive/mobile navigation (Priority: P3)

As a mobile user, I want automated tests that verify the navigation works correctly on different viewport sizes.

**Why this priority**: The app has a hamburger menu and responsive layout. Verifying this works across viewports prevents mobile regressions.

**Independent Test**: Can be tested independently by running `npx playwright test --grep "responsive"` with different viewport configurations.

**Acceptance Scenarios**:

1. **Given** the app loads on a mobile viewport (<768px), **When** I tap the hamburger menu, **Then** the navigation links appear
2. **Given** the app loads on a mobile viewport, **When** I tap a navigation link, **Then** the hamburger menu closes and the page navigates
3. **Given** an admin user views the app on mobile, **When** I toggle between Learner/Admin roles, **Then** admin links appear/disappear accordingly

### Edge Cases

- What happens when the database is not yet seeded (first startup race condition)?
- How does the test suite handle the `EnsureDeleted()` call in Program.cs that drops and recreates the database on every startup?
- What happens when Valkey (Redis) is unavailable — do SCORM-related tests fail gracefully?
- How are tests affected by HTMX partial-page updates vs full page navigations?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Test suite MUST use Playwright with TypeScript for test authoring
- **FR-002**: Test suite MUST authenticate using cookie-based login (matching the existing `LoginModel.OnPostAsync` flow)
- **FR-003**: Test suite MUST include seeded test credentials: `alice@example.com/password123` (Learner), `admin@example.com/password123` (OrgAdmin), `admin@librelms.local/Admin@12345` (SuperUser)
- **FR-004**: Test suite MUST wait for the app to be healthy before running tests (retry logic for startup)
- **FR-005**: Tests MUST cover the following page flows: Login, Browse Courses, Course Detail, My Courses, Admin Dashboard, Admin Learners, Admin Organizations, Admin Enrollments
- **FR-006**: Test suite MUST include RBAC verification tests for each role (Learner, OrgAdmin, SuperUser)
- **FR-007**: Tests MUST run headlessly by default and support headed mode via `--headed` flag
- **FR-008**: Test suite MUST include a `globalSetup` that waits for the app to be ready before tests begin
- **FR-009**: Tests MUST use Page Object Model pattern for maintainable selectors and actions
- **FR-010**: Test suite MUST be runnable from the repo root with a single command: `npm run test:e2e` (or equivalent)

### Key Entities

- **Test User**: Represents a seeded user account with known credentials and role (Learner, OrgAdmin, SuperUser)
- **Page Object**: Encapsulates selectors and actions for a specific Razor Page
- **Test Fixture**: Provides authenticated browser context per test file, with login/logout lifecycle

## Assumptions

- The LMS app runs on `http://localhost:5000` (default Kestrel port) during test execution
- Seeded data is available immediately after app startup (database migrations complete before tests run)
- The app uses `EnsureDeleted()` + `Migrate()` on every startup, so tests must wait for startup to complete
- Playwright browsers will be installed in the test project (via `npx playwright install --with-deps`)
- Tests run inside the same devcontainer as development (Constitution Principle V)
- HTMX-driven partial updates are visible to Playwright since it controls a real browser
