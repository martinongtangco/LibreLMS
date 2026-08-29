import { test, expect, type Page } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';

/**
 * Spec 042 — per-user theme preference E2E (FR-001..FR-012, SC-001..SC-006).
 *
 * US1: choose a theme that applies immediately and persists
 * (FR-001..FR-003, FR-011, SC-001/SC-002) — contracts in
 * specs/042-user-theme-preference/contracts/theme-ui.md.
 * US2: the Light theme is warm paper, never pure white, with AA text contrast
 * (SC-003, SC-006).
 * US3: the Dark theme is warm dark, never pure black, with AA text contrast
 * (SC-003) — T017.
 * US4: no first-paint flash, live device follow, anonymous device theme — T020.
 *
 * Written FIRST per TDD (tasks T010/T014/T017/T020 extend this file story by
 * story; T013/T016/T019/T022 turn each block green).
 *
 * Whole-file serial (top-level configure below): the tests mutate the seeded
 * learner's (alice) ThemePreference, which persists in MSSQL between runs.
 * Each test sets the state it needs via the Settings UI, and every mutation is
 * restored to 'System' in a finally block (house pattern, cf.
 * 12-profile-name.spec.ts) so the suite stays idempotent. With the suite's
 * `fullyParallel: true`, per-describe `serial` is NOT enough — separate
 * describe blocks are independent serial bundles that Playwright interleaves
 * across workers, and two stories would then race on alice's row (observed:
 * US2's 'Light' write landed between US1's Dark save and its re-login read).
 * One top-level serial bundle keeps the whole file on a single worker.
 */

test.describe.configure({ mode: 'serial' });

const THEME_SELECT = 'select[name="ThemePreference"]';

/* ── WCAG contrast helpers (US2/US3 share these) ─────────────────────── */

