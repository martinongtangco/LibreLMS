import { test, expect, Page } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';
import { testUsers } from '../utils/testUsers';

/**
 * Spec 058 US1 (P1) — dashboards report REAL completion data, not hardcoded zeros.
 *
 * Before the fix, DashboardService hardcoded AverageCompletionRate/CompletedCourseCount/
 * AverageScore to 0 on every view. After the fix they aggregate real CourseAttempt rows
 * (Scorm module, via the IScormAttemptStats contract).
 *
 * The test creates its own data per run (XVII.1): a unique learner in the root org who
 * completes one scored SCORM session, plus a unique empty org with its own OrgAdmin for
 * the "zero from real data" case (US1-2). Assertions are lower-bound (>= / > 0) because
 * the dev DB is shared and attempt data only grows; best-effort teardown keeps the DB
 * tidy without making the test depend on it.
 */

// The seeded SCORM course (same fixture as 15-scorm-launch-ui / 20-scorm-session-authz).
const SCORM_COURSE_ID = '11111111-1111-1111-1111-111111111111';

// One unique identity per run — idempotent against the persistent dev DB.
const RUN_TAG = `dash058-${Date.now()}`;
const LEARNER_EMAIL = `${RUN_TAG}@example.com`;
const LEARNER_NAME = `Dash 058 ${RUN_TAG}`;
const LEARNER_PASSWORD = `Dash058!x${RUN_TAG}9`;
const EMPTY_ORG_NAME = `Dash058 Empty ${RUN_TAG}`;
const EMPTY_ORG_ADMIN_EMAIL = `${RUN_TAG}-admin@example.com`;
const EMPTY_ORG_ADMIN_NAME = `Dash058 Empty Admin ${RUN_TAG}`;
const EMPTY_ORG_ADMIN_PASSWORD = `Dash058!A${RUN_TAG}9`;

const SCORE = 87.5;

