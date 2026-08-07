import { test, expect } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Authentication tests (User Story 1 — MVP smoke tests).
 *
 * Verifies login, invalid credentials, admin login, and logout flows.
 */
test.describe('Authentication', () => {
  test('successful login with learner credentials', async ({ page }) => {
    // Navigate to login page and sign in as alice (Learner)
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(testUsers.learner.email);
    await page.getByLabel('Password').fill(testUsers.learner.password);
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Verify redirect to courses page (app may redirect to /Courses or /Courses/Index)
    await expect(page).toHaveURL(/\/Courses(\/Index)?/);

    // Verify account name shows "Alice Johnson" in the account control
    await expect(page.locator('.account-name')).toContainText(testUsers.learner.name);
  });

  test('rejects invalid credentials', async ({ page }) => {
    // Navigate to login page and submit wrong password
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(testUsers.learner.email);
    await page.getByLabel('Password').fill('wrongpassword');
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Verify error message is displayed
    await expect(page.locator('.error-message')).toBeVisible();

    // Verify user stays on the login page
    await expect(page).toHaveURL(/\/Account\/Login/);
  });

  test('successful login with admin credentials', async ({ page }) => {
    // Navigate to login page and sign in as admin (OrgAdmin)
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(testUsers.orgAdmin.email);
    await page.getByLabel('Password').fill(testUsers.orgAdmin.password);
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Verify redirect to courses page
    await expect(page).toHaveURL(/\/Courses(\/Index)?/);

    // Verify admin nav links are visible
    // Admin links start hidden by default (role-learner is default);
    // switch to admin role to show them
    const adminSegment = page.locator('.role-segment[data-value="admin"]');
    if (await adminSegment.isVisible().catch(() => false)) {
      await adminSegment.click();
    }
    await expect(page.locator('.admin-link').first()).toBeVisible();
  });

  test('logout clears session', async ({ page }) => {
    // First log in as learner
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(testUsers.learner.email);
    await page.getByLabel('Password').fill(testUsers.learner.password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page).toHaveURL(/\/Courses(\/Index)?/);

    // Verify account control is visible before logout
    await expect(page.locator('#account-control')).toBeVisible();

    // Navigate to logout
    await page.goto('/Account/Logout');

    // Verify redirect to login page
    await expect(page).toHaveURL(/\/Account\/Login/);

    // Verify account control is not visible after logout
    await expect(page.locator('#account-control')).not.toBeVisible();
  });
});
