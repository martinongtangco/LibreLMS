import { test, expect, Page } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';
import * as net from 'node:net';

/**
 * Spec 031 US1 E2E — the REAL UI SCORM launch path (FR-005).
 *
 * The launch-page defect shipped because every prior SCORM test called the
 * launch API directly; this spec opens /Scorm/Launch/{courseId} in the
 * browser as the enrolled learner and asserts the page itself completes the
 * server-side launch call, renders the iframe, and records an attempt.
 *
 * Idempotent across runs (the DB persists): a stale active session is
 * recovered on demand by flushing the ephemeral Valkey store (never
 * preemptively — another spec may legitimately hold a session), and the test
 * finishes its own session at the end so it cannot block the next run.
 */

const SCORM_COURSE_ID = '11111111-1111-1111-1111-111111111111';

/** Shape of one entry in GET /api/scorm/attempts/my (camelCase JSON). */
interface AttemptDto {
  id: string;
  courseId: string;
  courseTitle: string;
  attemptNumber: number;
  status: string;
  startedAt: string;
}

/**
 * Clear the ephemeral SCORM session state in Valkey (keys: scorm:session:{id}).
 *
 * A stale ACTIVE session (30-minute TTL, and Valkey persists across app
 * restarts in this dev environment) blocks a fresh launch with "A session for
 * this course is already active". The UI test below therefore flushes its own
 * ephemeral store only when it hits that state — Valkey holds ONLY SCORM
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

/** The learner's attempts from GET /api/scorm/attempts/my. */
async function getMyAttempts(page: Page): Promise<AttemptDto[]> {
  const res = await page.request.get('/api/scorm/attempts/my');
  expect(res.ok(), `attempts API failed: ${await res.text().catch(() => '')}`).toBeTruthy();
  const body = (await res.json()) as { attempts: AttemptDto[] };
  return body.attempts;
}

test.describe('SCORM launch page — real UI path (spec 031 US1)', () => {
  // Serial: the tests share alice's session state in Valkey (an active
  // session blocks relaunch), so they must not interleave.
  test.describe.configure({ mode: 'serial' });

  test('UI launch renders the course iframe and records an attempt', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');

    // Count the learner's existing attempts for the seeded SCORM course.
    const before = (await getMyAttempts(page)).filter((a) => a.courseId === SCORM_COURSE_ID).length;

    // The real UI launch path — the server-side call the defect broke.
    await page.goto(`/Scorm/Launch/${SCORM_COURSE_ID}`);

    const errorPage = page.locator('.scorm-error-page');
    if ((await errorPage.isVisible()) && (await errorPage.textContent())?.includes('already active')) {
      // A stale active session from a previous run — flush the ephemeral
      // store and retry once (do NOT flush preemptively; another spec may
      // hold a session).
      await flushScormSessions();
      await page.goto(`/Scorm/Launch/${SCORM_COURSE_ID}`);
    }

    // US1.1: the success branch renders — iframe + status bar, no error page.
    await expect(errorPage).toBeHidden();
    const frame = page.locator('iframe.scorm-frame');
    await expect(frame).toBeVisible();
    await expect(frame).toHaveAttribute('src', /\/scorm-content\//);
    await expect(page.locator('.scorm-status-bar')).toBeVisible();
    await expect(page.locator('.scorm-status-bar')).toContainText('SCORM Session Active');

    // US1.2: exactly one new in-progress attempt for the course.
    const after = (await getMyAttempts(page)).filter((a) => a.courseId === SCORM_COURSE_ID);
    expect(after.length, 'the UI launch must record exactly one new attempt').toBe(before + 1);
    const latest = after.reduce((max, a) => (a.attemptNumber > max.attemptNumber ? a : max));
    expect(latest.status).toBe('in-progress');

    // Cleanup: finish the session the page created (id from the page's
    // script tag) so the active session cannot block other specs.
    // The launch page sets window.scormSessionId in a script tag; inside the
    // page context globalThis IS window (no DOM lib in this tsconfig).
    const sessionId = (await page.evaluate(() => (globalThis as any).scormSessionId)) as string | undefined;
    expect(typeof sessionId, 'the launch page must expose window.scormSessionId').toBe('string');
    expect(sessionId).not.toBe('');
    const finish = await page.request.post(`/api/scorm/session/${sessionId}/finish`, {
      data: { exit: 'normal' },
    });
    expect(finish.ok(), `finish failed: ${await finish.text().catch(() => '')}`).toBeTruthy();
  });

  test('launching a course the learner is not enrolled in shows the enrollment error', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');

    // Pick a course the learner is NOT enrolled in (deterministic enough:
    // the seeded catalog has far more courses than the learner is enrolled in).
    const coursesRes = await page.request.get('/api/courses');
    expect(coursesRes.ok()).toBeTruthy();
    const courses = ((await coursesRes.json()) as { courses: Array<{ id: string; title: string }> }).courses;

    const enrollmentsRes = await page.request.get('/api/enrollments/my');
    expect(enrollmentsRes.ok()).toBeTruthy();
    const enrollments = ((await enrollmentsRes.json()) as { enrollments: Array<{ courseId: string }> }).enrollments;
    const enrolledIds = new Set(enrollments.map((e) => e.courseId));

    const notEnrolled = courses.find((c) => !enrolledIds.has(c.id));
    expect(notEnrolled, 'seeded catalog must contain a course the learner is not enrolled in').toBeDefined();

    await page.goto(`/Scorm/Launch/${notEnrolled!.id}`);

    // US1.3 (FR-003): the API's 403 maps to the specific enrollment message,
    // not the generic failure.
    const errorPage = page.locator('.scorm-error-page');
    await expect(errorPage).toBeVisible();
    await expect(errorPage.locator('p')).toHaveText('You are not enrolled in this course.');
  });
});
