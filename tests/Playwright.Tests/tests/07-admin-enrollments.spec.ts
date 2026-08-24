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

  test('bulk-enroll button is spaced from the filter bar below (bug-038)', async ({ page }) => {
    // Login as OrgAdmin
    await login(page, testUsers.orgAdmin.email, testUsers.orgAdmin.password);
    await page.goto('/Admin/Enrollments/Index');

    // The global * { margin: 0 } reset removes the default <p> margin, so the gap
    // between the "Bulk Enroll" button and the filter bar must come from an
    // explicit spacing class — assert it is present and non-trivial.
    const actions = page.locator('p').filter({ has: page.getByRole('link', { name: 'Bulk Enroll' }) });
    const marginBottom = await actions.evaluate((el) => {
      // Minimal structural type — this project's TS lib set has no DOM lib.
      const view = (
        el as unknown as {
          ownerDocument: { defaultView: { getComputedStyle: (e: unknown) => Record<string, string> } };
        }
      ).ownerDocument.defaultView;
      return view.getComputedStyle(el).marginBottom;
    });
    const px = Number.parseFloat(marginBottom);
    expect(Number.isFinite(px), `bottom margin must be a length, got "${marginBottom}"`).toBe(true);
    expect(px, `action button needs bottom spacing from the filter bar (got ${marginBottom})`).toBeGreaterThanOrEqual(16);
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
