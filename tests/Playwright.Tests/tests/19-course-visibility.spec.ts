import { test, expect, Page } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';
import { testUsers } from '../utils/testUsers';

/**
 * Course visibility E2E (spec 047 / spec 009 scenario 5).
 *
 * Verifies that a course an org admin has hidden from an organization
 * disappears from that organization's learner catalog, other courses are
 * unaffected, and unhiding brings the course back.
 *
 * The seed data has a single root org (all users/courses root-owned) and
 * SetVisibilityOverrideAsync refuses locally-owned courses, so a course can
 * only be hidden from a *child* org that inherits it. The test therefore
 * builds the inheritance path per run (unique names via Date.now()):
 *
 *   1. SuperUser creates a child org under root (POST /api/organizations).
 *   2. SuperUser creates a verified Learner in that org (POST /api/users —
 *      admin-created accounts are auto-verified and can log in immediately).
 *   3. Learner browses /Courses/Index → an inherited seeded root course is
 *      visible.
 *   4. SuperUser PUTs the visibility override (isHidden=true) for the child
 *      org → the learner no longer sees the course; a second seeded course
 *      remains visible.
 *   5. Unhide (isHidden=false) → the course is back.
 *   6. finally: delete the learner, then the child org. The leftover
 *      IsHidden=false override row is inert (no FK on CourseVisibilityOverrides).
 *
 * The test never mutates alice or root-org state — it only reads root
 * courses and writes throwaway child-org rows.
 */

const ROOT_ORG_ID = '00000000-0000-0000-0000-000000000001';

// Seeded, non-SCORM, root-owned courses (CatalogSeeder) — resolved by title
// at runtime, never by hardcoded GUID.
const TARGET_COURSE = 'Database Design Fundamentals';
const CONTROL_COURSE = 'Advanced .NET Patterns';

async function loginForm(page: Page, email: string, password: string) {
  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(/Courses/, { timeout: 10_000 });
}

