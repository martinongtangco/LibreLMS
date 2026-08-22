import { test, expect, Page } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Admin Learners tests (Phase 7).
 *
 * Verifies the Admin Learners page shows seeded users and
 * that the create learner form is accessible.
 */
test.describe('Admin Learners', () => {
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

    // Switch to admin role to show admin nav links
    const adminSegment = page.locator('.role-segment[data-value="admin"]');
    if (await adminSegment.isVisible().catch(() => false)) {
      await adminSegment.click();
    }
  }

  test('learner list shows seeded users', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/Admin/Learners/Index');

    // Verify the page loaded (h1 "Learner Management" is visible)
    await expect(page.locator('h1', { hasText: 'Learner Management' })).toBeVisible();

    // Verify the data table is present
    await expect(page.locator('.data-table')).toBeVisible();

    // Verify seeded learners appear in the table body
    const tbody = page.locator('.data-table tbody');
    expect(await tbody.locator('tr').count()).toBeGreaterThanOrEqual(3);

    // Verify each seeded learner name appears. Spec 032: the list is
    // paginated (10 per page), so search each name to surface it regardless
    // of page position (the page 1 row count above already proves listing).
    const seededNames = ['Alice Johnson', 'Bob Smith', 'Carol Davis'];
    for (const name of seededNames) {
      await page.getByPlaceholder('Search by name or email...').fill(name);
      await page.getByRole('button', { name: 'Filter' }).click();
      await page.waitForLoadState('networkidle');
      await expect(page.locator('.data-table tbody').getByText(name)).toBeVisible();
    }
  });

  test('create learner form is accessible', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/Admin/Learners/Index');

    // Click the "Create Learner" button
    await page.getByRole('link', { name: 'Create Learner' }).click();

    // Verify create page loaded
    await expect(page).toHaveURL(/\/Admin\/Learners\/Create/);

    // Verify the create form elements are present
    await expect(page.locator('h1', { hasText: 'Create Learner' })).toBeVisible();
    await expect(page.getByLabel('Name')).toBeVisible();
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password')).toBeVisible();
    await expect(page.getByLabel('Organization')).toBeVisible();
    await expect(page.getByLabel('Role')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Create' })).toBeVisible();
  });
});
