import { test, expect, Page } from '@playwright/test';
import { CourseBrowsePage } from '../pages/CourseBrowsePage';
import { CourseDetailPage } from '../pages/CourseDetailPage';
import { testUsers } from '../utils/testUsers';

/**
 * Helper: log in as a user and navigate to the browse page.
 */
async function loginAndBrowse(page: Page, email: string, password: string) {
  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  // Wait for redirect after login
  await page.waitForURL(
    (url) =>
      url.pathname === '/' ||
      url.pathname.includes('/Courses') ||
      url.pathname.includes('/Courses'),
    { timeout: 10_000 }
  );
  await page.goto('/Courses/Index');
}

// ────────────────────────────────────────────────────────────────────
// US1: Course Browse Smoke Tests
// ────────────────────────────────────────────────────────────────────
test.describe('Course Browse Smoke', () => {
  test('browse page loads and shows seeded courses', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    await expect(page.locator('h1')).toContainText('Browse Courses');

    // Verify at least 10 course cards are visible
    const courseCount = await browsePage.getCourseCount();
    expect(courseCount).toBeGreaterThanOrEqual(10);
  });

  test('click course card navigates to detail page', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    // Click the first course card's title link
    const firstCard = page.locator('#course-list .card').first();
    await firstCard.locator('h3 a').click();

    // Verify we're on the detail page
    const detailPage = new CourseDetailPage(page);
    expect(await detailPage.isOnDetailPage()).toBeTruthy();
    await expect(detailPage.courseTitle).toBeVisible();
  });

  test('unauthenticated user can browse courses', async ({ page }) => {
    // Fresh (logged-out) context — just navigate directly
    await page.goto('/Courses/Index');

    await expect(page.locator('h1')).toContainText('Browse Courses');

    // Course cards should be visible without login
    const cards = page.locator('#course-list .card');
    await expect(cards.first()).toBeVisible();
  });
});

// ────────────────────────────────────────────────────────────────────
// US3: Course Search
// ────────────────────────────────────────────────────────────────────
test.describe('Course Search', () => {
  test('search by keyword filters results', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    await browsePage.searchFor('C#');

    const titles = await browsePage.getCourseTitles();
    // Only "Introduction to C#" should appear
    expect(titles).toHaveLength(1);
    expect(titles[0]).toContain('C#');
  });

  test('clear button resets search', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    // First filter to a single result
    await browsePage.searchFor('C#');
    let count = await browsePage.getCourseCount();
    expect(count).toBe(1);

    // Click Clear to reset
    await browsePage.clearFilters();
    count = await browsePage.getCourseCount();
    expect(count).toBeGreaterThanOrEqual(10);
  });

  test('search with no results shows empty state', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    await browsePage.searchFor('xyznonexistent');

    // No course result cards (empty-state cards don't count as results)
    const resultCards = page.locator('#course-list .card:not(.empty-state)');
    expect(await resultCards.count()).toBe(0);

    // Empty state should be shown
    await expect(page.getByText('No courses match your search')).toBeVisible();
  });
});

// ────────────────────────────────────────────────────────────────────
// US3: Course Category Filter
// ────────────────────────────────────────────────────────────────────
test.describe('Course Category Filter', () => {
  test('selecting Programming category shows 4 courses', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    await browsePage.selectCategory('Programming');

    const count = await browsePage.getCourseCount();
    expect(count).toBe(4);
  });

  test('selecting Design category shows 2 courses', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    await browsePage.selectCategory('Design');

    const count = await browsePage.getCourseCount();
    expect(count).toBe(2);
  });

  test('selecting Database category shows 2 courses', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    await browsePage.selectCategory('Database');

    const count = await browsePage.getCourseCount();
    expect(count).toBe(2);
  });

  test('selecting Tools category shows 2 courses', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    await browsePage.selectCategory('Tools');

    const count = await browsePage.getCourseCount();
    expect(count).toBe(2);
  });

  test('selecting All Categories shows all 10 courses', async ({ page }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    // First narrow to a category
    await browsePage.selectCategory('Design');
    expect(await browsePage.getCourseCount()).toBe(2);

    // Then reset to all
    await browsePage.selectCategory('All Categories');
    const count = await browsePage.getCourseCount();
    expect(count).toBeGreaterThanOrEqual(10);
  });
});

// ────────────────────────────────────────────────────────────────────
// US3: Course Detail Navigation
// ────────────────────────────────────────────────────────────────────
test.describe('Course Detail Navigation', () => {
  test('clicking a course card loads detail page with correct info', async ({
    page,
  }) => {
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    // Click the "Introduction to C#" course card
    const cSharpCard = browsePage.courseCard('Introduction to C#');
    await cSharpCard.locator('h3 a').click();

    const detailPage = new CourseDetailPage(page);
    expect(await detailPage.isOnDetailPage()).toBeTruthy();

    const title = await detailPage.getCourseTitle();
    expect(title).toBe('Introduction to C#');

    // Description should contain "C#"
    const description = await detailPage.courseDescription.innerText();
    expect(description.toLowerCase()).toContain('c#');
  });

  test('SCORM course detail shows launch option', async ({ page }) => {
    // Login as Alice (who is enrolled in Introduction to C#, the SCORM course)
    await loginAndBrowse(page, testUsers.learner.email, testUsers.learner.password);

    const browsePage = new CourseBrowsePage(page);
    const cSharpCard = browsePage.courseCard('Introduction to C#');
    await cSharpCard.locator('h3 a').click();

    const detailPage = new CourseDetailPage(page);

    // The SCORM launch button should be visible for an enrolled SCORM course
    await expect(detailPage.launchScormButton).toBeVisible();
  });
});
