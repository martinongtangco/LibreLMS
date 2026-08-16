import { test, expect, Page, APIRequestContext } from '@playwright/test';

/**
 * Spec 030 US1 negative flow (FR-002, SC-002) — the verification gate.
 *
 * Written FIRST per TDD. Uses a FRESH per-run account (unique email — the
 * established pattern in verify-email.spec.ts) so the seeded accounts'
 * verified state is never disturbed: the DB persists between runs and the
 * full suite runs in parallel, so a stranded unverified seeded account would
 * break the other profile specs.
 *
 * Flow: sign up → verify → sign in → /Dev/Unverify flips the (signed-in)
 * account unverified → name save refused (banner + resend, nav unchanged) →
 * resend produces a fresh link in the dev outbox → verify via the link →
 * re-sign-in → the name save now succeeds (gate passed).
 */

const run = Date.now();
const password = 'Sup3rSecret!x9';
const email = `gatee2e+${run}@example.com`;

async function signUp(page: Page, name: string): Promise<void> {
  await page.goto('/Account/Signup');
  await page.getByLabel('Full name').fill(name);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Confirm password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();
}

async function signIn(page: Page): Promise<void> {
  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(
    (url) => url.pathname === '/' || url.pathname === '/Courses',
    { timeout: 10_000 }
  );
}

/** Newest verification link for the account from the dev outbox (newest-first). */
async function latestVerifyLink(request: APIRequestContext): Promise<string | null> {
  const response = await request.get('/api/dev/outbox');
  const emails: Array<{ to: string; purpose: string; body: string }> = await response.json();
  const mine = emails.filter((e) => e.to === email && e.purpose === 'Verification');
  if (mine.length === 0) return null;
  const match = mine[0].body.match(/http:\/\/[^\s]+\/Account\/Verify\?token=[A-Za-z0-9_-]+/);
  return match ? match[0] : null;
}

async function openVerifyLink(page: Page, context: import('@playwright/test').BrowserContext): Promise<void> {
  const link = await latestVerifyLink(page.request);
  expect(link, 'a verification link should be present in the dev outbox').not.toBeNull();
  const verifyPage = await context.newPage();
  await verifyPage.goto(link!);
  await expect(verifyPage.getByRole('heading', { name: 'Your email is verified' })).toBeVisible();
  await verifyPage.close();
}

test.describe('Profile — verification gate (spec 030 US1 negative, SC-002)', () => {
  test('unverified account: name save refused with working resend; after re-verification the save succeeds', async ({ page, context }) => {
    await signUp(page, 'Gate Test');

    // Verify so the account can sign in (spec 027 FR-011).
    await openVerifyLink(page, context);
    await signIn(page);

    // /Dev/Unverify flips the signed-in account unverified (R7 dev toggle).
    await page.goto(`/Dev/Unverify?email=${encodeURIComponent(email)}`);
    await expect(page.locator('#unverify-result')).toHaveText(`unverified ${email}`);

    // The gate: name save refused, banner + resend shown, nav name unchanged.
    await page.goto('/Account/Profile');
    await expect(page.locator('#verification-banner')).toBeVisible();
    await expect(page.locator('#verification-banner')).toContainText('A verified email is required to save changes.');
    await expect(page.locator('#resend-verification-btn')).toBeVisible();

    await page.locator('#profile-name-input').fill('Gate Should Not Save');
    await page.locator('#save-name-btn').click();

    await expect(page.locator('#profile-success')).toBeHidden();
    await expect(page.locator('#verification-banner')).toBeVisible();
    await expect(page.locator('.account-name')).toHaveText('Gate Test');

    // Resend works: neutral confirmation + a fresh Verification email in the outbox.
    const before = (await (await page.request.get('/api/dev/outbox')).json()) as Array<{ to: string; purpose: string }>;
    const countBefore = before.filter((e) => e.to === email && e.purpose === 'Verification').length;

    await page.locator('#resend-verification-btn').click();
    await expect(page.locator('#resend-message')).toHaveText(`A verification email has been sent to ${email}.`);

    const after = (await (await page.request.get('/api/dev/outbox')).json()) as Array<{ to: string; purpose: string }>;
    const countAfter = after.filter((e) => e.to === email && e.purpose === 'Verification').length;
    expect(countAfter).toBe(countBefore + 1);

    // Re-verify via the outbox link, then re-sign-in (the scenario's step 6).
    await openVerifyLink(page, context);

    await page.goto('/Account/Logout');
    await page.waitForURL(
      (url) => url.pathname.includes('/Account/Login') || url.pathname === '/',
      { timeout: 10_000 }
    );
    await signIn(page);

    // Gate passed: a name save now succeeds and the banner is gone.
    await page.goto('/Account/Profile');
    await expect(page.locator('#verification-banner')).toBeHidden();
    await page.locator('#profile-name-input').fill('Gate Tester');
    await page.locator('#save-name-btn').click();
    await expect(page.locator('#profile-success')).toHaveText('Profile updated.');
    await expect(page.locator('.account-name')).toHaveText('Gate Tester');
  });
});
