# Quickstart: Run Playwright E2E Tests

## Prerequisites

1. **Devcontainer running**: Ensure you're inside the `.devcontainer` with MSSQL and Valkey running
   ```bash
   docker compose up -d mssql valkey
   ```

2. **Node.js installed**: The devcontainer includes Node.js. Verify:
   ```bash
   node --version
   npm --version
   ```

3. **Playwright browsers installed** (one-time setup):
   ```bash
   cd tests/Playwright.Tests
   npx playwright install --with-deps chromium
   ```

## Start the Application

In one terminal, start the LMS app:

```bash
cd /workspace
dotnet run --project src/Host/Host.csproj
```

Wait for the startup logs to show `Now listening on: http://localhost:5000`.

## Run the Tests

In a second terminal:

```bash
cd /workspace/tests/Playwright.Tests

# Run all tests (headless)
npx playwright test

# Run with UI (headed, interactive)
npx playwright test --headed

# Run a specific test file
npx playwright test tests/01-auth.spec.ts

# Run tests matching a pattern
npx playwright test --grep "RBAC"

# Generate HTML report
npx playwright test --reporter=html
npx playwright show-report
```

## Expected Outcomes

### Passing Suite
All tests should pass with output like:
```
Running 35 tests using 4 workers

  ✓  1 tests/01-auth.spec.ts > login flow > successful login with learner credentials
  ✓  2 tests/01-auth.spec.ts > login flow > rejects invalid credentials
  ✓  3 tests/02-course-browse.spec.ts > course browse > displays all seeded courses
  ...

35 passed (45s)
```

### Failing Tests
If tests fail:
1. Check if the app is running: `curl http://localhost:5000`
2. Check if seeded data exists: look for "Now listening" in the app logs
3. Run a single test in headed mode: `npx playwright test --headed tests/01-auth.spec.ts`
4. Check Playwright trace: `npx playwright show-trace trace.zip`

## Validation Scenarios

### Scenario 1: Login as Learner
1. Navigate to `http://localhost:5000/Account/Login`
2. Enter `alice@example.com` / `password123`
3. Click "Sign In"
4. **Expected**: Redirected to `/Courses/Index`, nav bar shows "Alice Johnson"

### Scenario 2: Browse and Search Courses
1. As a logged-in user, go to `/Courses/Index`
2. Type "C#" in the search box
3. **Expected**: Only "Introduction to C#" appears in results

### Scenario 3: Admin Dashboard
1. Navigate to `http://localhost:5000/Account/Login`
2. Enter `admin@example.com` / `password123`
3. Click "Sign In"
4. Navigate to `/Admin/Dashboard/Index`
5. **Expected**: Dashboard shows metrics (Organizations, Learners, Courses, Enrollments)

### Scenario 4: RBAC — Learner Cannot Access Admin
1. Log in as `alice@example.com` / `password123`
2. Navigate to `/Admin/Dashboard/Index` directly
3. **Expected**: Denied access (403 or redirect to login)

## CI Integration

For CI pipelines, use:
```bash
cd tests/Playwright.Tests
npx playwright install --with-deps chromium
npx playwright test --reporter=line --workers=4
```

The `global-setup.ts` will wait for the app to be healthy before running tests.
