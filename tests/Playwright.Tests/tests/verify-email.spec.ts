import { test, expect } from '@playwright/test';

/**
 * Spec 027 US2 E2E: email verification.
 *
 * Flow: sign up (unverified) → sign-in blocked + resend → open the verification
 * link from the dev outbox → sign-in works → the same link is rejected as used.
 * Invalid / missing / tampered links are rejected without any account state change.
 *
 * Each test uses its own fresh account (module-level run suffix is unique per run).
 *
 * The "expired link" state (24 h lifetime) requires DB time manipulation, which is
 * out of reach from this E2E environment (no SQL driver in the test project) — see
 * tasks.md T045 notes; the Expired branch shares its lookup path with the
 * already-used/invalid cases which are covered here.
 */

const run = Date.now();
const password = 'Sup3rSecret!x9';

async function signUp(page: import('@playwright/test').Page, name: string, email: string): Promise<void> {
  await page.goto('/Account/Signup');
  await page.getByLabel('Full name').fill(name);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Confirm password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();
}

/** Fetch the newest outbox entry for the given email+purpose and extract its link. */
async function getVerifyLink(
  request: import('@playwright/test').APIRequestContext,
  email: string,
): Promise<string> {
  const response = await request.get('/api/dev/outbox');
  expect(response.ok()).toBeTruthy();
  const emails: Array<{ to: string; purpose: string; body: string }> = await response.json();
  const mine = emails.filter((e) => e.to === email && e.purpose === 'Verification');
  expect(mine.length).toBeGreaterThan(0);
  const newest = mine[0]; // outbox is newest-first
  const match = newest.body.match(/http:\/\/[^\s]+\/Account\/Verify\?token=[A-Za-z0-9_-]+/);
  expect(match).not.toBeNull();
  return match![0];
}

test.describe('Email verification (spec 027 US2)', () => {
  test('unverified sign-in is blocked and resend produces a new link', async ({ page }) => {
    const email = `verifye2e+${run}+block@example.com`;
    await signUp(page, 'Verify Block', email);

    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Blocked: still on the login page with the unverified message + resend button.
    await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible();
    await expect(page.getByText('Your email address has not been verified')).toBeVisible();

    // Resend works: confirmation + a fresh Verification email in the outbox.
    const before = (await (await page.request.get('/api/dev/outbox')).json()) as Array<{ to: string; purpose: string }>;
    const countBefore = before.filter((e) => e.to === email && e.purpose === 'Verification').length;

    await page.getByRole('button', { name: 'Resend verification email' }).click();
    // String (not regex) match: the email contains '+' which would break a regex.
    await expect(page.getByText(`verification email has been sent to ${email}`)).toBeVisible();

    const after = (await (await page.request.get('/api/dev/outbox')).json()) as Array<{ to: string; purpose: string }>;
    const countAfter = after.filter((e) => e.to === email && e.purpose === 'Verification').length;
    expect(countAfter).toBe(countBefore + 1);
  });

  test('verification link verifies the account, then sign-in succeeds; the link is single-use', async ({ page, context }) => {
    const email = `verifye2e+${run}+verify@example.com`;
    await signUp(page, 'Verify Ok', email);

    // Open the verification link from the outbox.
    const link = await getVerifyLink(page.request, email);
    const verifyPage = await context.newPage();
    await verifyPage.goto(link);
    await expect(verifyPage.getByRole('heading', { name: 'Your email is verified' })).toBeVisible();
    await verifyPage.close();

    // Sign-in now works and leaves the login page (learner lands on /Courses via /).
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => url.pathname === '/' || url.pathname === '/Courses');
    expect(page.url()).not.toContain('/Account/Login');

    // Reusing the SAME link → "already used" (single-use, FR-016).
    const reusePage = await context.newPage();
    await reusePage.goto(link);
    await expect(reusePage.getByRole('heading', { name: 'Link already used' })).toBeVisible();
    await reusePage.close();
  });

  test('missing, invalid and tampered links are rejected without account state change', async ({ page, context }) => {
    const email = `verifye2e+${run}+tamper@example.com`;
    await signUp(page, 'Verify Tamper', email);

    const link = await getVerifyLink(page.request, email);
    const token = link.split('token=')[1];
    const tampered = token.slice(0, -2) + (token[0] === 'A' ? 'BB' : 'AA');

    const cases: Array<{ label: string; url: string }> = [
      { label: 'missing token', url: '/Account/Verify' },
      { label: 'invalid token', url: '/Account/Verify?token=not-a-real-token' },
      { label: 'tampered token', url: `/Account/Verify?token=${tampered}` },
    ];

    for (const c of cases) {
      const p = await context.newPage();
      await p.goto(c.url);
      await expect(p.getByRole('heading', { name: 'Link missing or invalid' })).toBeVisible();
      await p.close();
    }

    // No account state change: the real link still works after the bad attempts.
    const p2 = await context.newPage();
    await p2.goto(link);
    await expect(p2.getByRole('heading', { name: 'Your email is verified' })).toBeVisible();
    await p2.close();
  });

  test('expired link shows "Link expired" and does not verify the account', async ({ page, context }) => {
    // The 24 h expiry cannot be reached without backdating the DB row, and this E2E
    // environment has no SQL driver — skip with a reason (see tasks.md T045 notes).
    test.skip(true, 'expired link (24 h lifetime) needs DB time manipulation; no SQL driver in the E2E environment');

    const email = `verifye2e+${run}+expired@example.com`;
    await signUp(page, 'Verify Expired', email);
    const link = await getVerifyLink(page.request, email);
    const p = await context.newPage();
    await p.goto(link);
    await expect(p.getByRole('heading', { name: 'Link expired' })).toBeVisible();
    await p.close();
  });
});
