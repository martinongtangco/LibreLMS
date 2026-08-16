import { test, expect, Page } from '@playwright/test';
import { CourseBrowsePage } from '../pages/CourseBrowsePage';
import { testUsers } from '../utils/testUsers';

/**
 * Bug 028: Course page pagination did nothing.
 *
 * Root cause: the HTMX handler parameter `int page = 1` had its binding source
 * inferred as Form (ASP.NET Core behavior for optional value-type parameters),
 * so the `page` query-string value from hx-get requests was never bound and the
 * handler always returned page 1. Secondary issue: boundary Previous/Next
 * buttons were disabled but still visible.
 *
 * Catalog state: the canonical seeded catalog (CatalogSeeder) has exactly 10
 * courses — a single page of 12 — which cannot exercise pagination. This spec
 * was originally written against a live DB that happened to hold 13 courses
 * (10 seeded + 3 accumulated test data), which made it brittle: it failed
 * whenever the catalog drifted back to the canonical 10 (and it collided with
 * 02-course-browse, which asserts the catalog is exactly the 10 seeded
 * courses).
 *
 * The spec is now self-contained: beforeAll creates 13 filler courses titled
 * "Pg028 Course NN" and afterAll deletes them. Every pagination test searches
 * for "Pg028" first, so it always sees exactly 13 results (page 1 of 2:
 * 12 + 1) regardless of what else is in the catalog and regardless of which
 * other specs run in parallel. Tests run unauthenticated — learners have no
 * org claim, so the whole catalog (including fillers) is visible to them.
 */

const PAGE_SIZE = 12;
const TOTAL_COURSES = 13;
const BASE = 'http://localhost:5000';
const ROOT_ORG = '00000000-0000-0000-0000-000000000001';
const FILLER_SEARCH = 'Pg028';

/** Zero-padded filler titles so title ordering is predictable. */
const fillerTitle = (n: number) => `Pg028 Course ${String(n).padStart(2, '0')}`;

test.describe.configure({ mode: 'serial' });

/** Sign in as SuperUser and return an API request context with its cookies. */
async function adminApi(playwright: { chromium: import('@playwright/test').Chromium }) {
  const browser = await playwright.chromium.launch();
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto(`${BASE}/Account/Login`);
  await page.getByLabel('Email').fill(testUsers.superUser.email);
  await page.getByLabel('Password').fill(testUsers.superUser.password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL('**/Courses**', { timeout: 15000 });
  return { browser, api: context.request };
}

/** Delete any stale fillers left behind by a previously interrupted run. */
async function deleteFillers(api: import('@playwright/test').APIRequestContext) {
  const { courses } = await (await api.get(`${BASE}/api/admin/courses`)).json();
  for (const c of courses) {
    if (typeof c.title === 'string' && c.title.startsWith(`${FILLER_SEARCH} Course`)) {
      await api.delete(`${BASE}/api/admin/courses/${c.courseId}`);
    }
  }
}

test.beforeAll(async ({ playwright }) => {
  const { browser, api } = await adminApi(playwright);
  try {
    await deleteFillers(api);
    for (let n = 1; n <= TOTAL_COURSES; n++) {
      const resp = await api.post(`${BASE}/api/courses`, {
        data: {
          title: fillerTitle(n),
          shortDescription: 'Pagination test filler (spec 028, removed after the run).',
          fullDescription:
            'Temporary filler course created by 11-course-pagination.spec.ts so the ' +
            '"Pg028" search yields exactly 13 results (two pages of 12 + 1). Deleted in afterAll.',
          category: 'Tools',
          duration: '1 hour',
          organizationId: ROOT_ORG,
        },
      });
      if (resp.status() !== 201) throw new Error(`Filler creation for ${fillerTitle(n)} returned ${resp.status()}`);
    }
  } finally {
    await browser.close();
  }
});

test.afterAll(async ({ playwright }) => {
  const { browser, api } = await adminApi(playwright);
  try {
    await deleteFillers(api);
  } finally {
    await browser.close();
  }
});

/**
 * Go to the browse page and narrow the listing to exactly the 13 fillers,
 * so the tests are independent of the rest of the catalog.
 */
async function gotoBrowse(page: Page) {
  await page.goto('/Courses/Index');
  const browsePage = new CourseBrowsePage(page);
  await expect(browsePage.courseList.locator('.card').first()).toBeVisible();
  await browsePage.searchFor(FILLER_SEARCH);
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
    // Narrow to a single seeded course (does not match the Pg028 fillers)
    await browse.searchFor('C#');

    expect(await browse.getCourseCount()).toBe(1);
    // A single result page has no pagination nav at all
    await expect(browse.nextButton).toHaveCount(0);
    await expect(browse.previousButton).toHaveCount(0);
  });
});
