import { test, expect, type Page } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';

/**
 * Spec 042 — per-user theme preference E2E (FR-001..FR-012, SC-001..SC-006).
 *
 * US1 (this describe): choose a theme that applies immediately and persists
 * (FR-001..FR-003, FR-011, SC-001/SC-002) — contracts in
 * specs/042-user-theme-preference/contracts/theme-ui.md.
 *
 * Written FIRST per TDD (tasks T010/T014/T017/T020 extend this file story by
 * story; T013/T016/T019/T022 turn each block green).
 *
 * Serial + self-contained: the tests mutate the seeded learner's (alice)
 * ThemePreference, which persists in MSSQL between runs. Each test sets the
 * state it needs via the Settings UI, and every mutation is restored to
 * 'System' in a finally block (house pattern, cf. 12-profile-name.spec.ts) so
 * the suite stays idempotent.
 */

const THEME_SELECT = 'select[name="ThemePreference"]';

async function restoreSystemTheme(page: Page): Promise<void> {
  try {
    await page.goto('/Account/Settings');
    await page.locator(THEME_SELECT).selectOption('System', { timeout: 5_000 });
    // Give the save a moment to land before the next test signs in.
    await page.waitForTimeout(500);
  } catch {
    console.warn('theme-restore failed — alice may be left in a non-System theme');
  }
}

test.describe('Theme — choose, apply, persist (spec 042 US1)', () => {
  test.describe.configure({ mode: 'serial' });

  test('Settings offers exactly System/Light/Dark with System selected for a fresh account (FR-001)', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Settings');

    const select = page.locator(THEME_SELECT);
    const optionValues = await select.locator('option').evaluateAll((os) => os.map((o) => o.value));
    expect(optionValues).toEqual(['System', 'Light', 'Dark']);
    await expect(select).toHaveValue('System');
  });

  test('selecting Dark applies immediately without a page navigation (FR-003, SC-001)', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Settings');
    try {
      // Probe: a full navigation/reload wipes this flag; an in-place AJAX save must not.
      await page.evaluate(() => {
        (window as unknown as Record<string, string>).__themeNavProbe = 'alive';
      });

      await page.locator(THEME_SELECT).selectOption('Dark');

      // The theme attribute flips on the same document, within one second.
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark', { timeout: 5_000 });
      expect(await page.evaluate(() => (window as unknown as Record<string, string>).__themeNavProbe)).toBe('alive');
      expect(page.url()).toContain('/Account/Settings');
      // The selector itself reflects the saved value on the live page.
      await expect(page.locator(THEME_SELECT)).toHaveValue('Dark');
    } finally {
      await restoreSystemTheme(page);
    }
  });

  test('the saved theme follows the user to every page (FR-002, SC-002)', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Settings');
    try {
      await page.locator(THEME_SELECT).selectOption('Dark');
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark', { timeout: 5_000 });

      for (const path of ['/Courses/Index', '/MyCourses/Index', '/Account/Profile']) {
        await page.goto(path);
        await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark', { timeout: 5_000 });
      }
    } finally {
      await restoreSystemTheme(page);
    }
  });

  test('the saved theme survives sign-out and re-login (FR-002, SC-002)', async ({ page, browser }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Settings');
    let secondContext: Awaited<ReturnType<typeof browser.newContext>> | undefined;
    try {
      await page.locator(THEME_SELECT).selectOption('Dark');
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark', { timeout: 5_000 });

      await authFixture.logout(page);

      // A brand-new context: no cookies, no localStorage — only the account can carry the theme.
      secondContext = await browser.newContext();
      const p2 = await secondContext.newPage();
      await authFixture.loginAs(p2, 'Learner');
      await p2.goto('/Account/Settings');
      await expect(p2.locator(THEME_SELECT)).toHaveValue('Dark');
      await expect(p2.locator('html')).toHaveAttribute('data-theme', 'dark');
    } finally {
      if (secondContext) {
        const p2 = (await secondContext.pages())[0];
        if (p2) await restoreSystemTheme(p2);
        await secondContext.close();
      } else {
        await restoreSystemTheme(page);
      }
    }
  });
});
