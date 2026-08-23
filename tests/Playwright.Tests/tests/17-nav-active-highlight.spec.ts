import { test, expect, type Page, type APIRequestContext, type Browser } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Nav active-link highlight (spec 034).
 *
 * The layout's active-link IIFE used substring matching: '/Courses/Index' is a
 * substring of '/Admin/Courses/Index', so the admin Courses page highlighted
 * "Browse Courses" instead of "Courses" (and the loop broke before the correct
 * key was ever checked). The fix matches section paths (exact or section + '/'),
 * longest section wins.
 *
 * Self-contained per the 16-admin-pagination pattern: beforeAll creates
 * marker-prefixed filler courses (11 > default page size 10, so the admin
 * Courses list spans two pages) through the admin API; afterAll deletes them.
 */

const ROOT_ORG = '00000000-0000-0000-0000-000000000001';
const MARKER = 'NavHi034';
const FILLER_COUNT = 11; // > default page size (10) so "Next" renders

const pad2 = (n: number) => String(n).padStart(2, '0');
const fillerTitle = (n: number) => `${MARKER} T${pad2(n)}`;

let fillerCourseIds: string[] = [];
let api: APIRequestContext;
let apiBrowser: Browser;

test.describe.configure({ mode: 'serial' });

/** UI login as SuperUser (pattern: 05-admin-learners.spec.ts). */
async function loginAsSuperUser(page: Page) {
  const user = testUsers.superUser;
  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill(user.email);
  await page.getByLabel('Password').fill(user.password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL((url) => url.pathname.includes('/Courses') || url.pathname === '/', {
    timeout: 10_000,
  });
}

/**
 * Assert exactly one nav link is active and it is the given data-page.
 */
async function expectActiveOnly(page: Page, dataPage: string) {
  const active = page.locator('a.nav-link.active');
  await expect(active).toHaveCount(1);
  await expect(active.first()).toHaveAttribute('data-page', dataPage);
}

/**
 * Sign in as SuperUser in a throwaway browser and return the API request
 * context with its auth cookies (pattern: 16-admin-pagination.spec.ts).
 */
async function adminApiContext(playwright: { chromium: import('@playwright/test').Chromium }) {
  const browser: Browser = await playwright.chromium.launch();
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto('http://localhost:5000/Account/Login');
  await page.getByLabel('Email').fill(testUsers.superUser.email);
  await page.getByLabel('Password').fill(testUsers.superUser.password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL('**/Courses**', { timeout: 15000 });
  return { browser, api: context.request };
}

async function deleteFillers(ctx: APIRequestContext) {
  const coursesResp = await ctx.get('/api/admin/courses');
  if (!coursesResp.ok()) return;
  const { courses } = await coursesResp.json();
  for (const c of courses) {
    if (typeof c.title === 'string' && c.title.startsWith(MARKER)) {
      await ctx.delete(`/api/admin/courses/${c.courseId}`);
    }
  }
}

test.beforeAll(async ({ playwright }) => {
  const started = await adminApiContext(playwright);
  apiBrowser = started.browser;
  api = started.api;

  await deleteFillers(api);
  fillerCourseIds = [];
  for (let n = 1; n <= FILLER_COUNT; n++) {
    const resp = await api.post('/api/courses', {
      data: {
        title: fillerTitle(n),
        shortDescription: 'Nav highlight test filler (spec 034, removed after the run).',
        fullDescription:
          'Temporary filler course created by 17-nav-active-highlight.spec.ts. Deleted in afterAll.',
        category: 'Tools',
        duration: '1 hour',
        organizationId: ROOT_ORG,
      },
    });
    if (resp.status() !== 201) throw new Error(`Filler course ${fillerTitle(n)}: HTTP ${resp.status()}`);
    const location = resp.headers()['location'] ?? '';
    fillerCourseIds.push(location.split('/').pop() ?? '');
  }
});

test.afterAll(async () => {
  if (api) await deleteFillers(api);
  if (apiBrowser) await apiBrowser.close();
});

// ── Story 1: Admin Courses page highlights "Courses" (P1) ────────────────────

test('page 1: admin Courses is highlighted, Browse Courses is not (SC-001)', async ({ page }) => {
  await loginAsSuperUser(page);
  await page.goto('/Admin/Courses/Index');
  await expect(page.locator('h1', { hasText: 'Course Management' })).toBeVisible();
  await expectActiveOnly(page, 'admin-courses');
});

// ── Story 2: Highlight persists across pagination (P1) ───────────────────────

test('page 2 (after Next): highlight stays on admin Courses (SC-002)', async ({ page }) => {
  await loginAsSuperUser(page);
  await page.goto('/Admin/Courses/Index');
  await expect(page.locator('h1', { hasText: 'Course Management' })).toBeVisible();

  const next = page.locator('nav.admin-pagination a').filter({ hasText: 'Next' });
  await expect(next).toBeVisible(); // 11 filler courses guarantee > 1 page
  await next.click();
  await expect(page).toHaveURL(/pageNumber=2/);
  await expectActiveOnly(page, 'admin-courses');
});

test('pagination query does not move the highlight on other admin lists (FR-004)', async ({ page }) => {
  await loginAsSuperUser(page);
  await page.goto('/Admin/Enrollments/Index?pageNumber=2');
  await expectActiveOnly(page, 'admin-enrollments');
  await page.goto('/Admin/Learners/Index?pageNumber=2');
  await expectActiveOnly(page, 'admin-learners');
});

// ── Story 3: Subpages keep their section highlighted (P2) ────────────────────

test('subpages keep their section highlighted (SC-003)', async ({ page }) => {
  await loginAsSuperUser(page);
  const courseId = fillerCourseIds[0];
  expect(courseId).toBeTruthy();

  // Learner section subpage: course detail (route value: @page "{id:guid}")
  await page.goto(`/Courses/Detail/${courseId}`);
  await expect(page.locator('h1')).toBeVisible();
  await expectActiveOnly(page, 'browse-courses');

  // Admin section subpage: course edit
  await page.goto(`/Admin/Courses/Edit?courseId=${courseId}`);
  await expectActiveOnly(page, 'admin-courses');
});

test('every nav section page highlights exactly its own link (SC-003)', async ({ page }) => {
  await loginAsSuperUser(page);
  const cases: Array<[url: string, dataPage: string]> = [
    ['/MyCourses/Index', 'my-courses'],
    ['/Courses/Index', 'browse-courses'],
    ['/Admin/Dashboard/Index', 'dashboard'],
    ['/Admin/Courses/Index', 'admin-courses'],
    ['/Admin/Enrollments/Index', 'admin-enrollments'],
    ['/Admin/Learners/Index', 'admin-learners'],
    ['/Admin/Organizations/Index', 'admin-orgs'],
    ['/Admin/Upload', 'admin-upload'],
  ];
  for (const [url, dataPage] of cases) {
    await page.goto(url);
    await expectActiveOnly(page, dataPage);
  }
});

test('pages outside the nav sections highlight nothing (FR-006)', async ({ page }) => {
  // Logged out: the only nav link is Login (no data-page, never active)
  await page.goto('/Account/Login');
  await expect(page.locator('a.nav-link.active')).toHaveCount(0);

  // Logged in: profile page is outside every nav section
  await loginAsSuperUser(page);
  await page.goto('/Account/Profile');
  await expect(page.locator('a.nav-link.active')).toHaveCount(0);
});
