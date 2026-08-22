import { test, expect, Page, type APIRequestContext, type Browser } from '@playwright/test';
import { AdminEnrollmentsPage } from '../pages/AdminEnrollmentsPage';
import { AdminLearnersPage } from '../pages/AdminLearnersPage';
import { testUsers } from '../utils/testUsers';

/**
 * Admin list pagination + page size toggle (spec 032).
 *
 * Self-contained per the 11-course-pagination pattern: beforeAll creates
 * marker-prefixed filler data through the admin API, afterAll deletes it.
 * Every UI assertion is scoped to the marker, so seeded/accumulated data in
 * the system is irrelevant and other specs can run concurrently.
 *
 * Blocks are appended in task order (single writer — the orchestrating
 * session — see tasks.md serialization rule):
 *   T014 Admin Enrollments  ->  T023 Admin Learners  ->  T025 page-size toggle
 *   ->  T032 Admin Courses  ->  T036 cross-page consistency
 */

const ROOT_ORG = '00000000-0000-0000-0000-000000000001';
const MARKER = 'AdmPg032E'; // Enrollments story filler (learners + courses)
const LEARNERS_MARKER = 'AdmPg032L'; // Learners story filler (accounts only)
const FILLER_PASSWORD = 'Qw3rt!Pg032Filler';
const LEARNER_COUNT = 12; // > one default page (10) so the controls render
const COURSE_COUNT = 3;

const pad2 = (n: number) => String(n).padStart(2, '0');
const learnerName = (n: number) => `${MARKER} S${pad2(n)}`;
const learnerEmail = (n: number) => `adm.pg032e.${pad2(n)}@example.com`;
const courseTitle = (n: number) => `${MARKER} Course ${n}`;
const accountName = (n: number) => `${LEARNERS_MARKER} Alpha${pad2(n)}`;
const accountEmail = (n: number) => `adm.pg032l.${pad2(n)}@example.com`;
/** Same role pattern as the integration tests: 4 Learner, 4 OrgAdmin, 4 SuperUser. */
const accountRole = (n: number): string =>
  n % 3 === 1 ? 'Learner' : n % 3 === 2 ? 'OrgAdmin' : 'SuperUser';

test.describe.configure({ mode: 'serial' });

/**
 * Sign in as SuperUser in a throwaway browser and return the API request
 * context with its auth cookies (pattern: 11-course-pagination.spec.ts).
 */