test.describe('Dashboard real completion stats (spec 058 US1)', () => {
  // The learner test creates the shared attempt data the org/system tests assert on —
  // serial keeps the order deterministic (same pattern as 20-scorm-session-authz).
  test.describe.configure({ mode: 'serial' });

  let rootOrgId: string;
  let learnerId: string;
  let emptyOrgId: string;
  let emptyOrgAdminId: string;

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    await authFixture.loginAs(page, 'SuperUser');

    // Root org = the seeded "Root Organization" (the OrgAdmin's org, per 04-admin-dashboard).
    const orgRes = await page.request.get('/api/organizations');
    expect(orgRes.status()).toBe(200);
    const orgs: { id: string; name: string; parentId: string | null }[] =
      (await orgRes.json()).organizations;
    const root =
      orgs.find((o) => o.name === 'Root Organization') ??
      orgs.find((o) => o.parentId === null);
    expect(root, 'seeded root organization must exist').toBeTruthy();
    rootOrgId = root!.id;

    // Unique learner in the root org (admin-created accounts are verified → can sign in).
    const learnerRes = await page.request.post('/api/users', {
      data: {
        name: LEARNER_NAME,
        email: LEARNER_EMAIL,
        password: LEARNER_PASSWORD,
        role: 'Learner',
        organizationId: rootOrgId,
      },
    });
    expect(learnerRes.status(), `create learner: ${await learnerRes.text()}`).toBe(201);
    learnerId = new URL(learnerRes.headers()['location'] ?? '', 'http://x').pathname.split('/').pop()!;
    expect(learnerId).toMatch(/^[0-9a-f-]{36}$/i);

    // US1-2 fixture: a fresh org with no learners + an OrgAdmin scoped to it.
    const orgRes2 = await page.request.post('/api/organizations', {
      data: { name: EMPTY_ORG_NAME, description: 'spec 058 zero-case org', parentId: rootOrgId },
    });
    expect(orgRes2.status(), `create empty org: ${await orgRes2.text()}`).toBe(201);
    emptyOrgId = new URL(orgRes2.headers()['location'] ?? '', 'http://x').pathname.split('/').pop()!;

    const adminRes = await page.request.post('/api/users', {
      data: {
        name: EMPTY_ORG_ADMIN_NAME,
        email: EMPTY_ORG_ADMIN_EMAIL,
        password: EMPTY_ORG_ADMIN_PASSWORD,
        role: 'OrgAdmin',
        organizationId: emptyOrgId,
      },
    });
    expect(adminRes.status(), `create empty-org admin: ${await adminRes.text()}`).toBe(201);
    emptyOrgAdminId = new URL(adminRes.headers()['location'] ?? '', 'http://x').pathname.split('/').pop()!;

    await ctx.close();
  });

  test.afterAll(async ({ browser }) => {
    // Best-effort teardown: the dev DB is shared, and assertions above are lower-bound
    // so leftover rows can never flip a green run red.
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    try {
      await authFixture.loginAs(page, 'SuperUser');
      await page.request.delete(`/api/users/${learnerId}`);
      await page.request.delete(`/api/users/${emptyOrgAdminId}`);
      await page.request.delete(`/api/organizations/${emptyOrgId}`);
    } catch {
      // teardown is not part of the assertions
    } finally {
      await ctx.close();
    }
  });

  test('learner with one completed scored attempt: personal dashboard reflects it (US1-1)', async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();

    // Sign in as the fresh learner (admin-created, verified).
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(LEARNER_EMAIL);
    await page.getByLabel('Password').fill(LEARNER_PASSWORD);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(
      (url) => url.pathname === '/' || url.pathname.includes('/Courses'),
      { timeout: 10_000 }
    );

    // Enroll in the seeded SCORM course, then complete one scored session.
    const enrollRes = await page.request.post('/api/enrollments', {
      data: { courseId: SCORM_COURSE_ID },
    });
    expect(enrollRes.status(), `enroll: ${await enrollRes.text()}`).toBe(201);

    const launchRes = await page.request.post(`/api/scorm/${SCORM_COURSE_ID}/launch`);
    expect(launchRes.status(), `launch: ${await launchRes.text()}`).toBe(200);
    const { sessionId } = (await launchRes.json()) as { sessionId: string };

    for (const [element, value] of [
      ['cmi.core.lesson_status', 'completed'],
      ['cmi.core.score.raw', String(SCORE)],
    ] as const) {
      const setRes = await page.request.post(`/api/scorm/session/${sessionId}/setValue`, {
        data: { element, value },
      });
      expect(setRes.status(), `setValue ${element}: ${await setRes.text()}`).toBe(200);
    }

    const finishRes = await page.request.post(`/api/scorm/session/${sessionId}/finish`, {
      data: { exit: 'normal' },
    });
    expect(finishRes.status(), `finish: ${await finishRes.text()}`).toBe(200);
    const finishBody = (await finishRes.json()) as { success: boolean; status: string; score: number | null };
    expect(finishBody.success).toBe(true);
    expect(finishBody.status).toBe('completed');
    expect(finishBody.score).toBe(SCORE);

    // Personal dashboard: this learner's only attempt is completed with a recorded score.
    const dashRes = await page.request.get('/api/dashboard');
    expect(dashRes.status()).toBe(200);
    const body = (await dashRes.json()) as {
      role: string;
      metrics: { enrolledCourseCount: number; completedCourseCount: number; averageScore: number };
    };
    expect(body.role).toBe('Learner');
    // >= (not ==): the learner is unique per run, so 1 is expected — the lower bound
    // keeps the assertion stable if teardown is skipped mid-run and the row re-runs.
    expect(body.metrics.completedCourseCount).toBeGreaterThanOrEqual(1);
    expect(body.metrics.averageScore).toBeCloseTo(SCORE, 1);
    expect(body.metrics.enrolledCourseCount).toBeGreaterThanOrEqual(1);

    await ctx.close();
  });

  test('org dashboard for the learner org shows a non-zero completion rate (US1-1)', async ({ page }) => {
    await authFixture.loginAs(page, 'OrgAdmin');

    const res = await page.request.get('/api/dashboard');
    expect(res.status()).toBe(200);
    const body = (await res.json()) as {
      role: string;
      metrics: { averageCompletionRate: number };
    };
    expect(body.role).toBe('OrgAdmin');
    // The learner's completed attempt lives in the root org (the OrgAdmin's org).
    expect(body.metrics.averageCompletionRate).toBeGreaterThan(0);
  });

  test('system dashboard aggregates the same real per-attempt data (US1-3)', async ({ page }) => {
    await authFixture.loginAs(page, 'SuperUser');

    const res = await page.request.get('/api/dashboard');
    expect(res.status()).toBe(200);
    const body = (await res.json()) as {
      role: string;
      metrics: { averageCompletionRate: number };
    };
    expect(body.role).toBe('SuperUser');
    expect(body.metrics.averageCompletionRate).toBeGreaterThan(0);
  });

  test('org with no attempts reads 0 from real data, not an unwired field (US1-2)', async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();

    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(EMPTY_ORG_ADMIN_EMAIL);
    await page.getByLabel('Password').fill(EMPTY_ORG_ADMIN_PASSWORD);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(
      (url) => url.pathname === '/' || url.pathname.includes('/Courses'),
      { timeout: 10_000 }
    );

    const res = await page.request.get('/api/dashboard');
    expect(res.status()).toBe(200);
    const body = (await res.json()) as {
      role: string;
      metrics: { organizationName: string; learnerCount: number; averageCompletionRate: number };
    };
    expect(body.role).toBe('OrgAdmin');
    expect(body.metrics.organizationName).toBe(EMPTY_ORG_NAME);
    // Learner counts = Student rows (all accounts — roles distinguish privilege, see
    // UserLookupService), so exactly one: the org's own admin. Deterministic — the org
    // is private to this run.
    expect(body.metrics.learnerCount).toBe(1);
    // Zero because the subtree has no attempt data — the field is wired (contrast with
    // the pre-fix behavior where every org, however active, rendered 0).
    expect(body.metrics.averageCompletionRate).toBe(0);

    await ctx.close();
  });
});
