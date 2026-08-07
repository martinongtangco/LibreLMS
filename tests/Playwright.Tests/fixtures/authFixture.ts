import { Page } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Auth fixture: provides authenticated browser context per role.
 *
 * Uses cookie-based login matching the existing LoginModel.OnPostAsync flow.
 * Each test file can call loginAs() to get an authenticated page instance.
 */
export interface AuthFixture {
  loginAs(page: Page, role: 'Learner' | 'OrgAdmin' | 'SuperUser'): Promise<void>;
  logout(page: Page): Promise<void>;
}

export const authFixture: AuthFixture = {
  /**
   * Log in as a specific role using seeded credentials.
   * Navigates to /Account/Login, fills credentials, submits, and waits for redirect.
   */
  async loginAs(page: Page, role: 'Learner' | 'OrgAdmin' | 'SuperUser'): Promise<void> {
    const user =
      role === 'SuperUser'
        ? testUsers.superUser
        : role === 'OrgAdmin'
          ? testUsers.orgAdmin
          : testUsers.learner;

    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(user.email);
    await page.getByLabel('Password').fill(user.password);
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Wait for redirect to home page (which redirects to Courses/Index for authenticated users)
    await page.waitForURL((url) => {
      return (
        url.pathname === '/' ||
        url.pathname === '/Courses/Index' ||
        url.pathname.includes('/Courses')
      );
    }, { timeout: 10_000 });
  },

  /**
   * Log out the current user.
   */
  async logout(page: Page): Promise<void> {
    await page.goto('/Account/Logout');
    await page.waitForURL((url) => {
      return url.pathname.includes('/Account/Login') || url.pathname === '/';
    }, { timeout: 10_000 });
  },
};
