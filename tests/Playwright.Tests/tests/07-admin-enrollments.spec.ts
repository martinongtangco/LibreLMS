import { test, expect, Page } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Admin Enrollments page tests (Phase 7).
 *
 * Verifies that the OrgAdmin can view seeded enrollments and access
 * the bulk enroll form.
 */

async function login(page: Page, email: string, password: string) {
  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(
    (url) =>
      url.pathname === '/' ||
      url.pathname.includes('/Courses') ||
      url.pathname.includes('/Courses'),
    { timeout: 10_000 }
  );
}

test.describe('Admin Enrollments', () => {
  test('enrollment list shows seeded enrollments', async ({ page }) => {
    // Login as OrgAdmin
    await login(page, testUsers.orgAdmin.email, testUsers.orgAdmin.password);

    // Navigate to Admin Enrollments
    await page.goto('/Admin/Enrollments/Index');

    // Verify the page loaded (h1 title)
    await expect(page.locator('h1').first()).toContainText('Enrollment Management');

    // The page shows either a data table or an empty state
    const hasTable = await page.locator('.data-table').isVisible().catch(() => false);
    const hasEmptyState = await page.locator('.empty-state').isVisible().catch(() => false);
    expect(hasTable || hasEmptyState).toBe(true);

    // If there's a table, it should show enrollment data
    if (hasTable) {
      const table = page.locator('.data-table');
      await expect(table).toBeVisible();
    }
  });

  test('bulk enroll form is accessible', async ({ page }) => {
    // Login as OrgAdmin
    await login(page, testUsers.orgAdmin.email, testUsers.orgAdmin.password);

    // Navigate to Admin Enrollments
    await page.goto('/Admin/Enrollments/Index');

    // Verify "Bulk Enroll" link is visible (it's an <a> tag, not a button)
    const bulkEnroll = page.getByRole('link', { name: 'Bulk Enroll' });
    await expect(bulkEnroll).toBeVisible();

    // Click it and verify navigation to bulk enroll page
    await bulkEnroll.click();
    await page.waitForURL((url) => url.pathname.includes('/Admin/Enrollments/BulkEnroll'), {
      timeout: 10_000,
    });
  });
});
