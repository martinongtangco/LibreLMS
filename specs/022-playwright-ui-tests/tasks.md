# Tasks: Playwright Automated UI Tests

**Input**: Design documents from `/specs/022-playwright-ui-tests/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/test-contract.md

**Tests**: This IS a test project — all tasks produce test infrastructure or test cases.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Project Initialization)

**Goal**: Create the Playwright test project structure with all configuration files and shared utilities.

- [X] T001 Create `tests/Playwright.Tests/` directory structure with subdirectories `pages/`, `fixtures/`, `tests/`, `utils/`

- [X] T002 Initialize `tests/Playwright.Tests/package.json` with dependencies: `@playwright/test`, `typescript`, and `npm run test:e2e` script

- [X] T003 Create `tests/Playwright.Tests/tsconfig.json` with strict TypeScript settings targeting ES2022

- [X] T004 Create `tests/Playwright.Tests/playwright.config.ts` with Chromium project, `http://localhost:5000` baseURL, 30s test timeout, globalSetup pointing to `global-setup.ts`, and trace retention on failure

- [X] T005 [P] Create `tests/Playwright.Tests/global-setup.ts` with HTTP poll loop against `GET /` (follow redirects) that retries every 2s for up to 120s until the app returns 200

- [X] T006 [P] Create `tests/Playwright.Tests/utils/testUsers.ts` exporting `TestUser` interface and constants for seeded users: `alice@example.com/password123` (Learner), `admin@example.com/password123` (OrgAdmin), `admin@librelms.local/Admin@12345` (SuperUser), `bob@example.com/password123` (Learner), `carol@example.com/password123` (Learner)

- [X] T007 [P] Create `tests/Playwright.Tests/utils/appHealth.ts` with `waitForAppReady(baseURL, maxWaitMs)` utility function for health-check polling

---

## Phase 2: Foundational (Page Objects and Fixtures)

**Purpose**: Core Page Object Models and authentication fixture that ALL user story tests depend on.

**⚠️ CRITICAL**: No user story tests can begin until this phase is complete.

- [X] T008 Create `tests/Playwright.Tests/pages/BasePage.ts` with `BasePage` class: constructor taking `page: Page`, `waitForHtmxSettle()` helper that waits for `.htmx-indicator` to disappear, and `waitForNavigation(url)` for full-page navigations

- [X] T009 [P] Create `tests/Playwright.Tests/pages/LoginPage.ts` extending `BasePage`: `emailInput` locator (by label "Email"), `passwordInput` locator (by label "Password"), `signInButton` locator (by role button "Sign In"), `errorMessage` locator (`.error-message`), `login(email, password)` action method, `isOnLoginPage()` assertion

- [X] T010 [P] Create `tests/Playwright.Tests/pages/CourseBrowsePage.ts` extending `BasePage`: `searchInput` locator (`#search-input`), `categorySelect` locator (by `<select>` name "category"), `clearButton` locator (by role link "Clear"), `courseCard(courseTitle)` dynamic locator, `isOnBrowsePage()` assertion, `searchFor(query)` action, `selectCategory(cat)` action, `getCourseTitles()` returning visible course titles

- [X] T011 [P] Create `tests/Playwright.Tests/pages/CourseDetailPage.ts` extending `BasePage`: `courseTitle` locator (page `<h1>`), `courseDescription` locator, `enrollButton` locator (by role button "Enroll"), `isOnDetailPage()` assertion, `getCourseTitle()` returning text

- [X] T012 [P] Create `tests/Playwright.Tests/pages/MyCoursesPage.ts` extending `BasePage`: `refreshButton` locator (by role button "Refresh"), `enrollmentList` locator (`#enrollment-list`), `courseRow(courseTitle)` dynamic locator, `isOnMyCoursesPage()` assertion, `getEnrolledCourseTitles()` returning visible titles

- [X] T013 [P] Create `tests/Playwright.Tests/pages/AdminDashboardPage.ts` extending `BasePage`: `metricCard(label)` dynamic locator for metric cards by label text, `metricValue(label)` locator for the value under a given metric label, `allCoursesTable` locator (`.courses-table`), `isOnDashboardPage()` assertion, `getMetricValue(label)` returning metric number as string

- [X] T014 [P] Create `tests/Playwright.Tests/pages/AdminLearnersPage.ts` extending `BasePage`: `learnerTable` locator, `learnerRow(name)` dynamic locator, `createButton` locator (by role button "Create"), `isOnLearnersPage()` assertion, `getLearnerNames()` returning visible names

- [X] T015 [P] Create `tests/Playwright.Tests/pages/AdminOrganizationsPage.ts` extending `BasePage`: `orgTable` locator, `orgRow(name)` dynamic locator, `createButton` locator (by role button "Create"), `isOnOrganizationsPage()` assertion, `getOrganizationNames()` returning visible names

