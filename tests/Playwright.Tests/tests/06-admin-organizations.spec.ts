import { test, expect } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Admin Organizations: verify organization list and create form accessibility.
 *
 * Tests that OrgAdmin can view the seeded org tree and reach the
 * create-organization page.
 */
test.describe('Admin Organizations', () => {
  test.beforeEach(async ({ page }) => {
    // Log in as OrgAdmin before each test.
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(testUsers.orgAdmin.email);
    await page.getByLabel('Password').fill(testUsers.orgAdmin.password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => {
      return (
        url.pathname === '/' ||
        url.pathname.includes('/Courses') ||
        url.pathname.includes('/Courses')
      );
    }, { timeout: 10_000 });
  });

  test('organization list shows root org', async ({ page }) => {
    await page.goto('/Admin/Organizations/Index');

    // The page should render the seeded "Root Organization" in the org tree.
    await expect(page.getByText('Root Organization')).toBeVisible();
  });

  test('create organization form is accessible', async ({ page }) => {
    await page.goto('/Admin/Organizations/Index');

    // Click the "Create Organization" button on the index page.
    await page.getByRole('link', { name: 'Create Organization' }).click();

    // Should navigate to the create page and show the form fields.
    await expect(page.getByRole('heading', { name: 'Create Organization' })).toBeVisible();
    await expect(page.getByLabel('Name')).toBeVisible();
    await expect(page.getByLabel('Description')).toBeVisible();
  });
});
