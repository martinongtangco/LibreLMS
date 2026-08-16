import { test, expect, Page } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';
import * as net from 'node:net';

/**
 * Spec 030 US2 E2E — My Courses grouping (FR-005/FR-006/FR-007).
 *
 * Written FIRST per TDD. Idempotent across runs (the DB persists): enrolling
 * an already-enrolled course is a UI no-op, and completing the seeded SCORM
 * course just adds another completed attempt — the grouping assertions (which
 * are per-course-title, not counts of sections) hold on every run.
 */

const SCORM_COURSE_ID = '11111111-1111-1111-1111-111111111111';
const SCORM_TITLE = 'Introduction to C#';
// Seeded non-SCORM courses that always render on the first browse page
// (page size 12) for the root-org browse view.
const ENROLL_TITLES = ['Advanced .NET Patterns', 'Database Design Fundamentals'];

/**
 * Clear the ephemeral SCORM session state in Valkey (keys: scorm:session:{id}).
 *
 * A stale ACTIVE session (30-minute TTL, and Valkey persists across app
 * restarts in this dev environment) blocks a fresh launch with "A session for
 * this course is already active". The SCORM tests below therefore flush their
 * own ephemeral store when they hit that state — Valkey holds ONLY SCORM
 * runtime state in this project (Constitution VI), so FLUSHALL touches exactly
 * that. Raw RESP over node:net — no new dependency.
 */
async function flushScormSessions(): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const socket = net.connect(6379, 'valkey', () => {
      socket.write('*1\r\n$8\r\nFLUSHALL\r\n');
    });
    const timer = setTimeout(() => {
      socket.destroy();
      reject(new Error('valkey FLUSHALL timed out'));
    }, 5000);
    socket.once('data', () => {
      clearTimeout(timer);
      socket.end();
      resolve();
    });
    socket.once('error', (err) => {
      clearTimeout(timer);
      reject(err);
    });
  });
}

/**
 * Launch a SCORM session, recovering from a stale active session left over
 * from a previous run (flush the ephemeral store and retry once).
 */
async function launchScorm(page: Page, courseId: string): Promise<string> {
  let launch = await page.request.post(`/api/scorm/${courseId}/launch`);
  if (launch.status() === 400) {
    const body = await launch.text();
    if (body.includes('already active')) {
      await flushScormSessions();
      launch = await page.request.post(`/api/scorm/${courseId}/launch`);
    }
  }
  expect(launch.ok(), `launch failed: ${await launch.text().catch(() => '')}`).toBeTruthy();
  const body = (await launch.json()) as { sessionId: string };
  return body.sessionId;
}

/** Enroll via the course detail page UI; a no-op when already enrolled. */
async function ensureEnrolled(page: Page, title: string): Promise<void> {
  await page.goto('/Courses/Index');
  await page.getByRole('link', { name: title }).click();

  const enrollButton = page.getByRole('button', { name: 'Enroll now' });
  if (await enrollButton.isVisible().catch(() => false)) {
    await enrollButton.click();
  }
  // Either way the region now shows the enrolled state.
  await expect(page.getByRole('button', { name: '✓ Enrolled' })).toBeVisible();
}

test.describe('Profile — My Courses (spec 030 US2)', () => {
  // Serial: the SCORM tests share alice's session state in Valkey (an active
  // session blocks relaunch), so they must not interleave.
  test.describe.configure({ mode: 'serial' });

  test('enrolled courses appear under Enrolled with status labels, every course in exactly one section', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    for (const title of ENROLL_TITLES) {
      await ensureEnrolled(page, title);
    }

    await page.goto('/Account/Profile');
    await expect(page.locator('#my-courses')).toBeVisible();

    // The freshly enrolled (attempt-less) courses sit under Enrolled, labeled.
    const enrolled = page.locator('#enrolled-courses');
    for (const title of ENROLL_TITLES) {
      const row = enrolled.locator('.profile-course-row', { hasText: title });
      await expect(row).toBeVisible();
      await expect(row.locator('.profile-course-status')).toHaveText('Not Started');
    }

    // FR-006: every course appears in exactly one section (Completed wins).
    for (const title of [...ENROLL_TITLES, SCORM_TITLE]) {
      const inCompleted = await page.locator('#completed-courses .profile-course-title', { hasText: title }).count();
      const inEnrolled = await page.locator('#enrolled-courses .profile-course-title', { hasText: title }).count();
      expect(inCompleted + inEnrolled, `${title} must be in exactly one section`).toBe(1);
    }
  });

  test('a course with a completed attempt appears under Completed', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');

    // Deterministically complete the seeded SCORM course via the session API
    // (launch → set cmi.core.lesson_status → finish).
    const sessionId = await launchScorm(page, SCORM_COURSE_ID);

    const setValue = await page.request.post(`/api/scorm/session/${sessionId}/setValue`, {
      data: { element: 'cmi.core.lesson_status', value: 'completed' },
    });
    expect(setValue.ok()).toBeTruthy();

    const finish = await page.request.post(`/api/scorm/session/${sessionId}/finish`, {
      data: { exit: 'normal' },
    });
    expect(finish.ok()).toBeTruthy();
    const finishBody = (await finish.json()) as { success: boolean; status: string };
    expect(finishBody.success).toBeTruthy();
    expect(finishBody.status).toBe('completed');

    await page.goto('/Account/Profile');
    const completedRow = page.locator('#completed-courses .profile-course-row', { hasText: SCORM_TITLE });
    await expect(completedRow).toBeVisible();
    await expect(completedRow.locator('.profile-course-status')).toHaveText('Completed');
    await expect(page.locator('#enrolled-courses .profile-course-title', { hasText: SCORM_TITLE })).toHaveCount(0);
  });

  test('a retake never moves a completed course out of Completed', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');

    // Start a NEW (in-progress) attempt for the already-completed course.
    await launchScorm(page, SCORM_COURSE_ID);

    await page.goto('/Account/Profile');
    await expect(page.locator('#completed-courses .profile-course-row', { hasText: SCORM_TITLE })).toBeVisible();
  });

  test('a user with no enrollments sees the empty state, not an error', async ({ page, context }) => {
    const email = `nocoursese2e+${Date.now()}@example.com`;
    const password = 'Sup3rSecret!x9';

    // Fresh verified account (the outbox pattern from verify-email.spec.ts).
    await page.goto('/Account/Signup');
    await page.getByLabel('Full name').fill('No Courses');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByLabel('Confirm password', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();

    const outbox = (await (await page.request.get('/api/dev/outbox')).json()) as Array<{ to: string; purpose: string; body: string }>;
    const verification = outbox.find((e) => e.to === email && e.purpose === 'Verification');
    expect(verification, 'a verification email should be in the dev outbox').toBeDefined();
    const match = verification!.body.match(/http:\/\/[^\s]+\/Account\/Verify\?token=[A-Za-z0-9_-]+/);
    expect(match).not.toBeNull();
    const verifyPage = await context.newPage();
    await verifyPage.goto(match![0]);
    await expect(verifyPage.getByRole('heading', { name: 'Your email is verified' })).toBeVisible();
    await verifyPage.close();

    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(
      (url) => url.pathname === '/' || url.pathname === '/Courses',
      { timeout: 10_000 }
    );

    await page.goto('/Account/Profile');
    await expect(page.locator('#courses-empty')).toHaveText("You haven't enrolled in any courses yet");
    await expect(page.locator('#courses-error')).toBeHidden();
    // Personal details render alongside the empty state.
    await expect(page.locator('.profile-card').getByText(email, { exact: true })).toBeVisible();
  });
});
