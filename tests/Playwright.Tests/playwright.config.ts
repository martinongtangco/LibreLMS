import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for Libre LMS E2E tests.
 *
 * - Targets http://localhost:5000 (default Kestrel non-HTTPS port)
 * - Chromium-only for local dev; add Firefox/WebKit in CI
 * - globalSetup waits for the app to be healthy before tests begin
 * - Retains trace on first failure for debugging
 */
export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  expect: {
    timeout: 10_000,
  },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
  ],
  globalSetup: require.resolve('./global-setup'),

  use: {
    baseURL: 'http://localhost:5000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