test.describe('Course visibility (org admin hide/unhide)', () => {
  /**
   * Spec 054: the pager must agree with the visible course count.
   *
   * Fixture math (pageSize = 12): 10 seeded root courses + 14 admin-created
   * root courses (titles `ZZ Pag <ts> ..` so they sort to the end) = 24
   * total. Hiding the 14 new + 2 seeded from the child org leaves 8 visible.
   *
   * Pre-fix (RED): the SP pages/counts unfiltered → total 24 → "Page 1 of 2
   * (24 total)"; page 1 renders 8 cards (hidden rows stripped after paging);
   * the advertised page 2 renders empty.
   * Post-fix: total 8 → one page, no pagination nav at all.
   */
  test('pagination agrees with the visible course count when courses are hidden', async ({ browser }) => {
    const ts = Date.now();
    const learnerEmail = `pagvis${ts}@example.com`;
    const learnerPassword = 'Sup3rSecret!x9';

    let childOrgId: string | undefined;
    let learnerId: string | undefined;
    let superUserId: string | undefined;
    const createdCourseIds: string[] = [];
    const hiddenCourseIds: string[] = [];

    const adminPage = await browser.newPage();
    try {
      await authFixture.loginAs(adminPage, 'SuperUser');

      // 1. Child org (unique per run — org delete is a soft delete, so the
      //    (Name, ParentId) unique index makes fixed names non-idempotent).
      const orgRes = await adminPage.request.post('/api/organizations', {
        data: {
          name: `PagVis Org ${ts}`,
          description: 'spec 054 e2e: pagination with hidden courses',
          parentId: ROOT_ORG_ID,
        },
      });
      expect(orgRes.status()).toBe(201);
      childOrgId = (await orgRes.json()).id;

      // 2. Verified learner in the child org (admin-created ⇒ verified).
      const userRes = await adminPage.request.post('/api/users', {
        data: {
          name: `PagVis Learner ${ts}`,
          email: learnerEmail,
          password: learnerPassword,
          role: 'Learner',
          organizationId: childOrgId,
        },
      });
      expect(userRes.status()).toBe(201);
      learnerId = (await userRes.json()).id;

      // SuperUser id for the visibility overrides (createdBy query param).
      const usersRes = await adminPage.request.get('/api/users');
      expect(usersRes.status()).toBe(200);
      superUserId = (await usersRes.json()).users.find(
        (u: { email: string }) => u.email === testUsers.superUser.email
      )?.id;
      expect(superUserId, 'SuperUser must appear in GET /api/users').toBeTruthy();

      // 3. 14 root-owned courses via the admin UI (no course-creation API
      //    exists; the SuperUser's org claim is root, so the child org
      //    inherits every one of them).
      for (let i = 1; i <= 14; i++) {
        const title = `ZZ Pag ${ts} ${String(i).padStart(2, '0')}`;
        await adminPage.goto('/Admin/Courses/Create');
        await adminPage.getByLabel('Title').fill(title);
        await adminPage.getByLabel('Short Description').fill('spec 054 e2e filler');
        await adminPage.getByLabel('Full Description').fill('spec 054 e2e filler');
        await adminPage.getByLabel('Category').fill('Spec 054 Pagination');
        await adminPage.getByLabel('Duration').fill('1 hour');
        await adminPage.getByRole('button', { name: 'Create Course' }).click();
        // Razor Pages route: Pages/Admin/Courses/Index.cshtml → /Admin/Courses
        // (the Index segment is not in the URL).
        await adminPage.waitForURL(/Admin\/Courses(\?|$)/, { timeout: 20_000 });
      }

      // 4. Resolve ids: the 14 new + 2 seeded to hide (16 of 24 → 8 visible).
      const coursesRes = await adminPage.request.get('/api/courses');
      expect(coursesRes.status()).toBe(200);
      const { courses } = await coursesRes.json();
      for (const c of courses as { id: string; title: string }[]) {
        if (c.title.startsWith(`ZZ Pag ${ts} `))
          createdCourseIds.push(c.id);
      }
      expect(createdCourseIds).toHaveLength(14);
      hiddenCourseIds.push(...createdCourseIds);
      for (const t of [TARGET_COURSE, CONTROL_COURSE]) {
        const id = courses.find((c: { title: string }) => c.title === t)?.id;
        expect(id, `seeded course '${t}' must exist`).toBeTruthy();
        hiddenCourseIds.push(id);
      }
      expect(hiddenCourseIds).toHaveLength(16);

      // 5. Hide all 16 from the child org.
      for (const id of hiddenCourseIds) {
        const r = await adminPage.request.put(
          `/api/admin/courses/${id}/visibility?organizationId=${childOrgId}&isHidden=true&createdBy=${superUserId}`
        );
        expect(r.status()).toBe(200);
      }

      // 6. Learner's catalog: 8 visible of 24.
      const learnerPage = await browser.newPage();
      try {
        await loginForm(learnerPage, learnerEmail, learnerPassword);
        await learnerPage.goto('/Courses/Index');

        // Exactly the 8 visible courses on the page...
        await expect(learnerPage.locator('.metric-cards .card')).toHaveCount(8);

        // ...and the pager must agree: 8 visible → one page → no pagination
        // nav at all. RED pre-fix: nav shows "Page 1 of 2 (24 total)" and the
        // advertised page 2 renders empty.
        await expect(learnerPage.locator('nav.pagination')).toHaveCount(0);
        await expect(learnerPage.getByText(/Page \d+ of \d+/)).toHaveCount(0);
      } finally {
        await learnerPage.close();
      }
    } finally {
      // Teardown: unhide, delete the 14 courses, then learner, then org
      // (override rows are inert once the org is gone, but unhide anyway so
      //    the seeded courses' override rows read false).
      for (const id of hiddenCourseIds)
        await adminPage.request
          .put(`/api/admin/courses/${id}/visibility?organizationId=${childOrgId}&isHidden=false&createdBy=${superUserId}`)
          .catch(() => {});
      // Re-resolve by the run's title prefix — if a failure happened mid
      // creation loop, createdCourseIds is incomplete and the orphaned
      // courses would poison every later run of this spec (they sort into
      // the learner's page 1).
      let toDelete = [...createdCourseIds];
      try {
        const res = await adminPage.request.get('/api/courses');
        if (res.ok()) {
          const { courses } = await res.json();
          toDelete = (courses as { id: string; title: string }[])
            .filter((c) => c.title.startsWith(`ZZ Pag ${ts} `))
            .map((c) => c.id);
        }
      } catch { /* fall back to the recorded ids */ }
      for (const id of toDelete)
        await adminPage.request.delete(`/api/admin/courses/${id}`).catch(() => {});
      if (learnerId) await adminPage.request.delete(`/api/users/${learnerId}`).catch(() => {});
      if (childOrgId) await adminPage.request.delete(`/api/organizations/${childOrgId}`).catch(() => {});
      await adminPage.close();
    }
  });

  test('hidden inherited course is excluded from the child org learner catalog', async ({ browser }) => {
    const ts = Date.now();
    const learnerEmail = `visib${ts}@example.com`;
    // Must pass the credential policy (cf. 14-profile-courses 'Sup3rSecret!x9' pattern).
    const learnerPassword = 'Sup3rSecret!x9';

    let childOrgId: string | undefined;
    let learnerId: string | undefined;
    let targetCourseId: string | undefined;

    // SuperUser page: setup, API calls, and cleanup (page.request shares the
    // page context's cookies, so these calls are authenticated).
    const adminPage = await browser.newPage();
    try {
      await authFixture.loginAs(adminPage, 'SuperUser');

      // 1. Child org under root (unique per run).
      const orgRes = await adminPage.request.post('/api/organizations', {
        data: {
          name: `Visib Org ${ts}`,
          description: 'bug-047 e2e: child org for visibility test',
          parentId: ROOT_ORG_ID,
        },
      });
      expect(orgRes.status()).toBe(201);
      childOrgId = (await orgRes.json()).id;

      // 2. Verified learner in the child org (admin-created ⇒ verified).
      const userRes = await adminPage.request.post('/api/users', {
        data: {
          name: `Visib Learner ${ts}`,
          email: learnerEmail,
          password: learnerPassword,
          role: 'Learner',
          organizationId: childOrgId,
        },
      });
      expect(userRes.status()).toBe(201);
      learnerId = (await userRes.json()).id;

      // 3. Resolve course ids by title (do not hardcode GUIDs).
      const coursesRes = await adminPage.request.get('/api/courses');
      expect(coursesRes.status()).toBe(200);
      const { courses } = await coursesRes.json();
      targetCourseId = courses.find((c: { title: string }) => c.title === TARGET_COURSE)?.id;
      const controlCourseId = courses.find((c: { title: string }) => c.title === CONTROL_COURSE)?.id;
      expect(targetCourseId, `seeded course '${TARGET_COURSE}' must exist`).toBeTruthy();
      expect(controlCourseId, `seeded course '${CONTROL_COURSE}' must exist`).toBeTruthy();

      // SuperUser id for the createdBy query param.
      const usersRes = await adminPage.request.get('/api/users');
      expect(usersRes.status()).toBe(200);
      const superUserId = (await usersRes.json()).users.find(
        (u: { email: string }) => u.email === testUsers.superUser.email
      )?.id;
      expect(superUserId, 'SuperUser must appear in GET /api/users').toBeTruthy();

      // 4. Learner page (fresh context) — plain form login, since
      //    authFixture.loginAs only knows the seeded users.
      const learnerPage = await browser.newPage();
      try {
        await loginForm(learnerPage, learnerEmail, learnerPassword);

        // Before: the inherited root course is visible in the learner catalog.
        await learnerPage.goto('/Courses/Index');
        await expect(learnerPage.getByRole('link', { name: TARGET_COURSE })).toBeVisible();
        await expect(learnerPage.getByRole('link', { name: CONTROL_COURSE })).toBeVisible();

        // Hide the target course from the child org.
        const hideRes = await adminPage.request.put(
          `/api/admin/courses/${targetCourseId}/visibility?organizationId=${childOrgId}&isHidden=true&createdBy=${superUserId}`
        );
        expect(hideRes.status()).toBe(200);

        // After hide: target course is gone; the control course is unaffected.
        await learnerPage.goto('/Courses/Index');
        await expect(learnerPage.getByRole('link', { name: TARGET_COURSE })).not.toBeVisible();
        await expect(learnerPage.getByRole('link', { name: CONTROL_COURSE })).toBeVisible();

        // Unhide.
        const unhideRes = await adminPage.request.put(
          `/api/admin/courses/${targetCourseId}/visibility?organizationId=${childOrgId}&isHidden=false&createdBy=${superUserId}`
        );
        expect(unhideRes.status()).toBe(200);

        // After unhide: the course is back.
        await learnerPage.goto('/Courses/Index');
        await expect(learnerPage.getByRole('link', { name: TARGET_COURSE })).toBeVisible();
      } finally {
        await learnerPage.close();
        // 6. Cleanup: learner first, then the child org (no FK on the
        //    override table; the leftover IsHidden=false row is inert).
        if (learnerId)
          await adminPage.request.delete(`/api/users/${learnerId}`);
        if (childOrgId)
          await adminPage.request.delete(`/api/organizations/${childOrgId}`);
      }
    } finally {
      await adminPage.close();
    }
  });
});