/** WCAG 2.x relative luminance from a computed rgb()/rgba() string. */
function relLuminance(rgb: string): number {
  const m = rgb.match(/rgba?\(\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)\s*,\s*(\d+(?:\.\d+)?)/);
  if (!m) throw new Error(`unparseable color: ${rgb}`);
  const chan = (v: number) => {
    const c = v / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * chan(+m[1]) + 0.7152 * chan(+m[2]) + 0.0722 * chan(+m[3]);
}

/** WCAG contrast ratio between two computed colors (order-independent). */
function contrastRatio(fg: string, bg: string): number {
  const lum = [relLuminance(fg), relLuminance(bg)].sort((a, b) => b - a);
  return (lum[0] + 0.05) / (lum[1] + 0.05);
}

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

/* ── US2: Light paper palette (SC-003, SC-006) ───────────────────────── */

interface PageColors {
  bodyBg: string;
  bodyFg: string;
  cardBg: string | null;
  mutedFg: string | null;
  mutedBg: string;
}

/**
 * Sample the computed colors that US2/US3 assert on: body + first .card
 * backgrounds, body text color, and the first visible secondary (muted)
 * text element with its EFFECTIVE background (walked up to the first opaque
 * ancestor, since muted text usually sits on a transparent element).
 */
function samplePageColors(page: Page): Promise<PageColors> {
  return page.evaluate(() => {
    const opaqueBg = (el: Element | null): string => {
      let node: Element | null = el;
      while (node) {
        const bg = getComputedStyle(node).backgroundColor;
        if (bg !== 'rgba(0, 0, 0, 0)') return bg;
        node = node.parentElement;
      }
      return 'rgb(255, 255, 255)'; // browser canvas
    };
    const body = document.body;
    const card = body.querySelector('.card');
    const muted = Array.from(
      body.querySelectorAll<HTMLElement>('.text-muted, .card p, [class*="muted"]'),
    ).find((el) => el.textContent?.trim().length);
    return {
      bodyBg: getComputedStyle(body).backgroundColor,
      bodyFg: getComputedStyle(body).color,
      cardBg: card ? opaqueBg(card) : null,
      mutedFg: muted ? getComputedStyle(muted).color : null,
      mutedBg: muted ? opaqueBg(muted) : 'rgb(0, 0, 0)',
    };
  });
}

/** Standard page set per SC-003/SC-006 (catalog, detail, My Courses, Settings). */
const STANDARD_PAGES: Array<{ name: string; enter: (page: Page) => Promise<void> }> = [
  { name: 'course catalog', enter: (p) => p.goto('/Courses/Index') },
  {
    name: 'course detail (first seeded course)',
    enter: async (p) => {
      await p.goto('/Courses/Index');
      await p.locator('.card h3 a').first().click();
      await p.waitForURL(/\/Courses\/Detail/);
    },
  },
  { name: 'my courses', enter: (p) => p.goto('/MyCourses/Index') },
  { name: 'account settings', enter: (p) => p.goto('/Account/Settings') },
];

async function setTheme(page: Page, theme: 'Light' | 'Dark'): Promise<void> {
  await page.goto('/Account/Settings');
  await page.locator(THEME_SELECT).selectOption(theme);
  const want = theme.toLowerCase();
  await expect(page.locator('html')).toHaveAttribute('data-theme', want, { timeout: 5_000 });
}

test.describe('Theme — Light paper palette (spec 042 US2)', () => {
  for (const { name, enter } of STANDARD_PAGES) {
    test(`Light: ${name} — paper surfaces, AA text contrast (SC-006, SC-003)`, async ({ page }) => {
      await authFixture.loginAs(page, 'Learner');
      try {
        await setTheme(page, 'Light');
        await enter(page);

        const c = await samplePageColors(page);

        // SC-006: no pure-white background or surface on standard pages.
        expect(c.bodyBg, `body background on "${name}" is pure white`).not.toBe('rgb(255, 255, 255)');
        if (c.cardBg !== null) {
          expect(c.cardBg, `.card background on "${name}" is pure white`).not.toBe('rgb(255, 255, 255)');
        }

        // SC-003: AA (4.5:1) for primary and secondary text against effective backgrounds.
        expect(
          contrastRatio(c.bodyFg, c.bodyBg),
          `body text contrast ${contrastRatio(c.bodyFg, c.bodyBg).toFixed(2)}:1 on "${name}"`,
        ).toBeGreaterThanOrEqual(4.5);
        if (c.mutedFg !== null) {
          expect(
            contrastRatio(c.mutedFg, c.mutedBg),
            `muted text contrast ${contrastRatio(c.mutedFg, c.mutedBg).toFixed(2)}:1 on "${name}"`,
          ).toBeGreaterThanOrEqual(4.5);
        }
      } finally {
        await restoreSystemTheme(page);
      }
    });
  }
});

/* ── US3: Dark night-reading palette (FR-005, SC-003) ─────────────────── */

interface SemanticSample {
  label: string;
  fg: string;
  bg: string;
}

/**
 * First visible element per semantic color group, with its computed
 * foreground and EFFECTIVE (opaque-ancestor-walked) background. Groups
 * mirror the token pairs in site.css: brand/accent, success, error.
 */
function sampleSemanticColors(page: Page): Promise<SemanticSample[]> {
  return page.evaluate(() => {
    const opaqueBg = (el: Element | null): string => {
      let node: Element | null = el;
      while (node) {
        const bg = getComputedStyle(node).backgroundColor;
        if (bg !== 'rgba(0, 0, 0, 0)') return bg;
        node = node.parentElement;
      }
      return 'rgb(255, 255, 255)';
    };
    const groups: Array<{ label: string; selector: string }> = [
      { label: 'brand', selector: '.btn-primary, .btn-ghost, .brand, [class*="brand"]' },
      { label: 'success', selector: '.tag-accent-2, .alert-success, .text-success' },
      { label: 'error', selector: '.alert-danger, .text-danger' },
    ];
    const out: SemanticSample[] = [];
    for (const g of groups) {
      const el = Array.from(document.querySelectorAll<HTMLElement>(g.selector)).find(
        (n) => n.offsetWidth > 0 || n.offsetHeight > 0,
      );
      if (el) out.push({ label: g.label, fg: getComputedStyle(el).color, bg: opaqueBg(el) });
    }
    return out;
  });
}

test.describe('Theme — Dark night-reading palette (spec 042 US3)', () => {
  // US3 page set per T017: catalog + settings (the two dark-mode anchor pages).
  const DARK_PAGES: Array<{ name: string; enter: (page: Page) => Promise<void> }> = [
    { name: 'course catalog', enter: (p) => p.goto('/Courses/Index') },
    { name: 'account settings', enter: (p) => p.goto('/Account/Settings') },
  ];

  for (const { name, enter } of DARK_PAGES) {
    test(`Dark: ${name} — soft dark surfaces, AA text contrast (FR-005, SC-003)`, async ({ page }) => {
      await authFixture.loginAs(page, 'Learner');
      try {
        await setTheme(page, 'Dark');
        await enter(page);

        const c = await samplePageColors(page);

        // FR-005: soft dark, never pure black.
        expect(c.bodyBg, `body background on "${name}" is pure black`).not.toBe('rgb(0, 0, 0)');

        // SC-003: AA (4.5:1) for primary and secondary text.
        expect(
          contrastRatio(c.bodyFg, c.bodyBg),
          `body text contrast ${contrastRatio(c.bodyFg, c.bodyBg).toFixed(2)}:1 on "${name}"`,
        ).toBeGreaterThanOrEqual(4.5);
        if (c.mutedFg !== null) {
          expect(
            contrastRatio(c.mutedFg, c.mutedBg),
            `muted text contrast ${contrastRatio(c.mutedFg, c.mutedBg).toFixed(2)}:1 on "${name}"`,
          ).toBeGreaterThanOrEqual(4.5);
        }

        // SC-003: semantic (brand/success/error) text ≥ 4.5:1 where present.
        for (const s of await sampleSemanticColors(page)) {
          expect(
            contrastRatio(s.fg, s.bg),
            `${s.label} text contrast ${contrastRatio(s.fg, s.bg).toFixed(2)}:1 on "${name}"`,
          ).toBeGreaterThanOrEqual(4.5);
        }

        // Distinguishability spot-check: badge/tag backgrounds differ from the
        // card surface so state chips read as shapes, not just hue.
        const chip = await page.evaluate(() => {
          const tag = document.querySelector<HTMLElement>('.tag, .badge');
          const card = document.querySelector<HTMLElement>('.card');
          if (!tag || !card) return null;
          return {
            tagBg: getComputedStyle(tag).backgroundColor,
            cardBg: getComputedStyle(card).backgroundColor,
          };
        });
        if (chip) {
          expect(
            chip.tagBg,
            `tag background ${chip.tagBg} equals card surface ${chip.cardBg} on "${name}"`,
          ).not.toBe(chip.cardBg);
        }
      } finally {
        await restoreSystemTheme(page);
      }
    });
  }
});

/* ── US4: System follows the device, no flash (FR-007…FR-009, SC-004/005) ─ */

test.describe('Theme — System follows device, no flash (spec 042 US4)', () => {
  /** First document HTML of a navigation (asserted pre-settle — SC-004). */
  async function firstDocumentHtml(page: Page, url: string): Promise<string> {
    const [resp] = await Promise.all([
      page.waitForResponse((r) => r.request().resourceType() === 'document'),
      page.goto(url),
    ]);
    return resp.text();
  }

  test('explicit themes render data-theme in the FIRST served document (SC-004)', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    try {
      await setTheme(page, 'Dark');
      const darkHtml = await firstDocumentHtml(page, '/Courses/Index');
      expect(darkHtml).toMatch(/<html[^>]*data-theme="dark"/);

      await setTheme(page, 'Light');
      const lightHtml = await firstDocumentHtml(page, '/Courses/Index');
      expect(lightHtml).toMatch(/<html[^>]*data-theme="light"/);
    } finally {
      await restoreSystemTheme(page);
    }
  });

  test('System serves no server-side theme and the resolver runs before site.css (SC-004)', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Settings');
    try {
      // Force System so the assertion does not depend on prior test state.
      await page.locator(THEME_SELECT).selectOption('System');
      const html = await firstDocumentHtml(page, '/Courses/Index');

      // System: no server-side dark/light attribute…
      expect(html).not.toMatch(/<html[^>]*data-theme="(dark|light)"/);
      // …and the inline resolver is served in <head> BEFORE the stylesheet
      // paints, so the first paint is already themed (no flash).
      const scriptIdx = html.indexOf('prefers-color-scheme');
      const cssIdx = html.indexOf('/css/site.css');
      expect(scriptIdx, 'head resolver script missing from served HTML').toBeGreaterThan(-1);
      expect(cssIdx, 'site.css link missing from served HTML').toBeGreaterThan(-1);
      expect(scriptIdx).toBeLessThan(cssIdx);
    } finally {
      await restoreSystemTheme(page);
    }
  });

  test('System live-follows a device change with no reload (FR-007, SC-005)', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.emulateMedia({ colorScheme: 'light' });
    await page.goto('/Courses/Index');
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');

    const urlBefore = page.url();
    await page.evaluate(() => {
      (window as unknown as Record<string, string>).__themeNavProbe = 'alive';
    });
    await page.emulateMedia({ colorScheme: 'dark' });
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark', { timeout: 3_000 });
    expect(page.url()).toBe(urlBefore);
    expect(await page.evaluate(() => (window as unknown as Record<string, string>).__themeNavProbe)).toBe('alive');
  });

  test('explicit save stops device-follow; System re-selection re-arms it (FR-007/FR-008)', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.goto('/Account/Settings');
    try {
      // Device dark + System → dark from the head resolver.
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

      // Explicit Light wins over the device immediately…
      await page.locator(THEME_SELECT).selectOption('Light');
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'light', { timeout: 5_000 });
      // …and a device flip no longer overrides it (follow disarmed).
      await page.emulateMedia({ colorScheme: 'light' });
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');

      // System again: re-arms follow and resolves the (now light) device setting.
      await page.locator(THEME_SELECT).selectOption('System');
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'light', { timeout: 5_000 });
      await page.emulateMedia({ colorScheme: 'dark' });
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark', { timeout: 3_000 });
    } finally {
      await restoreSystemTheme(page);
    }
  });

  test('anonymous visitors follow the device setting (FR-009)', async ({ page, browser }) => {
    for (const scheme of ['dark', 'light'] as const) {
      const ctx = await browser.newContext({ colorScheme: scheme });
      const anon = await ctx.newPage();
      await anon.goto('/Courses/Index');
      await expect(anon.locator('html')).toHaveAttribute('data-theme', scheme);
      await ctx.close();
    }
  });

  test('anonymous dark: login primary button keeps AA contrast (T018 fix)', async ({ page, browser }) => {
    // The login page is only reachable in Dark via the anonymous device path,
    // so this is where the btn-primary --on-accent fix (T018) is exercised.
    const ctx = await browser.newContext({ colorScheme: 'dark' });
    const anon = await ctx.newPage();
    await anon.goto('/Account/Login');
    await expect(anon.locator('html')).toHaveAttribute('data-theme', 'dark');
    const c = await anon.evaluate((): { fg: string; bg: string } | null => {
      const btn = document.querySelector('.btn-primary');
      if (!btn) return null;
      return { fg: getComputedStyle(btn).color, bg: getComputedStyle(btn).backgroundColor };
    });
    expect(c, '.btn-primary missing on the login page').not.toBeNull();
    expect(
      contrastRatio(c!.fg, c!.bg),
      `btn-primary contrast ${contrastRatio(c!.fg, c!.bg).toFixed(2)}:1`,
    ).toBeGreaterThanOrEqual(4.5);
    await ctx.close();
  });
});