- [X] T016 [P] Create `tests/Playwright.Tests/pages/AdminEnrollmentsPage.ts` extending `BasePage`: `enrollmentTable` locator, `bulkEnrollButton` locator, `isOnEnrollmentsPage()` assertion

- [X] T017 [P] Create `tests/Playwright.Tests/pages/AccountPage.ts` extending `BasePage`: `accountControl` locator (`#account-control`), `accountName` locator (`.account-name`), `accountDropdown` locator (`#account-dropdown`), `profileLink` locator (by role link "View Profile"), `settingsLink` locator (by role link "Settings"), `getAccountName()` returning text, `clickAccountDropdown()` action

- [X] T018 Create `tests/Playwright.Tests/fixtures/authFixture.ts` with `loginAs(role: 'Learner'|'OrgAdmin'|'SuperUser')` async function that navigates to `/Account/Login`, fills credentials from `testUsers.ts`, submits form, and waits for redirect to `/Courses/Index`; and `logout()` function that navigates to `/Account/Logout`

**Checkpoint**: Foundation ready — all page objects and fixtures exist. User story implementation can now begin.

---

## Phase 3: User Story 1 - Run UI Smoke Tests (Priority: P1) 🎯 MVP

**Goal**: Core smoke tests covering login, course browsing, enrollment, my courses, and admin page access. This is the MVP — the test suite works end-to-end after this phase.

**Independent Test**: Start the app, run `npx playwright test tests/01-auth.spec.ts tests/02-course-browse.spec.ts tests/03-enrollment.spec.ts tests/04-admin-dashboard.spec.ts` and all tests pass.

### Implementation for User Story 1

- [X] T019 [US1] Create `tests/Playwright.Tests/tests/01-auth.spec.ts` with test suite "Authentication": (a) "successful login with learner credentials" — logs in as alice@example.com, verifies redirect to /Courses/Index and account name shows "Alice Johnson"; (b) "rejects invalid credentials" — submits wrong password, verifies error message displayed and stays on login page; (c) "successful login with admin credentials" — logs in as admin@example.com, verifies admin nav links are visible

- [X] T020 [US1] Create `tests/Playwright.Tests/tests/02-course-browse.spec.ts` with test suite "Course Browse Smoke": (a) "browse page loads and shows seeded courses" — logs in as Learner, navigates to /Courses/Index, verifies at least 10 course cards are visible; (b) "click course card navigates to detail page" — clicks first course card, verifies /Courses/Detail loads with correct title; (c) "unauthenticated user can browse courses" — uses fresh (logged-out) context, navigates to /Courses/Index, verifies page loads

- [X] T021 [US1] Create `tests/Playwright.Tests/tests/03-enrollment.spec.ts` with test suite "Enrollment Smoke": (a) "enroll in a course from detail page" — as bob@example.com (not yet enrolled in "Advanced .NET Patterns"), navigates to that course detail, clicks Enroll, verifies success message; (b) "view my courses shows enrolled courses" — navigates to /MyCourses/Index, verifies at least one course appears in the enrollment list

- [X] T022 [US1] Create `tests/Playwright.Tests/tests/04-admin-dashboard.spec.ts` with test suite "Admin Dashboard Smoke": (a) "OrgAdmin can access dashboard" — logs in as admin@example.com, navigates to /Admin/Dashboard/Index, verifies metric cards show Organizations, Learners, Courses, Enrollments; (b) "dashboard shows non-zero metrics for seeded data" — verifies each metric value is a positive number

**Checkpoint**: At this point, the core smoke test suite is fully functional. Login, browse, enroll, and admin access are all tested. This is the MVP.

---

## Phase 4: User Story 2 - Role-Based Access Control Tests (Priority: P2)

**Goal**: Dedicated RBAC tests verifying each role (Learner, OrgAdmin, SuperUser, unauthenticated) can or cannot access each admin page.

**Independent Test**: Run `npx playwright test --grep "RBAC"` and verify unauthorized access is denied for each role/page combination.

### Implementation for User Story 2

- [X] T023 [US2] Create `tests/Playwright.Tests/tests/08-rbac.spec.ts` with test suite "RBAC — Unauthenticated": (a) "unauthenticated user redirected to login for /Admin/Dashboard/Index"; (b) "unauthenticated user redirected to login for /Admin/Learners/Index"; (c) "unauthenticated user redirected to login for /MyCourses/Index"

- [X] T024 [US2] Add to `tests/Playwright.Tests/tests/08-rbac.spec.ts` test suite "RBAC — Learner Access Denied": (a) "Learner cannot access /Admin/Dashboard/Index" — logs in as alice@example.com, navigates to dashboard, verifies 403 or redirect; (b) "Learner cannot access /Admin/Learners/Index"; (c) "Learner cannot access /Admin/Organizations/Index"; (d) "Learner cannot access /Admin/Enrollments/Index"; (e) "Learner CAN access /Courses/Index and /MyCourses/Index"

