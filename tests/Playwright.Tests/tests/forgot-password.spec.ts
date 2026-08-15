import { test, expect, Page } from '@playwright/test';

/**
 * Spec 027 US3 E2E: forgot / reset password + SecurityStamp session invalidation.
 *
 * Flow: request reset (neutral confirmation + outbox email) → reset via link →
 * new password works, old fails, link is single-use, and a pre-existing signed-in
 * browser context is signed out (FR-017/FR-018, via the cookie SecurityStamp
 * re-validation). Unregistered emails get the identical confirmation and no outbox
 * entry (no enumeration). The 6th request within an hour is throttled.
 *
 * Each test uses its own fresh account (module-level run suffix is unique per run;
 * per-test suffix keeps accounts independent — test 1 changes its account's password).
 *
 * The "expired link" (30 min lifetime) state needs DB time manipulation and is out
 * of reach here (no SQL driver in the test project) — see the analogous note in
 * verify-email.spec.ts / tasks.md T045.
 */

const run = Date.now();
const oldPassword = 'Sup3rSecret!x9';
const newPassword = 'T0talNew!pass99';

const emailFor = (suffix: string) => `reset+${run}+${suffix}@example.com`;

async function signUp(page: Page, name: string, email: string): Promise<void> {
  await page.goto('/Account/Signup');
  await page.getByLabel('Full name').fill(name);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(oldPassword);
  await page.getByLabel('Confirm password', { exact: true }).fill(oldPassword);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();
}

async function verifyAccount(page: Page, email: string): Promise<void> {
  const response = await page.request.get('/api/dev/outbox');
  const emails: Array<{ to: string; purpose: string; body: string }> = await response.json();
  const mine = emails.find((e) => e.to === email && e.purpose === 'Verification');
  expect(mine, 'verification email present').toBeTruthy();
  const link = mine!.body.match(/http:\/\/[^\s]+\/Account\/Verify\?token=[A-Za-z0-9_-]+/)!;
  const p = await page.context().newPage();
  await p.goto(link[0]);
  await expect(p.getByRole('heading', { name: 'Your email is verified' })).toBeVisible();
  await p.close();
}

/** Submit the forgot-password form; returns the neutral confirmation text. */
async function requestReset(page: Page, targetEmail: string): Promise<string> {
  await page.goto('/Account/ForgotPassword');
  await page.getByLabel('Email').fill(targetEmail);
  await page.getByRole('button', { name: 'Send reset link' }).click();
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();
  const para = page.locator('p', { hasText: 'If the email is registered' }).first();
  await expect(para).toBeVisible();
  return (await para.textContent()) ?? '';
}

async function countOutbox(page: Page, purpose: string, to: string): Promise<number> {
  const response = await page.request.get('/api/dev/outbox');
  const emails: Array<{ to: string; purpose: string }> = await response.json();
  return emails.filter((e) => e.purpose === purpose && e.to === to).length;
}

async function getResetLink(page: Page, email: string): Promise<string> {
  const response = await page.request.get('/api/dev/outbox');
  const emails: Array<{ to: string; purpose: string; body: string }> = await response.json();
  const mine = emails.filter((e) => e.to === email && e.purpose === 'PasswordReset');
  expect(mine.length).toBeGreaterThan(0);
  return mine[0].body.match(/http:\/\/[^\s]+\/Account\/ResetPassword\?token=[A-Za-z0-9_-]+/)![0];
}

async function performReset(page: Page, link: string, password: string): Promise<void> {
  await page.goto(link);
  await expect(page.getByRole('heading', { name: 'Choose a new password' })).toBeVisible();
  await page.getByLabel('New password', { exact: true }).fill(password);
  await page.getByLabel('Confirm new password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Update password' }).click();
  await expect(page.getByRole('heading', { name: 'Password updated' })).toBeVisible();
}

