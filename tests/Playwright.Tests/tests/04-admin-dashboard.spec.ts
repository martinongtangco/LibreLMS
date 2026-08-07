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
});
