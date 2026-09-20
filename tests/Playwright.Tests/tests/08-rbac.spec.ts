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

  test('unauthenticated user redirected to login for /MyCourses/Index', async ({ page }) => {
    // Spec 055 US3 (FR-008, journey J4): My Courses is learner data — guests
    // are challenged to sign-in with the page as return address (pre-055 this
    // page rendered an empty state anonymously; the demo-identity fallback
    // made that state unreliable, so the page now requires sign-in).
    await page.goto('/MyCourses/Index');
    await expect(page).toHaveURL(/\/Account\/Login\?ReturnUrl=%2FMyCourses/);
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

// ─── RBAC — OrgAdmin subtree scope (spec 052) ─────────────────────────
//
// The seeded OrgAdmin (admin@example.com) sits in the Root org, whose
// subtree is everything — so cross-org denial needs a CHILD org admin.
// The block creates one (child org + child OrgAdmin + child learner) as
// SuperUser via the API, asserts the four surfaces deny the foreign
// subtree, then tears it down. No seed-data change: the exact-count
// baselines of the other specs are untouched.

const SEED_SCORM_COURSE_ID = '11111111-1111-1111-1111-111111111111';

interface SubtreeOrgDto { id: string; name: string; parentId: string | null }
interface SubtreeUserDto { id: string; name: string; email: string; role: string; organizationId: string }
interface SubtreeEnrollmentDto { enrollmentId: string; studentId: string; studentEmail: string; courseId: string }

test.describe('RBAC — OrgAdmin subtree scope (spec 052)', () => {
  test.describe.configure({ mode: 'serial' });

  // Unique per run: beforeAll re-runs on every retry, and org deletion is a
  // SOFT delete — the (Name, ParentId) unique index keeps the row, so a fixed
  // name would collide on the second attempt. Timestamp suffix avoids that
  // without depending on cleanup semantics.
  const runStamp = Date.now();
  const childOrgName = `Subtree Scope Org ${runStamp}`;
  const childAdminEmail = `scope-admin-${runStamp}@example.com`;
  const childLearnerEmail = `scope-learner-${runStamp}@example.com`;
  const rootLearnerEmail = testUsers.learner.email; // alice — Root org
  const password = 'Subtree@Org#2026';

  let rootOrgId = '';
  let childOrgId = '';
  let childAdminId = '';
  let childLearnerId = '';
  let rootLearnerId = '';
  let childEnrollmentId = '';

  /** A fresh authenticated context for the given user (cookie login). */
  async function contextFor(browser: import('@playwright/test').Browser, email: string, pwd: string) {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    await login(page, email, pwd);
    return { ctx, page };
  }

  test.beforeAll(async ({ browser }) => {
    const { ctx, page } = await contextFor(browser, testUsers.superUser.email, testUsers.superUser.password);
    try {
      // Idempotency: hard-delete user leftovers from a crashed earlier run
      // (org soft-deletes are harmless — unique names avoid index collisions).
      const staleUsers = (await (await page.request.get('/api/users')).json()).users as SubtreeUserDto[];
      for (const u of staleUsers.filter(x => x.email.startsWith('scope-admin-') || x.email.startsWith('scope-learner-'))) {
        await page.request.delete(`/api/users/${u.id}`);
      }

      // Root org (the parent for the child).
      const orgs = (await (await page.request.get('/api/organizations')).json()).organizations as SubtreeOrgDto[];
      rootOrgId = orgs.find(o => o.name === 'Root Organization')!.id;

      // Child org under Root.
      const orgRes = await page.request.post('/api/organizations', {
        data: { name: childOrgName, description: 'spec 052 E2E', parentId: rootOrgId },
      });
      expect(orgRes.ok(), `child org create: ${await orgRes.text()}`).toBeTruthy();
      childOrgId = ((await orgRes.json()) as SubtreeOrgDto).id;

      // Child OrgAdmin + child Learner.
      const adminRes = await page.request.post('/api/users', {
        data: { name: 'Scope Test Admin', email: childAdminEmail, password, role: 'OrgAdmin', organizationId: childOrgId },
      });
      expect(adminRes.status(), `child admin create: ${await adminRes.text()}`).toBe(201);
      childAdminId = ((await adminRes.json()) as SubtreeUserDto).id;

      const learnerRes = await page.request.post('/api/users', {
        data: { name: 'Scope Test Learner', email: childLearnerEmail, password, role: 'Learner', organizationId: childOrgId },
      });
      expect(learnerRes.status(), `child learner create: ${await learnerRes.text()}`).toBe(201);
      childLearnerId = ((await learnerRes.json()) as SubtreeUserDto).id;

      // Enroll the child learner in the seeded SCORM course.
      const enrRes = await page.request.post('/api/admin/enrollments', {
        data: { studentId: childLearnerId, courseId: SEED_SCORM_COURSE_ID },
      });
      expect(enrRes.ok(), `child enrollment: ${await enrRes.text()}`).toBeTruthy();
      childEnrollmentId = ((await enrRes.json()) as SubtreeEnrollmentDto).enrollmentId;

      // Root learner id + ensure the root learner has an enrollment in the
      // seeded course (the seeded data does; this only self-heals).
      const users = (await (await page.request.get('/api/users')).json()).users as SubtreeUserDto[];
      rootLearnerId = users.find(u => u.email === rootLearnerEmail)!.id;
      const enrList = (await (await page.request.get('/api/admin/enrollments')).json()).enrollments as SubtreeEnrollmentDto[];
      if (!enrList.some(e => e.studentId === rootLearnerId && e.courseId === SEED_SCORM_COURSE_ID)) {
        await page.request.post('/api/admin/enrollments', {
          data: { studentId: rootLearnerId, courseId: SEED_SCORM_COURSE_ID },
        });
      }
    } finally {
      await ctx.close();
    }
  });

  test.afterAll(async ({ browser }) => {
    const { ctx, page } = await contextFor(browser, testUsers.superUser.email, testUsers.superUser.password);
    try {
      if (childEnrollmentId) await page.request.delete(`/api/admin/enrollments/${childEnrollmentId}`);
      if (childAdminId) await page.request.delete(`/api/users/${childAdminId}`);
      if (childLearnerId) await page.request.delete(`/api/users/${childLearnerId}`);
      if (childOrgId) await page.request.delete(`/api/organizations/${childOrgId}`);
    } finally {
      await ctx.close();
    }
  });

  test('users: child OrgAdmin sees only the subtree; the foreign user id is 403', async ({ browser }) => {
    const { ctx, page } = await contextFor(browser, childAdminEmail, password);
    try {
      const res = await page.request.get('/api/users');
      expect(res.ok()).toBeTruthy();
      const users = (await res.json()).users as SubtreeUserDto[];
      const emails = users.map(u => u.email);
      expect(emails, 'child subtree learner must be visible').toContain(childLearnerEmail);
      expect(emails, 'root-org learner must NOT be visible to a child OrgAdmin').not.toContain(rootLearnerEmail);

      const foreign = await page.request.get(`/api/users/${rootLearnerId}`);
      expect(foreign.status(), 'out-of-subtree user read must be 403').toBe(403);
    } finally {
      await ctx.close();
    }
  });

  test('organizations: child OrgAdmin sees only the subtree; the foreign org is 403', async ({ browser }) => {
    const { ctx, page } = await contextFor(browser, childAdminEmail, password);
    try {
      const res = await page.request.get('/api/organizations');
      expect(res.ok()).toBeTruthy();
      const orgs = (await res.json()).organizations as SubtreeOrgDto[];
      const ids = orgs.map(o => o.id);
      expect(ids, 'child org must be visible').toContain(childOrgId);
      expect(ids, 'the parent/root org must NOT be visible to a child OrgAdmin').not.toContain(rootOrgId);

      const foreign = await page.request.get(`/api/organizations/${rootOrgId}`);
      expect(foreign.status(), 'out-of-subtree org read must be 403').toBe(403);
    } finally {
      await ctx.close();
    }
  });

  test('course visibility: out-of-subtree org is 403; in-subtree org works', async ({ browser }) => {
    const { ctx, page } = await contextFor(browser, childAdminEmail, password);
    try {
      const foreign = await page.request.put(
        `/api/admin/courses/${SEED_SCORM_COURSE_ID}/visibility?organizationId=${rootOrgId}&isHidden=true`
      );
      expect(foreign.status(), 'visibility write for an out-of-subtree org must be 403').toBe(403);

      const own = await page.request.put(
        `/api/admin/courses/${SEED_SCORM_COURSE_ID}/visibility?organizationId=${childOrgId}&isHidden=true`
      );
      expect(own.status(), `in-subtree visibility write must work: ${await own.text()}`).toBe(200);

      // Restore (the override is per (org, course); the child org is torn
      // down in afterAll anyway — this keeps the seeded course clean).
      const restore = await page.request.put(
        `/api/admin/courses/${SEED_SCORM_COURSE_ID}/visibility?organizationId=${childOrgId}&isHidden=false`
      );
      expect(restore.status()).toBe(200);
    } finally {
      await ctx.close();
    }
  });

  test('enrollments: only the subtree is visible; enrolling a foreign student is 403', async ({ browser }) => {
    const { ctx, page } = await contextFor(browser, childAdminEmail, password);
    try {
      const res = await page.request.get('/api/admin/enrollments');
      expect(res.ok()).toBeTruthy();
      const enrollments = (await res.json()).enrollments as SubtreeEnrollmentDto[];
      const studentEmails = enrollments.map(e => e.studentEmail);
      expect(studentEmails, 'child learner enrollment must be visible').toContain(childLearnerEmail);
      expect(studentEmails, 'root-org learner enrollment must NOT be visible').not.toContain(rootLearnerEmail);

      const foreign = await page.request.post('/api/admin/enrollments', {
        data: { studentId: rootLearnerId, courseId: SEED_SCORM_COURSE_ID },
      });
      expect(foreign.status(), 'enrolling an out-of-subtree student must be 403').toBe(403);
    } finally {
      await ctx.close();
    }
  });

  test('SuperUser is unaffected: system-wide users list', async ({ browser }) => {
    const { ctx, page } = await contextFor(browser, testUsers.superUser.email, testUsers.superUser.password);
    try {
      const res = await page.request.get('/api/users');
      expect(res.ok()).toBeTruthy();
      const emails = ((await res.json()).users as SubtreeUserDto[]).map(u => u.email);
      expect(emails, 'SuperUser sees the root learner').toContain(rootLearnerEmail);
      expect(emails, 'SuperUser sees the child subtree learner').toContain(childLearnerEmail);
    } finally {
      await ctx.close();
    }
  });
});