test.describe('Forgot password (spec 027 US3)', () => {
  test('full reset cycle: neutral request, link reset, old password dead, single-use link, session killed', async ({ page, browser }) => {
    const email = emailFor('cycle');
    await signUp(page, 'Reset Cycle', email);
    await verifyAccount(page, email);

    // Context A: signed in BEFORE the reset (must be killed by the stamp rotation).
    const contextA = await browser.newContext();
    const pageA = await contextA.newPage();
    await pageA.goto('/Account/Login');
    await pageA.getByLabel('Email').fill(email);
    await pageA.getByLabel('Password').fill(oldPassword);
    await pageA.getByRole('button', { name: 'Sign In' }).click();
    await pageA.waitForURL((url) => url.pathname === '/' || url.pathname === '/Courses');
    const aliveBefore = await pageA.request.get('/Account/Profile', { maxRedirects: 0 });
    expect(aliveBefore.status()).toBe(200);

    // Context B (this test's page): signed-out request → neutral + outbox entry.
    const confirmation = await requestReset(page, email);
    expect(confirmation).toContain('If the email is registered, a password-reset email has been sent');
    expect(await countOutbox(page, 'PasswordReset', email)).toBe(1);

    // Reset via the outbox link.
    const link = await getResetLink(page, email);
    await performReset(page, link, newPassword);

    // FR-017/FR-018: context A's pre-existing session is now dead (redirect, not 200).
    const deadAfter = await pageA.request.get('/Account/Profile', { maxRedirects: 0 });
    expect(deadAfter.status()).toBe(302);
    expect(deadAfter.headers()['location']).toContain('/Account/Login');

    // Old password fails; new password works (fresh context C).
    const contextC = await browser.newContext();
    const pageC = await contextC.newPage();
    await pageC.goto('/Account/Login');
    await pageC.getByLabel('Email').fill(email);
    await pageC.getByLabel('Password').fill(oldPassword);
    await pageC.getByRole('button', { name: 'Sign In' }).click();
    await expect(pageC.getByText('Invalid email or password.')).toBeVisible();

    await pageC.getByLabel('Password').fill(newPassword);
    await pageC.getByRole('button', { name: 'Sign In' }).click();
    await pageC.waitForURL((url) => url.pathname === '/' || url.pathname === '/Courses');
    expect(pageC.url()).not.toContain('/Account/Login');

    // Single-use: the same reset link is now rejected.
    const reuse = await contextC.newPage();
    await reuse.goto(link);
    await expect(reuse.getByRole('heading', { name: 'Link already used' })).toBeVisible();

    await contextA.close();
    await contextC.close();
  });

  test('unregistered email gets the identical confirmation and no outbox entry', async ({ page }) => {
    const ghost = emailFor('ghost');
    const message = await requestReset(page, ghost);
    expect(message).toBe('If the email is registered, a password-reset email has been sent.');
    expect(await countOutbox(page, 'PasswordReset', ghost)).toBe(0);
  });

  test('reset form rejects a policy-violating password and keeps the token usable', async ({ page }) => {
    const email = emailFor('policy');
    await signUp(page, 'Reset Policy', email);
    await verifyAccount(page, email);
    await requestReset(page, email);
    const link = await getResetLink(page, email);

    await page.goto(link);
    await page.getByLabel('New password', { exact: true }).fill('short1A');
    await page.getByLabel('Confirm new password', { exact: true }).fill('short1A');
    await page.getByRole('button', { name: 'Update password' }).click();
    await expect(page.getByText('Password must be at least 12 characters.')).toBeVisible();

    // Token was NOT consumed: a good password still works on the same link.
    await page.getByLabel('New password', { exact: true }).fill(newPassword);
    await page.getByLabel('Confirm new password', { exact: true }).fill(newPassword);
    await page.getByRole('button', { name: 'Update password' }).click();
    await expect(page.getByRole('heading', { name: 'Password updated' })).toBeVisible();
  });

  test('missing and bogus reset links are rejected; used link rejected after reset', async ({ page }) => {
    const missing = await page.context().newPage();
    await missing.goto('/Account/ResetPassword');
    await expect(missing.getByRole('heading', { name: 'Link missing or invalid' })).toBeVisible();
    await missing.close();

    const bogus = await page.context().newPage();
    await bogus.goto('/Account/ResetPassword?token=definitely-not-real');
    await expect(bogus.getByRole('heading', { name: 'Link missing or invalid' })).toBeVisible();
    await bogus.close();

    // Used: request, consume, then reuse.
    const email = emailFor('used');
    await signUp(page, 'Reset Used', email);
    await verifyAccount(page, email);
    await requestReset(page, email);
    const link = await getResetLink(page, email);
    await performReset(page, link, newPassword);

    const reuse = await page.context().newPage();
    await reuse.goto(link);
    await expect(reuse.getByRole('heading', { name: 'Link already used' })).toBeVisible();
    await reuse.close();
  });

  test('the 6th reset request within an hour is throttled', async ({ page }) => {
    const email = emailFor('throttle');
    await signUp(page, 'Reset Throttle', email);
    await verifyAccount(page, email);

    // Requests 1–5 are accepted (each issues a fresh link).
    for (let i = 1; i <= 5; i++) {
      const message = await requestReset(page, email);
      expect(message, `request ${i}`).toContain('a password-reset email has been sent');
    }
    expect(await countOutbox(page, 'PasswordReset', email)).toBe(5);

    // Request 6 → throttled (still neutral about registration) and no new email.
    const throttled = await requestReset(page, email);
    expect(throttled).toContain('Please wait before requesting another');
    expect(await countOutbox(page, 'PasswordReset', email)).toBe(5);
  });
});
