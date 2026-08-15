import { test, expect, type Page } from '@playwright/test';

/**
 * Sign-up validation tests (spec 027 US1 — self-service registration).
 *
 * POST /Account/Signup re-validates server-side and re-renders the form with
 * inline field errors (div.field-error under each field) or a general error
 * (div.error-message). These tests assert the exact server messages for every
 * rejection, plus the per-email throttle (10 sign-up attempts / 24 h).
 *
 * Every test uses a unique email derived from a per-run timestamp so the
 * per-email throttle can never leak between tests or between runs.
 */

const run = Date.now();

/** A password that satisfies the strict policy: 14 chars, upper, lower, digit. */
const STRONG_PASSWORD = 'Sup3rSecret!x9';

/** A valid full name that appears in no password used below. */
const NAME = 'Test Person';

/** Go to /Account/Signup, fill the four labeled fields, and click 'Create account'. */
async function submit(
  page: Page,
  fields: { name: string; email: string; password: string; confirm: string },
  options: { bypassNativeValidation?: boolean } = {},
): Promise<void> {
  await page.goto('/Account/Signup');
  await page.getByLabel('Full name', { exact: true }).fill(fields.name);
  await page.getByLabel('Email', { exact: true }).fill(fields.email);
  await page.getByLabel('Password', { exact: true }).fill(fields.password);
  await page.getByLabel('Confirm password', { exact: true }).fill(fields.confirm);
  if (options.bypassNativeValidation) {
    // The Email input is type="email": a submit-button click would be blocked by the
    // browser's native constraint validation, so call form.submit() directly to let
    // the server-side format check produce its error. form.submit() triggers a real
    // navigation, which destroys the evaluate context — that rejection is expected.
    await page
      .locator('form')
      .evaluate((form) => (form as HTMLFormElement).submit())
      .catch(() => undefined);
  } else {
    await page.getByRole('button', { name: 'Create account' }).click();
  }
}

test.describe('Sign-up validation (spec 027 US1)', () => {
  test('duplicate email is rejected (case-insensitive)', async ({ page }) => {
    const email = `dup+${run}@example.com`;

    // First sign-up with a valid strong password succeeds.
    await submit(page, { name: NAME, email, password: STRONG_PASSWORD, confirm: STRONG_PASSWORD });
    await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();

    // Resubmitting the same email uppercased is rejected on the Email field
    // and the form is re-rendered (not the success screen).
    await submit(page, {
      name: NAME,
      email: email.toUpperCase(),
      password: STRONG_PASSWORD,
      confirm: STRONG_PASSWORD,
    });
    await expect(page.getByRole('heading', { name: 'Create account', exact: true })).toBeVisible();
    await expect(page.locator('.field-error', { hasText: 'Email already in use.' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('password shorter than 12 characters is rejected', async ({ page }) => {
    // 11 chars; otherwise policy-compliant (upper, lower, digit, not blocklisted).
    await submit(page, {
      name: NAME,
      email: `short+${run}@example.com`,
      password: 'Sup3rSecr3t',
      confirm: 'Sup3rSecr3t',
    });
    await expect(
      page.locator('.field-error', { hasText: 'Password must be at least 12 characters.' }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('password without an uppercase letter is rejected', async ({ page }) => {
    await submit(page, {
      name: NAME,
      email: `lowercase+${run}@example.com`,
      password: 'sup3rsecret!x9',
      confirm: 'sup3rsecret!x9',
    });
    await expect(
      page.locator('.field-error', {
        hasText: 'Password must contain at least one uppercase letter.',
      }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('password without a digit is rejected', async ({ page }) => {
    // 12 chars with upper and lower, but no digit.
    await submit(page, {
      name: NAME,
      email: `nodigit+${run}@example.com`,
      password: 'SuprSecret!x',
      confirm: 'SuprSecret!x',
    });
    await expect(
      page.locator('.field-error', { hasText: 'Password must contain at least one digit.' }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('password containing the full name is rejected', async ({ page }) => {
    // 'test person' appears in the password (case-insensitive match).
    await submit(page, {
      name: NAME,
      email: `namepw+${run}@example.com`,
      password: 'Sup3rtest personx9',
      confirm: 'Sup3rtest personx9',
    });
    await expect(
      page.locator('.field-error', { hasText: 'Password must not contain your full name.' }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('password containing the email address is rejected', async ({ page }) => {
    const email = `emailpw+${run}@example.com`;
    await submit(page, {
      name: NAME,
      email,
      password: `Sup3r${email}x9`,
      confirm: `Sup3r${email}x9`,
    });
    await expect(
      page.locator('.field-error', { hasText: 'Password must not contain your email address.' }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('blocklisted common password is rejected', async ({ page }) => {
    await submit(page, {
      name: NAME,
      email: `common+${run}@example.com`,
      password: 'Password12345',
      confirm: 'Password12345',
    });
    await expect(
      page.locator('.field-error', { hasText: 'Password is too common. Choose a different one.' }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('password confirmation mismatch is rejected', async ({ page }) => {
    await submit(page, {
      name: NAME,
      email: `mismatch+${run}@example.com`,
      password: STRONG_PASSWORD,
      confirm: 'Sup3rSecret!y9',
    });
    await expect(
      page.locator('.field-error', { hasText: 'Passwords do not match.' }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('malformed email is rejected', async ({ page }) => {
    // bypassNativeValidation: the browser's type="email" check would block the
    // submission client-side; the server-side format check must be exercised.
    await submit(
      page,
      {
        name: NAME,
        email: 'not-an-email',
        password: STRONG_PASSWORD,
        confirm: STRONG_PASSWORD,
      },
      { bypassNativeValidation: true },
    );
    await expect(
      page.locator('.field-error', { hasText: 'Enter a valid email address.' }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });

  test('the 11th sign-up attempt within 24h is throttled', async ({ page }) => {
    const fields = {
      name: NAME,
      email: `throttle+${run}@example.com`,
      password: STRONG_PASSWORD,
      confirm: STRONG_PASSWORD,
    };

    // Attempt 1 succeeds (account created).
    await submit(page, fields);
    await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();

    // Attempts 2-10 are duplicates of the just-created account.
    for (let attempt = 2; attempt <= 10; attempt++) {
      await submit(page, fields);
      await expect(page.locator('.field-error', { hasText: 'Email already in use.' })).toBeVisible();
    }

    // Attempt 11 hits the 10/24h per-email throttle (checked before validation)
    // and shows the general error instead of any field error.
    await submit(page, fields);
    await expect(
      page.locator('.error-message', {
        hasText: 'Too many sign-up attempts for this email. Please try again later.',
      }),
    ).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Check your email' })).not.toBeVisible();
  });
});
