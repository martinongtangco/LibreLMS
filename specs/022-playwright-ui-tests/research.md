# Research: Playwright E2E Tests for Libre LMS

## Decision: Playwright with TypeScript (not .NET Playwright wrapper)

**Rationale**: The Playwright team maintains the TypeScript API as the primary interface. Using TypeScript directly gives access to the latest features, better IDE support, and the largest community. The .NET wrapper (`Microsoft.Playwright`) lags behind and has a smaller ecosystem.

**Alternatives considered**:
- `Microsoft.Playwright` (.NET wrapper) — rejected because it adds an abstraction layer over the same engine and has fewer examples
- Cypress — rejected because it requires an in-browser test runner; Playwright runs headlessly and is faster for CI
- Selenium — rejected because it's slower, less reliable with modern SPAs/HTMX, and requires more boilerplate

## Decision: Page Object Model (POM) pattern for test organization

**Rationale**: The LMS has many pages (Login, Browse Courses, Course Detail, My Courses, multiple Admin pages). POM encapsulates selectors and actions per page, making tests readable and reducing duplication when page structure changes.

**Alternatives considered**:
- Inline selectors in tests — rejected because changes to CSS classes or HTMX behavior would require updating every test
- Screenplay pattern — rejected as overkill for ~40 tests

## Decision: Global setup waits for app health via HTTP polling

**Rationale**: The app calls `EnsureDeleted()` + `Migrate()` + `Seed()` on every startup, which can take 15-30 seconds. A simple HTTP poll against the root URL (`/`) with retry ensures tests don't start before the database is ready.

**Alternatives considered**:
- Fixed `setTimeout` — rejected because startup time varies by machine
- Database ping — rejected because it requires knowing the connection string and adds complexity

## Decision: Cookie-based login matching the existing `LoginModel.OnPostAsync`

**Rationale**: The app uses cookie authentication via ASP.NET Core's `AddCookie`. Playwright can submit the login form (POST to `/Account/Login`) just like a real user, receiving the auth cookie automatically. No need to bypass auth or inject cookies manually.

**Alternatives considered**:
- Storage state (Playwright's `storageState` API) — could be used for faster auth by skipping login form, but the login form itself should be tested. Will use form submission for login tests and storage state for other tests.
- API token auth — not available; the app only supports cookies

## Decision: Separate test files per functional area

**Rationale**: Tests are organized by user flow (auth, courses, enrollment, admin, RBAC, responsive) so that:
- Each file can be run independently with `--grep`
- Failures are easy to triage by area
- New tests are added to the right file without reading unrelated tests

**Alternatives considered**:
- Single monolithic test file — rejected for maintainability
- Tests organized by page rather than flow — rejected because user flows span multiple pages

## Decision: Test against seeded data, not test-created data

**Rationale**: The app seeds known data on every startup (10 courses, 4 users, sample SCORM package). Tests use this predictable data. No need for API calls to create test fixtures — the seeders are the fixture factory.

**Alternatives considered**:
- Create test data via API before each test — rejected because the app drops and recreates the database on startup, so pre-seeded data is always fresh
- Use Testcontainers for a separate test database — rejected as overkill; the app already uses the devcontainer's MSSQL

## Decision: Default to Chromium, add Firefox/WebKit in CI config

**Rationale**: Chromium covers the majority of real users. Firefox and WebKit can be enabled in CI by setting `useWebKit` and `useFirefox` in the Playwright config. For local development, Chromium-only is faster.

**Alternatives considered**:
- All three browsers always — rejected because it triples test runtime for local development

## HTMX Partial Update Handling

**Finding**: The LMS uses HTMX extensively for partial-page updates (course search, enrollment list, pagination). Playwright sees these as DOM mutations, not navigation events. Tests must use `page.waitForSelector()` or `expect(locator).toBeVisible()` instead of `page.waitForNavigation()` for HTMX-driven updates.

**Decision**: Use Playwright's auto-waiting on locators (e.g., `await page.getByRole('button', { name: 'Enroll' }).click()` then `await expect(page.getByText('Enrolled')).toBeVisible()`) which handles HTMX updates naturally.

## App Port Configuration

**Finding**: The app runs inside a devcontainer. Kestrel's default port is 5000 for non-HTTPS. The `docker-compose.yml` does not expose the app port (only MSSQL 1433 and Valkey 6379 are exposed), so tests will run inside the container against `http://localhost:5000`.

**Decision**: Playwright config will target `http://localhost:5000` as the base URL. Tests must start the app with `dotnet run --project src/Host/Host.csproj` in the background before running tests.
