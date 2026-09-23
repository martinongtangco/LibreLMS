import { test, expect, Page } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';
import * as net from 'node:net';

/**
 * Spec 050 US2 E2E — SCORM session API authentication + ownership, over real
 * HTTP.
 *
 * Before the fix these endpoints were anonymous and unowned: any caller with
 * a session GUID could read/write another learner's CMI data. After the fix:
 *  - unauthenticated callers get 401 on the four session endpoints
 *  - a different authenticated learner gets 403 (JSON, not a redirect)
 *  - the owner's normal flow (setValue/getValue/commit/finish) still works
 *  - the api.js shim stays anonymous (static script text, no session data)
 *
 * The launch call reuses the valkey-flush recovery pattern from
 * 15-scorm-launch-ui (a stale active session blocks a fresh launch).
 */

const SCORM_COURSE_ID = '11111111-1111-1111-1111-111111111111';

/**
 * Clear the ephemeral SCORM session state in Valkey (keys: scorm:session:{id}).
 * Only needed when a stale active session blocks a fresh launch. Valkey holds
 * ONLY SCORM runtime state in this project, so FLUSHALL is safe (same helper
 * as 15-scorm-launch-ui).
 */
async function flushScormSessions(): Promise<void> {
  // Follow the app's own connection string: inside compose this resolves to
  // valkey:6379, but on a bare CI runner the service is published on
  // localhost:6379 and the "valkey" hostname does not resolve at all.
  const [valkeyHost, valkeyPort] = (process.env.ConnectionStrings__Valkey ?? 'valkey:6379').split(':');

  await new Promise<void>((resolve, reject) => {
    const socket = net.connect(Number(valkeyPort ?? 6379), valkeyHost, () => {
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

/** Log in as the second seeded learner (bob) — authFixture only covers roles. */
async function loginAsBob(page: Page): Promise<void> {
  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill('bob@example.com');
  await page.getByLabel('Password').fill('password123');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(
    (url) => url.pathname === '/' || url.pathname.includes('/Courses'),
    { timeout: 10_000 }
  );
}

/** Launch a SCORM session as the caller of this context; returns the sessionId. */
async function launchSession(request: {
  post(url: string): Promise<{ status(): number; ok(): boolean; json(): Promise<any>; text(): Promise<string> }>;
}): Promise<string> {
  let res = await request.post(`/api/scorm/${SCORM_COURSE_ID}/launch`);
  if (!res.ok()) {
    const body = await res.text().catch(() => '');
    expect(body.includes('already active'), `launch failed unexpectedly: ${res.status()} ${body}`).toBeTruthy();
    // Stale active session from a previous run — flush and retry once.
    await flushScormSessions();
    res = await request.post(`/api/scorm/${SCORM_COURSE_ID}/launch`);
  }
  expect(res.ok(), `launch must succeed: ${await res.text().catch(() => '')}`).toBeTruthy();
  const body = await res.json();
  return body.sessionId as string;
}

test.describe('SCORM session API — authentication + ownership (spec 050 US2)', () => {
  test.describe.configure({ mode: 'serial' });

  test('intruder gets 403 and unauthenticated gets 401 on all four endpoints', async ({ page, browser }) => {
    // alice (Learner) launches the seeded SCORM course — owns the session.
    await authFixture.loginAs(page, 'Learner');
    const sessionId = await launchSession(page.request);
    expect(sessionId).toMatch(/^[0-9a-f-]{36}$/i);

    // bob — a different authenticated learner — gets a JSON 403 on all four.
    const bobCtx = await browser.newContext();
    const bob = await bobCtx.newPage();
    await loginAsBob(bob);

    const setRes = await bob.request.post(`/api/scorm/session/${sessionId}/setValue`, {
      data: { element: 'cmi.core.lesson_status', value: 'passed' },
    });
    expect(setRes.status(), 'intruder setValue must be 403').toBe(403);

    const getRes = await bob.request.get(`/api/scorm/session/${sessionId}/getValue?element=cmi.core.lesson_status`);
    expect(getRes.status(), 'intruder getValue must be 403').toBe(403);

    const commitRes = await bob.request.post(`/api/scorm/session/${sessionId}/commit`);
    expect(commitRes.status(), 'intruder commit must be 403').toBe(403);

    const finishRes = await bob.request.post(`/api/scorm/session/${sessionId}/finish`, {
      data: { exit: 'normal' },
    });
    expect(finishRes.status(), 'intruder finish must be 403').toBe(403);

    // The 403 must be a JSON body (Results.Forbid() would 302 under cookie auth).
    expect((await setRes.json()).errorCode).toBe('403');

    // Unauthenticated — a fresh context with no login — gets 401 on all four.
    const anonCtx = await browser.newContext();
    const anon = await anonCtx.newPage();

    const anonSet = await anon.request.post(`/api/scorm/session/${sessionId}/setValue`, {
      data: { element: 'cmi.core.lesson_status', value: 'passed' },
    });
    expect(anonSet.status(), 'unauthenticated setValue must be 401').toBe(401);

    // Cookie auth challenges an unauthenticated GET with a 302 to the login
    // page (and POST/PUT/DELETE with a 401) — either way the handler is never
    // reached and no session data is served. Assert the raw challenge.
    const anonGet = await anon.request.get(`/api/scorm/session/${sessionId}/getValue?element=cmi.core.lesson_status`, { maxRedirects: 0 });
    expect([302, 401], 'unauthenticated getValue must be challenged (302-to-login or 401), never 200').toContain(anonGet.status());
    expect(anonGet.headers()['location'] ?? '').toContain('/Account/Login');

    // Bodyless POSTs are challenged with a 302-to-login (same rule as GET);
    // JSON-body POSTs get a plain 401. Assert the raw challenge.
    const anonCommit = await anon.request.post(`/api/scorm/session/${sessionId}/commit`, { maxRedirects: 0 });
    expect([302, 401], 'unauthenticated commit must be challenged (302-to-login or 401), never 200').toContain(anonCommit.status());

    const anonFinish = await anon.request.post(`/api/scorm/session/${sessionId}/finish`, {
      data: { exit: 'normal' },
    });
    expect(anonFinish.status(), 'unauthenticated finish must be 401').toBe(401);

    await bobCtx.close();
    await anonCtx.close();

    // Cleanup: the owner finishes the session (also proves the owner path).
    const finish = await page.request.post(`/api/scorm/session/${sessionId}/finish`, {
      data: { exit: 'normal' },
    });
    expect(finish.status(), 'owner finish must succeed').toBe(200);
  });

  test('owner flow works end to end and api.js stays anonymous', async ({ page, browser }) => {
    await authFixture.loginAs(page, 'Learner');
    const sessionId = await launchSession(page.request);

    // Owner setValue → 200, getValue round-trips.
    const set = await page.request.post(`/api/scorm/session/${sessionId}/setValue`, {
      data: { element: 'cmi.core.lesson_status', value: 'completed' },
    });
    expect(set.status(), `owner setValue must be 200: ${await set.text().catch(() => '')}`).toBe(200);

    const get = await page.request.get(`/api/scorm/session/${sessionId}/getValue?element=cmi.core.lesson_status`);
    expect(get.status()).toBe(200);
    expect((await get.json()).value).toBe('completed');

    // The api.js shim stays anonymous — no session data, just script text.
    const anonCtx = await browser.newContext();
    const shimRes = await anonCtx.request.get(`/api/scorm/session/${sessionId}/api.js`);
    expect(shimRes.status(), 'the api.js shim must stay anonymous (200)').toBe(200);
    expect(shimRes.headers()['content-type']).toContain('javascript');
    await anonCtx.close();

    // Owner commit + finish (doubles as cleanup).
    const commit = await page.request.post(`/api/scorm/session/${sessionId}/commit`);
    expect(commit.status(), `owner commit must be 200: ${await commit.text().catch(() => '')}`).toBe(200);

    const finish = await page.request.post(`/api/scorm/session/${sessionId}/finish`, {
      data: { exit: 'normal' },
    });
    expect(finish.status(), `owner finish must be 200: ${await finish.text().catch(() => '')}`).toBe(200);
  });
});