- [X] T025 [US2] Add to `tests/Playwright.Tests/tests/08-rbac.spec.ts` test suite "RBAC — OrgAdmin Full Access": (a) "OrgAdmin can access /Admin/Dashboard/Index"; (b) "OrgAdmin can access /Admin/Learners/Index"; (c) "OrgAdmin can access /Admin/Organizations/Index"; (d) "OrgAdmin can access /Admin/Enrollments/Index"; (e) "OrgAdmin can access /Admin/Courses/Index"; (f) "OrgAdmin can access /Admin/Upload"

- [X] T026 [US2] Add to `tests/Playwright.Tests/tests/08-rbac.spec.ts` test suite "RBAC — SuperUser Full Access": (a) "SuperUser can access all admin pages" — logs in as admin@librelms.local, iterates over all admin paths, verifies each loads (200 or redirect to content page)

**Checkpoint**: RBAC tests complete. Every role × page combination is verified.

---

## Phase 5: User Story 3 - Course Browsing and Search Flows (Priority: P3)

**Goal**: Tests for course catalog search, category filtering, and course detail navigation — the primary learner workflow.

**Independent Test**: Run `npx playwright test --grep "course"` and verify search/filter results match expected seeded courses.

### Implementation for User Story 3

- [X] T027 [P] [US3] Add to `tests/Playwright.Tests/tests/02-course-browse.spec.ts` test suite "Course Search": (a) "search by keyword filters results" — types "C#" in search box, waits for HTMX update, verifies only "Introduction to C#" appears; (b) "clear button resets search" — clicks Clear, verifies all 10 courses reappear; (c) "search with no results shows empty state" — types "xyznonexistent", verifies no course cards match

- [X] T028 [P] [US3] Add to `tests/Playwright.Tests/tests/02-course-browse.spec.ts` test suite "Course Category Filter": (a) "selecting Programming category shows 4 courses" — selects "Programming" from dropdown, waits for HTMX update, verifies 4 course cards; (b) "selecting Design category shows 2 courses"; (c) "selecting Database category shows 2 courses"; (d) "selecting Tools category shows 2 courses"; (e) "selecting All Categories shows all 10 courses"

- [X] T029 [US3] Add to `tests/Playwright.Tests/tests/02-course-browse.spec.ts` test suite "Course Detail Navigation": (a) "clicking a course card loads detail page with correct info" — clicks "Introduction to C#", verifies /Courses/Detail loads, title matches, description contains "C# programming"; (b) "SCORM course detail shows launch option" — clicks "Introduction to C#" (the seeded SCORM course), verifies launch button or link is present

**Checkpoint**: Course browsing and search tests complete.

---

## Phase 6: User Story 4 - Responsive/Mobile Navigation Tests (Priority: P3)

**Goal**: Verify navigation works correctly on different viewport sizes, including hamburger menu and role toggle.

**Independent Test**: Run `npx playwright test --grep "responsive"` with different viewport configurations.

### Implementation for User Story 4

