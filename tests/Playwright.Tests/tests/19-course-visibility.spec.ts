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