async function adminApi(playwright: { chromium: import('@playwright/test').Chromium }) {
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

interface FillerIds {
  learnerIds: string[];
  accountIds: string[];
  courseIds: string[];
}

/** Collect the ids of filler rows still present (from a previously interrupted run). */
async function findStaleFillers(api: APIRequestContext): Promise<FillerIds> {
  const ids: FillerIds = { learnerIds: [], accountIds: [], courseIds: [] };

  const usersResp = await api.get('/api/users');
  if (usersResp.ok()) {
    const { users } = await usersResp.json();
    for (const u of users) {
      if (typeof u.name !== 'string') continue;
      if (u.name.startsWith(LEARNERS_MARKER)) ids.accountIds.push(u.id);
      else if (u.name.startsWith(MARKER)) ids.learnerIds.push(u.id);
    }
  }

  const coursesResp = await api.get('/api/admin/courses');
  if (coursesResp.ok()) {
    const { courses } = await coursesResp.json();
    for (const c of courses) {
      if (typeof c.title === 'string' && c.title.startsWith(`${MARKER} Course`)) {
        ids.courseIds.push(c.courseId);
      }
    }
  }

  return ids;
}

async function deleteFillers(api: APIRequestContext) {
  // Enrollments first (the users endpoint deletes the student row only).
  const enrollResp = await api.get(`/api/admin/enrollments?student=${encodeURIComponent(MARKER)}`);
  if (enrollResp.ok()) {
    const { enrollments } = await enrollResp.json();
    for (const e of enrollments) {
      await api.delete(`/api/admin/enrollments/${e.enrollmentId}`);
    }
  }

  const stale = await findStaleFillers(api);
  for (const id of stale.learnerIds) {
    await api.delete(`/api/users/${id}`);
  }
  for (const id of stale.accountIds) {
    await api.delete(`/api/users/${id}`);
  }
  for (const id of stale.courseIds) {
    await api.delete(`/api/admin/courses/${id}`);
  }
}

test.beforeAll(async ({ playwright }) => {
  const { browser, api } = await adminApi(playwright);
  try {
    await deleteFillers(api);

    const ids: FillerIds = { learnerIds: [], accountIds: [], courseIds: [] };

    for (let n = 1; n <= COURSE_COUNT; n++) {
      const resp = await api.post('/api/courses', {
        data: {
          title: courseTitle(n),
          shortDescription: 'Pagination test filler (spec 032, removed after the run).',
          fullDescription: `Temporary filler course created by 16-admin-pagination.spec.ts. Deleted in afterAll.`,
          category: 'Tools',
          duration: '1 hour',
          organizationId: ROOT_ORG,
        },
      });
      if (resp.status() !== 201) throw new Error(`Filler course ${courseTitle(n)}: HTTP ${resp.status()}`);
      const location = resp.headers()['location'] ?? '';
      ids.courseIds.push(location.split('/').pop() ?? '');
    }

    for (let n = 1; n <= LEARNER_COUNT; n++) {
      const resp = await api.post('/api/users', {
        data: {
          name: learnerName(n),
          email: learnerEmail(n),
          password: FILLER_PASSWORD,
          role: 'Learner',
          organizationId: ROOT_ORG,
        },
      });
      if (resp.status() !== 201) throw new Error(`Filler learner ${learnerName(n)}: HTTP ${resp.status()}`);
      const location = resp.headers()['location'] ?? '';
      ids.learnerIds.push(location.split('/').pop() ?? '');
    }

    // Learner i enrolls in course ((i-1) mod 3)+1 — every course has exactly 4 learners.
    for (let n = 1; n <= LEARNER_COUNT; n++) {
      const resp = await api.post('/api/admin/enrollments', {
        data: { studentId: ids.learnerIds[n - 1], courseId: ids.courseIds[(n - 1) % COURSE_COUNT] },
      });
      const status = resp.status();
      if (status !== 201 && status !== 409) {
        throw new Error(`Filler enrollment for ${learnerName(n)}: HTTP ${status}`);
      }
    }

    // Learners-story filler: 12 accounts with mixed roles (no enrollments).
    for (let n = 1; n <= LEARNER_COUNT; n++) {
      const resp = await api.post('/api/users', {
        data: {
          name: accountName(n),
          email: accountEmail(n),
          password: FILLER_PASSWORD,
          role: accountRole(n),
          organizationId: ROOT_ORG,
        },
      });
      if (resp.status() !== 201) throw new Error(`Filler account ${accountName(n)}: HTTP ${resp.status()}`);
      const location = resp.headers()['location'] ?? '';
      ids.accountIds.push(location.split('/').pop() ?? '');
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

// ────────────────────────────────────────────────────────────────────
// T014 — US1: Admin > Enrollments pagination (marker-scoped to 12 rows)
// ────────────────────────────────────────────────────────────────────
test.describe('Admin Enrollments pagination (spec 032, US1)', () => {
  test('shows one page of rows with pagination controls', async ({ page }) => {
    await loginAsSuperUser(page);
    const enrollments = new AdminEnrollmentsPage(page);
    await enrollments.gotoWithQuery(`student=${encodeURIComponent(MARKER)}`);

    expect(await enrollments.getRowCount()).toBe(10); // default page size 10 of 12
    await expect(enrollments.pageIndicator).toHaveText(`Page 1 of 2 (${LEARNER_COUNT} total)`);
    await expect(enrollments.paginationNav).toBeVisible();
  });

  test('previous hidden on page 1, next hidden on the last page', async ({ page }) => {
    await loginAsSuperUser(page);
    const enrollments = new AdminEnrollmentsPage(page);
    await enrollments.gotoWithQuery(`student=${encodeURIComponent(MARKER)}`);

    // Page 1: Previous absent from the DOM entirely, Next present.
    await expect(enrollments.previousLink).toHaveCount(0);
    await expect(enrollments.nextLink).toHaveCount(1);

    await enrollments.nextLink.click();
    await page.waitForLoadState('networkidle');

    // Last page (2 rows): Next absent, Previous present.
    expect(await enrollments.getRowCount()).toBe(LEARNER_COUNT - 10);
    await expect(enrollments.pageIndicator).toHaveText(`Page 2 of 2 (${LEARNER_COUNT} total)`);
    await expect(enrollments.nextLink).toHaveCount(0);
    await expect(enrollments.previousLink).toHaveCount(1);
  });

  test('course filter composes with pagination and resets to page 1', async ({ page }) => {
    await loginAsSuperUser(page);
    const enrollments = new AdminEnrollmentsPage(page);
    // Start on page 2 of the marker filter.
    await enrollments.gotoWithQuery(`student=${encodeURIComponent(MARKER)}&pageNumber=2`);
    await expect(enrollments.pageIndicator).toContainText('Page 2 of 2');

    // Course 1 has exactly 4 learners (1, 4, 7, 10) — one page, so the whole
    // nav (incl. indicator) is hidden (interaction rule 4). Row count pins the
    // filtered total (FR-013).
    await enrollments.filterByCourse(courseTitle(1));
    expect(await enrollments.getRowCount()).toBe(4);
    await expect(enrollments.paginationNav).toHaveCount(0);
  });

  test('single-row results hide the whole nav', async ({ page }) => {
    await loginAsSuperUser(page);
    const enrollments = new AdminEnrollmentsPage(page);
    await enrollments.gotoWithQuery(`student=${encodeURIComponent(MARKER)}`);
    await enrollments.filterByStudent(learnerName(12));

    expect(await enrollments.getRowCount()).toBe(1);
    await expect(enrollments.paginationNav).toHaveCount(0);
  });

  test('filter change resets pagination to page 1', async ({ page }) => {
    await loginAsSuperUser(page);
    const enrollments = new AdminEnrollmentsPage(page);
    await enrollments.gotoWithQuery(`student=${encodeURIComponent(MARKER)}&pageNumber=2`);
    await expect(enrollments.pageIndicator).toContainText('Page 2 of 2');

    // Narrow to S01..S09 (9 rows, one page) via the filter form.
    await enrollments.filterByStudent(`${MARKER} S0`);
    expect(await enrollments.getRowCount()).toBe(9);
    await expect(enrollments.paginationNav).toHaveCount(0);
    await expect(page).toHaveURL(/pageNumber=1/);
  });
});

// ────────────────────────────────────────────────────────────────────
// T023 — US2: Admin > Learners pagination (marker-scoped to 12 accounts)
// ────────────────────────────────────────────────────────────────────
test.describe('Admin Learners pagination (spec 032, US2)', () => {
  test('shows one page of rows with pagination controls', async ({ page }) => {
    await loginAsSuperUser(page);
    const learners = new AdminLearnersPage(page);
    await learners.gotoWithQuery(`search=${encodeURIComponent(LEARNERS_MARKER)}`);

    expect(await learners.getRowCount()).toBe(10); // default page size 10 of 12
    await expect(learners.pageIndicator).toHaveText(`Page 1 of 2 (${LEARNER_COUNT} total)`);
    await expect(learners.paginationNav).toBeVisible();
  });

  test('previous hidden on page 1, next hidden on the last page', async ({ page }) => {
    await loginAsSuperUser(page);
    const learners = new AdminLearnersPage(page);
    await learners.gotoWithQuery(`search=${encodeURIComponent(LEARNERS_MARKER)}`);

    await expect(learners.previousLink).toHaveCount(0);
    await expect(learners.nextLink).toHaveCount(1);

    await learners.nextLink.click();
    await page.waitForLoadState('networkidle');

    expect(await learners.getRowCount()).toBe(LEARNER_COUNT - 10);
    await expect(learners.pageIndicator).toHaveText(`Page 2 of 2 (${LEARNER_COUNT} total)`);
    await expect(learners.nextLink).toHaveCount(0);
    await expect(learners.previousLink).toHaveCount(1);
  });

  test('search narrows paginated results', async ({ page }) => {
    await loginAsSuperUser(page);
    const learners = new AdminLearnersPage(page);
    await learners.gotoWithQuery(`search=${encodeURIComponent(LEARNERS_MARKER)}`);
    await expect(learners.pageIndicator).toContainText('Page 1 of 2');

    // Alpha01..Alpha09 (9 rows, one page) — nav disappears, rows pin the total.
    await learners.searchFor(`${LEARNERS_MARKER} Alpha0`);
    expect(await learners.getRowCount()).toBe(9);
    await expect(learners.paginationNav).toHaveCount(0);
  });

  test('role filter composes with search', async ({ page }) => {
    await loginAsSuperUser(page);
    const learners = new AdminLearnersPage(page);
    // OrgAdmin accounts: Alpha02, Alpha05, Alpha08, Alpha11 — 4 rows, one page.
    await learners.gotoWithQuery(
      `search=${encodeURIComponent(LEARNERS_MARKER)}&role=OrgAdmin`,
    );

    expect(await learners.getRowCount()).toBe(4);
    await expect(learners.paginationNav).toHaveCount(0);
    const names = await learners.getLearnerNames();
    expect(names).toContain('AdmPg032L Alpha02');
    expect(names).toContain('AdmPg032L Alpha11');
  });
});
