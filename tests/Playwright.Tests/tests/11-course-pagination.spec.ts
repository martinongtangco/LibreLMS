import { test, expect, Page } from '@playwright/test';
import { CourseBrowsePage } from '../pages/CourseBrowsePage';

/**
 * Bug 028: Course page pagination did nothing.
 *
 * Root cause: the HTMX handler parameter `int page = 1` had its binding source
 * inferred as Form (ASP.NET Core behavior for optional value-type parameters),
 * so the `page` query-string value from hx-get requests was never bound and the
 * handler always returned page 1. Secondary issue: boundary Previous/Next
 * buttons were disabled but still visible.
 *
 * Seeded catalog: 13 courses, page size 12 → page 1 has 12 cards, page 2 has 1.
 * Tests run unauthenticated so the org-visibility filter doesn't affect counts.
 */

const PAGE_SIZE = 12;
const TOTAL_COURSES = 13;

async function gotoBrowse(page: Page) {
  await page.goto('/Courses/Index');
  const browsePage = new CourseBrowsePage(page);
  await expect(browsePage.courseList.locator('.card').first()).toBeVisible();
  return browsePage;
}

// ────────────────────────────────────────────────────────────────────
// US1: Next/Previous actually change the page
// ────────────────────────────────────────────────────────────────────
test.describe('Course Pagination — page navigation (bug 028)', () => {
  test('page 1 shows first 12 of 13 courses', async ({ page }) => {
    const browse = await gotoBrowse(page);

    expect(await browse.getCourseCount()).toBe(PAGE_SIZE);
    await expect(browse.pageIndicator).toContainText(`Page 1 of 2 (${TOTAL_COURSES} total)`);
  });

  test('next on page 1 reveals the 13th course on page 2', async ({ page }) => {
    const browse = await gotoBrowse(page);
    const pageOneTitles = await browse.getCourseTitles();
    expect(pageOneTitles).toHaveLength(PAGE_SIZE);

    await browse.clickNext();

    const pageTwoTitles = await browse.getCourseTitles();
    expect(pageTwoTitles).toHaveLength(TOTAL_COURSES - PAGE_SIZE);
    await expect(browse.pageIndicator).toContainText(`Page 2 of 2 (${TOTAL_COURSES} total)`);

    // The page-2 course must be one that was NOT on page 1
    expect(pageTwoTitles[0]).not.toBe(pageOneTitles[0]);
  });

  test('previous on the last page returns to page 1', async ({ page }) => {
    const browse = await gotoBrowse(page);
    const pageOneTitles = await browse.getCourseTitles();

    await browse.clickNext();
    await expect(browse.pageIndicator).toContainText('Page 2 of 2');

    await browse.clickPrevious();

    expect(await browse.getCourseCount()).toBe(PAGE_SIZE);
    await expect(browse.pageIndicator).toContainText(`Page 1 of 2 (${TOTAL_COURSES} total)`);
    expect(await browse.getCourseTitles()).toEqual(pageOneTitles);
  });

  test('no course is duplicated or missing across pages', async ({ page }) => {
    const browse = await gotoBrowse(page);
    const pageOneTitles = await browse.getCourseTitles();

    await browse.clickNext();
    const pageTwoTitles = await browse.getCourseTitles();

    const all = [...pageOneTitles, ...pageTwoTitles];
    expect(all).toHaveLength(TOTAL_COURSES);
    expect(new Set(all).size).toBe(TOTAL_COURSES);
  });
});

// ────────────────────────────────────────────────────────────────────
// US2: Boundary buttons are hidden, not just disabled
// ────────────────────────────────────────────────────────────────────
test.describe('Course Pagination — boundary visibility (bug 028)', () => {
  test('previous button is not rendered on the first page', async ({ page }) => {
    const browse = await gotoBrowse(page);

    // Absent from the DOM entirely — not merely disabled
    await expect(browse.previousButton).toHaveCount(0);
    await expect(browse.nextButton).toHaveCount(1);
  });

  test('next button is not rendered on the last page', async ({ page }) => {
    const browse = await gotoBrowse(page);
    await browse.clickNext();

    await expect(browse.nextButton).toHaveCount(0);
    await expect(browse.previousButton).toHaveCount(1);
  });

  test('search still filters and shows single-page results without a next button', async ({ page }) => {
    const browse = await gotoBrowse(page);
    await browse.searchFor('C#');

    expect(await browse.getCourseCount()).toBe(1);
    // A single result page has no pagination nav at all
    await expect(browse.nextButton).toHaveCount(0);
    await expect(browse.previousButton).toHaveCount(0);
  });
});
