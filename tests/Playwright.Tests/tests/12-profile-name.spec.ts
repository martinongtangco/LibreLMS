import { test, expect, type BrowserContext } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';
import { testUsers } from '../utils/testUsers';

/**
 * Spec 030 US1 E2E — editable display name (FR-001/FR-003/FR-004, SC-001).
 *
 * Written FIRST per TDD against the profile page markup contract in
 * specs/030-editable-user-profile/contracts/http-surface.md.
 *
 * Uses the seeded learner (alice). Serial mode: the tests below mutate and
 * restore her name, and 05-admin-learners.spec.ts asserts the seeded name
 * 'Alice Johnson' in the admin learner list — the DB persists between runs,
 * so every rename is restored in a finally block to keep the suite
 * idempotent.
 */

test.describe('Profile — editable display name (spec 030 US1)', () => {
  test.describe.configure({ mode: 'serial' });

  test('profile renders the editable name pre-filled with read-only email and role', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Profile');

    await expect(page.locator('#profile-name-input')).toHaveValue(testUsers.learner.name);

    // Read-only rows: email + role render as plain values inside the personal card.
    await expect(page.getByText(testUsers.learner.email, { exact: true })).toBeVisible();
    await expect(page.locator('.profile-card').getByText('Learner', { exact: true })).toBeVisible();
  });

  test('valid name save succeeds, nav shows the new name without re-login, admin learner list shows it', async ({ page, browser }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Profile');

    let adminContext: BrowserContext | undefined;
    try {
      await page.locator('#profile-name-input').fill('Alice J. Smith');
      await page.locator('#save-name-btn').click();

      await expect(page.locator('#profile-success')).toHaveText('Profile updated.');
      // The form POST re-renders the same page — no redirect, no re-login.
      expect(page.url()).toContain('/Account/Profile');
      // Cookie re-issued (R2): the nav reflects the new name on the resulting page (FR-004).
      await expect(page.locator('.account-name')).toHaveText('Alice J. Smith');

      // The single source of truth: the admin learner list shows the new name too.
      adminContext = await browser.newContext();
      const adminPage = await adminContext.newPage();
      await authFixture.loginAs(adminPage, 'OrgAdmin');
      const adminSegment = adminPage.locator('#role-pill .role-segment[data-value="admin"]');
      if (await adminSegment.isVisible().catch(() => false)) {
        await adminSegment.click();
      }
      await adminPage.goto('/Admin/Learners/Index');
      // Spec 032: the learner list is paginated (10 per page) — search
      // surfaces the renamed user regardless of page position.
      await adminPage.getByPlaceholder('Search by name or email...').fill('Alice J. Smith');
      await adminPage.getByRole('button', { name: 'Filter' }).click();
      await adminPage.waitForLoadState('networkidle');
      await expect(adminPage.locator('.data-table tbody').getByText('Alice J. Smith')).toBeVisible();
    } finally {
      if (adminContext) {
        await adminContext.close();
      }
      // Restore the seeded name for 05-admin-learners and future runs, no matter
      // where this test failed. The rename can only have happened after we reached
      // the profile page, so restore only from there.
      if (page.url().includes('/Account/Profile')) {
        await page.locator('#profile-name-input').fill(testUsers.learner.name);
        await page.locator('#save-name-btn').click();
        await expect(page.locator('#profile-success')).toHaveText('Profile updated.');
      }
    }
  });

  test('empty name is rejected with a field error and nothing is saved', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Profile');

    const navBefore = (await page.locator('.account-name').textContent()) ?? '';
    await page.locator('#profile-name-input').fill('');
    await page.locator('#save-name-btn').click();

    await expect(page.locator('#name-error')).toHaveText('Name is required.');
    await expect(page.locator('#profile-success')).toBeHidden();
    await expect(page.locator('.account-name')).toHaveText(navBefore.trim());
  });

  test('over-long name (150 chars) is rejected and nothing is saved', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Profile');

    const navBefore = (await page.locator('.account-name').textContent()) ?? '';
    // Bypass the client-side maxlength (a UX convenience) so the server-side
    // FR-003 guard is actually exercised: a programmatic .value assignment is
    // not truncated by the input's maxlength attribute.
    await page.locator('#profile-name-input').evaluate((el, value) => { el.value = value; }, 'x'.repeat(150));
    await page.locator('#save-name-btn').click();

    await expect(page.locator('#name-error')).toHaveText('Name must be 100 characters or fewer.');
    await expect(page.locator('#profile-success')).toBeHidden();
    await expect(page.locator('.account-name')).toHaveText(navBefore.trim());
  });
});
