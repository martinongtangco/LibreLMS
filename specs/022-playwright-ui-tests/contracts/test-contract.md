# Test Contract: Playwright E2E Test Suite

This document defines the behavioral contract for the Playwright test suite — what it tests, how it interacts with the application, and what outcomes it expects.

## Application Contract

### Base URL
- **Default**: `http://localhost:5000`
- **Configurable** via `BASE_URL` environment variable or Playwright config `use.baseURL`

### Startup Requirements
- The app must be running and responding to HTTP requests before tests begin
- The `global-setup.ts` polls `GET /` until it receives a 200 response (with redirects followed)
- Maximum wait: 120 seconds before timing out

### Authentication Contract
- Login endpoint: `POST /Account/Login` with form fields `Email` and `Password`
- On success: sets auth cookie, redirects to `/` → `/Courses/Index`
- On failure: stays on `/Account/Login` with error message in `.error-message` element
- Auth cookie persists for 7 days (`IsPersistent = true`)

## Test File Contract

Each test file follows this structure:

```
tests/
├── 01-auth.spec.ts           # Authentication flows
├── 02-course-browse.spec.ts  # Course catalog browsing
├── 03-enrollment.spec.ts     # Enrollment flows
├── 04-admin-dashboard.spec.ts # Admin dashboard
├── 05-admin-learners.spec.ts  # Learner management
├── 06-admin-organizations.spec.ts # Organization management
├── 07-admin-enrollments.spec.ts # Enrollment management
├── 08-rbac.spec.ts           # Role-based access control
└── 09-responsive.spec.ts     # Mobile/responsive tests
```

## Selector Contract

Tests MUST use semantic selectors over CSS class selectors:

| Prefer | Instead of |
|--------|-----------|
| `page.getByRole('button', { name: 'Sign In' })` | `page.locator('.btn.btn-primary')` |
| `page.getByLabel('Email')` | `page.locator('input[name="Email"]')` |
| `page.getByRole('link', { name: 'My Courses' })` | `page.locator('a.nav-link[data-page="my-courses"]')` |
| `page.getByText('Introduction to C#')` | `page.locator('.card h2')` |

**Rationale**: CSS classes change during UI redesigns. Semantic selectors are more stable and self-documenting.

## HTMX Update Contract

When a page uses HTMX for partial updates:
1. Do NOT use `page.waitForNavigation()` — use `page.waitForSelector()` or `expect().toBeVisible()`
2. Use Playwright's auto-waiting: `await locator.click()` then `await expect(resultLocator).toBeVisible()`
3. For loading indicators: `await expect(page.locator('.htmx-indicator')).not.toBeVisible()` before asserting on results

## Role-Based Page Visibility Contract

| Page | Unauthenticated | Learner | OrgAdmin | SuperUser |
|------|----------------|---------|----------|-----------|
| `/Courses/Index` | ✅ | ✅ | ✅ | ✅ |
| `/Courses/Detail` | ✅ | ✅ | ✅ | ✅ |
| `/Account/Login` | ✅ | Redirect | Redirect | Redirect |
| `/MyCourses/Index` | Redirect | ✅ | ✅ | ✅ |
| `/Admin/Dashboard/Index` | Redirect | 403/Redirect | ✅ | ✅ |
| `/Admin/Courses/Index` | Redirect | 403/Redirect | ✅ | ✅ |
| `/Admin/Learners/Index` | Redirect | 403/Redirect | ✅ | ✅ |
| `/Admin/Organizations/Index` | Redirect | 403/Redirect | ✅ | ✅ |
| `/Admin/Enrollments/Index` | Redirect | 403/Redirect | ✅ | ✅ |
| `/Admin/Upload` | Redirect | 403/Redirect | ✅ | ✅ |

## Test Timing Contract

- **Individual test timeout**: 30 seconds
- **Full suite timeout**: 5 minutes
- **App startup wait**: Up to 120 seconds (global setup)
- **HTMX request wait**: Up to 5 seconds (Playwright default)
