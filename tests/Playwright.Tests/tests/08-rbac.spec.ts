import { test, expect } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Helper: log in via the Razor Pages login form.
 */
async function login(page: import('@playwright/test').Page, email: string, password: string): Promise<void> {
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

// ─── RBAC — Unauthenticated ──────────────────────────────────────

test.describe('RBAC — Unauthenticated', () => {
  test('unauthenticated user redirected to login for /Admin/Dashboard/Index', async ({ page }) => {
    await page.goto('/Admin/Dashboard/Index');
    expect(page.url()).toContain('/Account/Login');
  });

  test('unauthenticated user redirected to login for /Admin/Learners/Index', async ({ page }) => {
    await page.goto('/Admin/Learners/Index');
    expect(page.url()).toContain('/Account/Login');
  });

  test('unauthenticated user sees empty MyCourses (no redirect)', async ({ page }) => {
    // MyCourses/Index is accessible without auth (shows empty state)
    await page.goto('/MyCourses/Index');
    // Page loads without redirect to login
    expect(page.url()).not.toContain('/Account/Login');
    // Should show empty state or enrollment list
    const enrollmentList = page.locator('#enrollment-list');
    await expect(enrollmentList).toBeVisible();
  });
});

// ─── RBAC — Learner Access Denied ─────────────────────────────────

test.describe('RBAC — Learner Access Denied', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, testUsers.learner.email, testUsers.learner.password);
  });

  test('Learner cannot access /Admin/Dashboard/Index', async ({ page }) => {
    await page.goto('/Admin/Dashboard/Index');
    // Learner should be redirected to login or get 403
    const url = page.url();
    expect(url.includes('/Account/Login') || url.includes('/Error')).toBeTruthy();
  });

  test('Learner cannot access /Admin/Learners/Index', async ({ page }) => {
    await page.goto('/Admin/Learners/Index');
    const url = page.url();
    expect(url.includes('/Account/Login') || url.includes('/Error')).toBeTruthy();
  });

  test('Learner cannot access /Admin/Organizations/Index', async ({ page }) => {
    await page.goto('/Admin/Organizations/Index');
    const url = page.url();
    expect(url.includes('/Account/Login') || url.includes('/Error')).toBeTruthy();
  });

  test('Learner cannot access /Admin/Enrollments/Index', async ({ page }) => {
    await page.goto('/Admin/Enrollments/Index');
    const url = page.url();
    expect(url.includes('/Account/Login') || url.includes('/Error')).toBeTruthy();
  });

  test('Learner CAN access /Courses/Index and /MyCourses/Index', async ({ page }) => {
    await page.goto('/Courses/Index');
    await expect(page.locator('h1')).toContainText('Browse Courses');

    await page.goto('/MyCourses/Index');
    await expect(page.locator('h1')).toContainText('My Courses');
  });
});

// ─── RBAC — OrgAdmin Full Access ──────────────────────────────────

test.describe('RBAC — OrgAdmin Full Access', () => {
  test.beforeEach(async ({ page }) => {
    await login(page, testUsers.orgAdmin.email, testUsers.orgAdmin.password);
  });

  test('OrgAdmin can access /Admin/Dashboard/Index', async ({ page }) => {
    await page.goto('/Admin/Dashboard/Index');
    await expect(page.locator('h1')).toContainText('Dashboard');
  });

  test('OrgAdmin can access /Admin/Learners/Index', async ({ page }) => {
    await page.goto('/Admin/Learners/Index');
    await expect(page.locator('h1')).toContainText('Learner Management');
  });

  test('OrgAdmin can access /Admin/Organizations/Index', async ({ page }) => {
    await page.goto('/Admin/Organizations/Index');
    await expect(page.locator('h1')).toContainText('Organization Management');
  });

  test('OrgAdmin can access /Admin/Enrollments/Index', async ({ page }) => {
    await page.goto('/Admin/Enrollments/Index');
    await expect(page.locator('h1')).toContainText('Enrollment Management');
  });

  test('OrgAdmin can access /Admin/Courses/Index', async ({ page }) => {
    await page.goto('/Admin/Courses/Index');
    // Should not redirect to login
    expect(page.url()).not.toContain('/Account/Login');
  });

  test('OrgAdmin can access /Admin/Upload', async ({ page }) => {
    await page.goto('/Admin/Upload');
    expect(page.url()).not.toContain('/Account/Login');
  });
});

// ─── RBAC — SuperUser Full Access ─────────────────────────────────

test.describe('RBAC — SuperUser Full Access', () => {
  const adminPaths = [
    '/Admin/Dashboard/Index',
    '/Admin/Learners/Index',
    '/Admin/Organizations/Index',
    '/Admin/Enrollments/Index',
    '/Admin/Courses/Index',
    '/Admin/Upload',
  ];

  test.beforeEach(async ({ page }) => {
    await login(page, testUsers.superUser.email, testUsers.superUser.password);
  });

  test('SuperUser can access all admin pages', async ({ page }) => {
    for (const path of adminPaths) {
      await page.goto(path);
      const url = page.url();
      expect(
        url.includes('/Account/Login'),
        `SuperUser should not be redirected to login for ${path}`
      ).toBeFalsy();
    }
  });
});
