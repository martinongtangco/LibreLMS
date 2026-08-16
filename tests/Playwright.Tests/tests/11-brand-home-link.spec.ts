import { test, expect, Page } from '@playwright/test';
import { testUsers } from '../utils/testUsers';
import { CourseBrowsePage } from '../pages/CourseBrowsePage';

/**
 * Spec 029: Clickable Brand Link to Home.
 *
 * The "Libre LMS" navbar brand (`.navbar .brand`) must be a link to the site
 * root on EVERY page — for signed-out and signed-in users, all roles, desktop
 * and mobile — so no page (most critically the Login page) is a navigation
 * dead end. The root URL already 302-redirects to /Courses (Browse Courses),
 * so a brand click lands the user on Browse Courses.
 *
 * Behavior contract: specs/029-clickable-home-brand/contracts/brand-link.md
 */

const brand = (page: Page) => page.locator('.navbar .brand');

/**
 * Sign in with a test user via the login form and wait for the redirect.
 */
async function signIn(page: Page, user: { email: string; password: string }) {
  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill(user.email);
  await page.getByLabel('Password').fill(user.password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(
    (url) => url.pathname === '/' || url.pathname.includes('/Courses'),
    { timeout: 10_000 }
  );
}

/**
 * Assert the page is Browse Courses: final URL is /Courses and the course
 * listing is visible.
 */
async function expectBrowseCourses(page: Page) {
  await expect(page).toHaveURL(/\/Courses(\/Index)?\/?$/, { timeout: 10_000 });
  const browse = new CourseBrowsePage(page);
  await expect(browse.courseList.locator('.card').first()).toBeVisible();
}

// ────────────────────────────────────────────────────────────────────
// US1: Escape the Login Page (P1, MVP)
// ────────────────────────────────────────────────────────────────────
test.describe('Brand link — signed-out user (US1)', () => {
  test('signed-out user on Login page: brand is a link to Home', async ({ page }) => {
    await page.goto('/Account/Login');
    await expect(page.getByRole('heading', { name: 'Login', level: 1 })).toBeVisible();

    // The brand is an <a> targeting the site root
    const brandEl = brand(page);
    await expect(brandEl).toHaveCount(1);
    expect(await brandEl.evaluate((el) => el.tagName)).toBe('A');
    await expect(brandEl).toHaveAttribute('href', '/');

    // One click reaches Browse Courses — no sign-in prompt
    await brandEl.click();
    await expectBrowseCourses(page);
    await expect(page.getByLabel('Email')).toHaveCount(0);
  });

  test('brand on Home is idempotent', async ({ page }) => {
    await page.goto('/Courses');
    await expectBrowseCourses(page);

    // Clicking the brand while already on Home just (re)loads Browse Courses
    await brand(page).click();
    await expectBrowseCourses(page);
    // No error/denial page and no redirect loop
    await expect(page.getByRole('heading', { name: 'Login', level: 1 })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Access denied', level: 1 })).toHaveCount(0);
  });
});

// ────────────────────────────────────────────────────────────────────
// US2: One-Click Return to Home from Any Page (P1)
// ────────────────────────────────────────────────────────────────────
test.describe('Brand link — signed-in users (US2)', () => {
  test('signed-in learner on My Courses: brand click lands on Browse Courses', async ({ page }) => {
    await signIn(page, testUsers.learner);
    await page.goto('/MyCourses/Index');
    await expect(page.getByRole('heading', { name: 'My Courses', level: 1 })).toBeVisible();

    await brand(page).click();
    await expectBrowseCourses(page);

    // Signed-in state preserved — account name still visible in the navbar
    await expect(page.locator('.navbar .account-name')).toHaveText(testUsers.learner.name);
  });

  test('signed-in admin (Admin role view) on admin Dashboard: brand click lands on Browse Courses, NOT the admin Dashboard', async ({ page }) => {
    await signIn(page, testUsers.orgAdmin);

    // Set the Admin role view explicitly (deterministic, independent of localStorage)
    await page.locator('#role-pill .role-segment[data-value="admin"]').click();

    await page.goto('/Admin/Dashboard/Index');
    await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeVisible();

    await brand(page).click();
    await expectBrowseCourses(page);

    // Brand is not role-aware: the admin Dashboard must NOT be shown
    await expect(page.getByRole('heading', { name: 'Dashboard', level: 1 })).toHaveCount(0);
  });
});

// ────────────────────────────────────────────────────────────────────
// US3: Home Is Browse Courses by Default (P2)
// ────────────────────────────────────────────────────────────────────
test.describe('Root URL shows Browse Courses (US3)', () => {
  test('anonymous visitor: root URL shows Browse Courses', async ({ page }) => {
    await page.goto('/');
    await expectBrowseCourses(page);
  });

  test('signed-in user: root URL shows Browse Courses', async ({ page }) => {
    await signIn(page, testUsers.learner);
    await page.goto('/');
    await expectBrowseCourses(page);
  });
});

// ────────────────────────────────────────────────────────────────────
// Edge cases (Phase 5)
// ────────────────────────────────────────────────────────────────────
test.describe('Brand link — edge cases', () => {
  test('access-denied login variant: brand present and navigates to Home', async ({ page }) => {
    // A learner hitting an admin-only URL is bounced to the Login page in its
    // "Access denied" variant — the brand must still offer the escape to Home.
    await signIn(page, testUsers.learner);
    await page.goto('/Admin/Dashboard/Index');

    await expect(page).toHaveURL(/\/Account\/Login/);
    await expect(page.getByRole('heading', { name: 'Access denied', level: 1 })).toBeVisible();

    const brandEl = brand(page);
    await expect(brandEl).toHaveCount(1);
    await expect(brandEl).toHaveAttribute('href', '/');

    await brandEl.click();
    await expectBrowseCourses(page);
  });
});

test.describe('Brand link — mobile 375px', () => {
  test.use({ viewport: { width: 375, height: 812 } });

  test('mobile 375px: brand visible and clickable on Login page (signed out)', async ({ page }) => {
    await page.goto('/Account/Login');

    // Brand stays visible in the collapsed (hamburger) navbar
    const brandEl = brand(page);
    await expect(brandEl).toBeVisible();

    await brandEl.click();
    await expectBrowseCourses(page);
  });

  test('mobile: hamburger open, brand click resets nav state', async ({ page }) => {
    await signIn(page, testUsers.learner);

    // Open the hamburger menu
    const hamburger = page.locator('#nav-toggle');
    const navLinks = page.locator('#nav-links');
    await expect(hamburger).toBeVisible();
    await hamburger.click();
    await expect(navLinks).toHaveClass(/is-open/);

    // Brand click is a full page navigation — nav state cannot survive it
    await brand(page).click();
    await expectBrowseCourses(page);
    await expect(navLinks).not.toHaveClass(/is-open/);
  });
});
