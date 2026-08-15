import { test, expect } from '@playwright/test';

/**
 * Self-service sign-up tests (spec 027 — User Story 1).
 *
 * Verifies the sign-up form creates an unverified account (no auto sign-in)
 * and that the dev outbox records verification + welcome emails, including
 * a single-use verification link in the verification body.
 */

interface OutboxEmail {
  to: string;
  purpose: string;
  subject: string;
  body: string;
  sentAtUtc: string;
}

const run = Date.now();
const email = `newlearner+${run}@example.com`;

test.describe('Self-service sign-up (spec 027 US1)', () => {
  test('creates an unverified account and records verification + welcome emails', async ({
    page,
    request,
  }) => {
    // Fill the sign-up form
    await page.goto('/Account/Signup');
    await page.getByLabel('Full name').fill('Test Learner');
    await page.getByLabel('Email').fill(email);
    // exact: true — the 'Confirm password' label also contains 'Password'
    await page.getByLabel('Password', { exact: true }).fill('Sup3rSecret!x9');
    await page.getByLabel('Confirm password', { exact: true }).fill('Sup3rSecret!x9');
    await page.getByRole('button', { name: 'Create account' }).click();

    // Confirmation screen shows and the user is NOT signed in
    await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();
    await expect(page.locator('#account-control')).toBeHidden();

    // The dev outbox (Development-only) records both emails for this address
    const response = await request.get('/api/dev/outbox');
    expect(response.ok()).toBeTruthy();
    const emails = (await response.json()) as OutboxEmail[];
    const mine = emails.filter((e) => e.to === email);

    const verification = mine.find((e) => e.purpose === 'Verification');
    expect(verification, 'expected a Verification email for the new account in the dev outbox').toBeTruthy();
    const welcome = mine.find((e) => e.purpose === 'Welcome');
    expect(welcome, 'expected a Welcome email for the new account in the dev outbox').toBeTruthy();

    // The verification body contains the single-use verification link
    const verifyLink = verification?.body.match(/http:\/\/[^\s]+\/Account\/Verify\?token=[A-Za-z0-9_-]+/);
    expect(verifyLink, 'expected a verify link in the Verification email body').toBeTruthy();
  });

  test('shows the policy hints before submit', async ({ page }) => {
    // Navigate to the sign-up page without submitting
    await page.goto('/Account/Signup');

    // The password policy hints are visible before submit
    const hints = page.locator('.password-hints');
    await expect(hints.getByText('At least 12 characters')).toBeVisible();
    await expect(hints.getByText('commonly used password')).toBeVisible();
  });
});