- [X] T030 [US4] Create `tests/Playwright.Tests/tests/09-responsive.spec.ts` with Playwright mobile viewport config (e.g., `{ viewport: { width: 375, height: 812 } }`): (a) "hamburger menu toggles navigation on mobile" — at 375px width, clicks hamburger button (#nav-toggle), verifies nav links (#nav-links) become visible with `is-open` class; (b) "clicking nav link closes hamburger menu" — clicks a nav link inside hamburger, verifies menu closes; (c) "admin links hidden by default on mobile for Learner" — logs in as Learner, verifies `.admin-link` elements are not visible; (d) "role toggle shows admin links on mobile" — as OrgAdmin, clicks role-segment "admin", verifies admin links appear

**Checkpoint**: Responsive tests complete.

---

## Phase 7: Admin Page Detail Tests

**Goal**: Complete remaining admin page tests for Learners, Organizations, and Enrollments management.

### Implementation for Admin Pages

- [X] T031 [P] Create `tests/Playwright.Tests/tests/05-admin-learners.spec.ts` with test suite "Admin Learners": (a) "learner list shows seeded users" — as OrgAdmin, navigates to /Admin/Learners/Index, verifies Alice, Bob, Carol appear; (b) "create learner form is accessible" — clicks Create button, verifies create page/form loads

- [X] T032 [P] Create `tests/Playwright.Tests/tests/06-admin-organizations.spec.ts` with test suite "Admin Organizations": (a) "organization list shows root org" — as OrgAdmin, navigates to /Admin/Organizations/Index, verifies "Root Organization" appears; (b) "create organization form is accessible" — clicks Create button, verifies create page/form loads

- [X] T033 [P] Create `tests/Playwright.Tests/tests/07-admin-enrollments.spec.ts` with test suite "Admin Enrollments": (a) "enrollment list shows seeded enrollments" — as OrgAdmin, navigates to /Admin/Enrollments/Index, verifies Alice's enrollment in "Introduction to C#" appears; (b) "bulk enroll form is accessible" — verifies bulk enroll page/form is reachable

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect the entire test suite.

- [X] T034 [P] Add `tests/Playwright.Tests/.gitignore` with `node_modules/`, `test-results/`, `playwright-report/`, `trace/`

- [X] T035 Update `tests/Playwright.Tests/playwright.config.ts` to add `retries: 2` for flaky-test resilience and `reporter: [['list'], ['html']]` for dual output

- [X] T036 [P] Add `tests/Playwright.Tests/tests/01-auth.spec.ts` test for logout flow: "logout clears session" — logs in, navigates to /Account/Logout, verifies redirect to /Account/Login and account control is not visible

- [X] T037 Add npm script `test:e2e:headed` to `tests/Playwright.Tests/package.json` running `npx playwright test --headed`

- [X] T038 Validate full suite: run `cd tests/Playwright.Tests && npx playwright test` — **46 passed (8.0s)**

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user story tests
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP, complete after this phase
- **User Story 2 (Phase 4)**: Depends on Foundational — independent of US1, US3, US4
- **User Story 3 (Phase 5)**: Depends on Foundational — extends existing test file from US1 (T020)
- **User Story 4 (Phase 6)**: Depends on Foundational — independent
- **Admin Pages (Phase 7)**: Depends on Foundational — independent
- **Polish (Phase 8)**: Depends on all story phases

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependencies on other stories
- **US2 (P2)**: Can start after Foundational — independent of US1
- **US3 (P3)**: Can start after Foundational — extends `02-course-browse.spec.ts` from US1 but adds new test suites (no blocking)
- **US4 (P3)**: Can start after Foundational — independent

### Parallel Opportunities

- All Phase 1 tasks marked [P] (T005-T007) can run in parallel after T001-T004
- All Phase 2 page object tasks (T009-T017) are marked [P] and can run in parallel
- US1, US2, US4, and Phase 7 admin page tests can all proceed in parallel after Foundational
- Within US3: T027 and T028 are marked [P] (add to same file but different test suites — coordinate writes)

---

## Parallel Example: Foundational Phase

```bash
# Launch all page objects in parallel (after BasePage + Login are done):
Task: "Create CourseBrowsePage.ts in tests/Playwright.Tests/pages/"
Task: "Create CourseDetailPage.ts in tests/Playwright.Tests/pages/"
Task: "Create MyCoursesPage.ts in tests/Playwright.Tests/pages/"
Task: "Create AdminDashboardPage.ts in tests/Playwright.Tests/pages/"
Task: "Create AdminLearnersPage.ts in tests/Playwright.Tests/pages/"
Task: "Create AdminOrganizationsPage.ts in tests/Playwright.Tests/pages/"
Task: "Create AdminEnrollmentsPage.ts in tests/Playwright.Tests/pages/"
Task: "Create AccountPage.ts in tests/Playwright.Tests/pages/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T007)
2. Complete Phase 2: Foundational (T008-T018) — CRITICAL, blocks all stories
3. Complete Phase 3: US1 Smoke Tests (T019-T022)
4. **STOP and VALIDATE**: Run `npx playwright test` — all smoke tests should pass
5. This is a complete, working test suite for the core flows

### Incremental Delivery

1. MVP: Setup + Foundational + US1 → Core smoke tests work
2. Add US2 → RBAC verification complete
3. Add US3 → Course search/filter tested
4. Add US4 → Responsive navigation tested
5. Add Phase 7 → Admin page detail tests complete
6. Add Phase 8 → Polish, retries, reporting

### Parallel Team Strategy

With multiple developers or subagents:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (smoke tests) + US3 (extends same file)
   - Developer B: US2 (RBAC tests)
   - Developer C: US4 (responsive) + Phase 7 (admin pages)
3. All stories merge into `tests/Playwright.Tests/tests/` independently

---

## Notes

- [P] tasks = different files, no dependencies on each other
- [Story] label maps task to specific user story for traceability
- US3 tasks extend files created in US1 (T027/T028 add to `02-course-browse.spec.ts` from T020) — coordinate to avoid overwriting
- Each user story is independently completable and runnable with `--grep`
- Commit after each phase to enable incremental delivery
- Run `npx playwright test --headed` for debugging individual failures
- The app MUST be running before `npx playwright test` (global-setup waits for it)
