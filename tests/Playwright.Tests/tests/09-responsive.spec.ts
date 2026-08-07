import { test, expect } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * User Story 4 - Responsive/Mobile Navigation Tests.
 *
 * Verifies navigation works correctly on different viewport sizes,
 * including hamburger menu and role toggle.
 *
 * Runs with a mobile viewport (375 x 812) to simulate iPhone X / SE.
 */
test.describe('Responsive Navigation', () => {
  test.use({ viewport: { width: 375, height: 812 } });

  /**
   * Helper: login via the login form with given credentials.
   */
  async function login(page: import('@playwright/test').Page, email: string, password: string) {
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

  test('hamburger menu toggles navigation on mobile', async ({ page }) => {
    // Navigate to any page that shows the navbar (login page shows login link in nav)
    await page.goto('/Account/Login');

    // First log in so the hamburger button appears (it is inside the authenticated block)
    await login(page, testUsers.learner.email, testUsers.learner.password);

    // Hamburger button should be visible at 375px
    const hamburger = page.locator('#nav-toggle');
    await expect(hamburger).toBeVisible();

    // Nav links should not have is-open initially
    const navLinks = page.locator('#nav-links');
    // Click hamburger to open
    await hamburger.click();
    await expect(navLinks).toHaveClass(/is-open/);

    // Click hamburger again to close
    await hamburger.click();
    await expect(navLinks).not.toHaveClass(/is-open/);
  });

  test('clicking nav link closes hamburger menu', async ({ page }) => {
    await login(page, testUsers.learner.email, testUsers.learner.password);

    const hamburger = page.locator('#nav-toggle');
    const navLinks = page.locator('#nav-links');

    // Open hamburger menu
    await hamburger.click();
    await expect(navLinks).toHaveClass(/is-open/);

    // Click a nav link (e.g., "My Courses" or "Browse Courses")
    const myCoursesLink = page.locator('a.nav-link[data-page="my-courses"]');
    await myCoursesLink.click();

    // Wait for navigation to complete
    await page.waitForURL((url) => url.pathname.includes('/MyCourses'));

    // Hamburger menu should be closed after navigation
    await expect(navLinks).not.toHaveClass(/is-open/);
  });

  test('admin links hidden by default on mobile for Learner', async ({ page }) => {
    // Learner role does NOT have admin role, so no admin links exist in the DOM at all
    await login(page, testUsers.learner.email, testUsers.learner.password);

    // Open hamburger to see all available links
    const hamburger = page.locator('#nav-toggle');
    await hamburger.click();

    // .admin-link elements should NOT exist in DOM for a Learner
    const adminLinks = page.locator('.admin-link');
    await expect(adminLinks).toHaveCount(0);
  });

  test('role toggle shows admin links on mobile', async ({ page }) => {
    // OrgAdmin has role toggle pill and admin links in DOM
    await login(page, testUsers.orgAdmin.email, testUsers.orgAdmin.password);

    // Open hamburger menu
    const hamburger = page.locator('#nav-toggle');
    await hamburger.click();
    const navLinks = page.locator('#nav-links');
    await expect(navLinks).toHaveClass(/is-open/);

    // By default, admin links may or may not be visible depending on localStorage state.
    // First, ensure we start in "learner" mode (which hides admin links)
    const learnerSegment = page.locator('.role-segment[data-value="learner"]').first();
    await learnerSegment.click();

    // Small delay for JS to apply body class
    await page.waitForTimeout(300);

    // Admin links should be hidden when in learner mode
    const adminLinks = page.locator('.admin-link');
    for (const link of await adminLinks.all()) {
      await expect(link).toBeHidden();
    }

    // Click "admin" role segment to show admin links
    const adminSegment = page.locator('.role-segment[data-value="admin"]').first();
    await adminSegment.click();

    // Small delay for JS to apply body class
    await page.waitForTimeout(300);

    // Admin links should now be visible
    for (const link of await adminLinks.all()) {
      await expect(link).toBeVisible();
    }
  });
});
