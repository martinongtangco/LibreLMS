import { test, expect, Page } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Admin Dashboard Smoke Tests (User Story 1).
 *
 * Verifies OrgAdmin can access the admin dashboard and that
 * seeded data produces non-zero metric values.
 */
test.describe('Admin Dashboard Smoke', () => {
  async function loginAsAdmin(page: Page) {
    const user = testUsers.orgAdmin;
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(user.email);
    await page.getByLabel('Password').fill(user.password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(
      (url) => url.pathname.includes('/Courses') || url.pathname === '/',
      { timeout: 10_000 }
    );
  }

  test('OrgAdmin can access dashboard', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/Admin/Dashboard/Index');

    // Verify the page loaded (h1 "Dashboard" is visible)
    await expect(page.locator('h1', { hasText: 'Dashboard' })).toBeVisible();

    // Verify metric cards are visible
    const metricCards = page.locator('.metric-card');
    await expect(metricCards).toHaveCount(4);

    // Verify each expected metric label is present
    const expectedLabels = ['Organizations', 'Learners', 'Courses', 'Enrollments'];
    for (const label of expectedLabels) {
      await expect(
        page.locator('.metric-label', { hasText: label })
      ).toBeVisible();
    }
  });

  test('dashboard shows metric values for seeded data', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/Admin/Dashboard/Index');

    // Verify metric values are present and are numeric
    const metricValues = page.locator('.metric-value');
    const count = await metricValues.count();

    expect(count).toBeGreaterThanOrEqual(4);

    // All metrics should be visible and contain a number (may be 0 for some scopes)
    for (let i = 0; i < count; i++) {
      const value = metricValues.nth(i);
      await expect(value).toBeVisible();
      const text = (await value.textContent())?.trim() ?? '';
      expect(text).toMatch(/^\d+$/);
    }
  });

  /**
   * bug-039 regression guard: the OrganizationId claim was dropped from the auth
   * cookie in the spec 027 rebuild, so OrgAdmin dashboards resolved no org scope
   * and rendered 0/0/0/0 with an empty Completion Rate and All Courses section.
   * These assertions require the seeded non-zero minimums (the dev DB is shared
   * with other suites, so values can only grow, never shrink below the seeds)
   * so a dropped claim can never ship green — the old "matches a number"
   * assertion accepted 0.
   */
  test('OrgAdmin dashboard shows seeded metrics, completion rate, and courses (bug-039)', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/Admin/Dashboard/Index');

    // Org-scope banner: the page must know which org it is scoping to.
    await expect(page.locator('p.text-muted', { hasText: 'Viewing metrics for' })).toBeVisible();

    const card = (label: string) =>
      page.locator('.metric-card', { hasText: label }).locator('.metric-value');

    // Every metric is numeric (Organizations counts child orgs only — any value
    // is valid, even 0 — but it must be a rendered number, not a missing branch).
    for (const label of ['Organizations', 'Learners', 'Courses', 'Enrollments']) {
      await expect(card(label)).toHaveText(/^\d+$/);
    }

    // Seeded data (EnrollmentSeeder/CatalogSeeder): 5 students, 10 courses, 1
    // enrollment, all in the root org (the OrgAdmin's org). These must be
    // non-zero — before the fix every card rendered 0.
    const metric = async (label: string) =>
      parseInt((await card(label).textContent())?.trim() ?? '0', 10);
    expect(await metric('Learners')).toBeGreaterThanOrEqual(5);
    expect(await metric('Courses')).toBeGreaterThanOrEqual(10);
    expect(await metric('Enrollments')).toBeGreaterThanOrEqual(1);

    // Completion Rate must render a percentage (a null model value renders blank).
    const completionRate = page
      .locator('h3', { hasText: 'Completion Rate' })
      .locator('xpath=following-sibling::p[1]');
    await expect(completionRate).not.toBeEmpty();
    await expect(completionRate).toHaveText(/\d+(\.\d+)?%$/);

    // All Courses table must list the seeded courses (empty before the fix).
    const courseRows = page.locator('.courses-table tbody tr');
    await expect(courseRows).toHaveCount(10);
  });

  /**
   * story 040 (L3): direct probe of the cookie's org-scope claim. GET
   * /api/dashboard 401s for an OrgAdmin whose cookie lacks the OrganizationId
   * claim (Program.cs cannot parse a missing org), so a 200 with
   * role=OrgAdmin and non-zero learner count proves the claim survived sign-in
   * end-to-end — through the real cookie, not a page render.
   */
  test('OrgAdmin cookie carries org-scope claim: /api/dashboard returns 200 (story 040)', async ({ page }) => {
    await loginAsAdmin(page);

    // page.request shares the browser context's cookies (unlike the standalone
    // `request` fixture, which gets the 302-to-login and masks the failure).
    const res = await page.request.get('/api/dashboard');
    expect(res.status()).toBe(200);

    const body = await res.json();
    expect(body.role).toBe('OrgAdmin');
    expect(body.metrics.organizationName).toBeTruthy();
    expect(body.metrics.learnerCount).toBeGreaterThanOrEqual(5);
  });
});
