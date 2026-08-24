import { test, expect } from '@playwright/test';
import { request as playwrightRequest } from 'playwright';
import type { APIRequestContext, Locator, Page } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Admin Organizations: verify organization list and create form accessibility.
 *
 * Tests that OrgAdmin can view the seeded org tree and reach the
 * create-organization page.
 */
test.describe('Admin Organizations', () => {
  test.beforeEach(async ({ page }) => {
    // Log in as OrgAdmin before each test.
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(testUsers.orgAdmin.email);
    await page.getByLabel('Password').fill(testUsers.orgAdmin.password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => {
      return (
        url.pathname === '/' ||
        url.pathname.includes('/Courses') ||
        url.pathname.includes('/Courses')
      );
    }, { timeout: 10_000 });
  });

  test('organization list shows root org', async ({ page }) => {
    await page.goto('/Admin/Organizations/Index');

    // The page should render the seeded "Root Organization" in the org tree.
    await expect(page.getByText('Root Organization')).toBeVisible();
  });

  test('create organization form is accessible', async ({ page }) => {
    await page.goto('/Admin/Organizations/Index');

    // Click the "Create Organization" button on the index page.
    await page.getByRole('link', { name: 'Create Organization' }).click();

    // Should navigate to the create page and show the form fields.
    await expect(page.getByRole('heading', { name: 'Create Organization' })).toBeVisible();
    await expect(page.getByLabel('Name')).toBeVisible();
    await expect(page.getByLabel('Description')).toBeVisible();
  });
});

/**
 * Admin Organizations — tree hierarchy (spec 036, US1/US2).
 *
 * Asserts the DOM contract from specs/036-org-tree-branching/contracts/organization-tree-ui.md:
 * C-01 (single top-level root li, every org exactly once), C-02/C-03 (DOM nesting IS the
 * parent/child relationship), C-04 (siblings share a parent <ul>), C-07 (root indicator + badge).
 *
 * TDD: these target the tree markup delivered by the US1 implementation and are expected to
 * FAIL before that markup exists (red phase, task T003).
 */

// The <li> for the node whose OWN card name is exactly 'name' (nearest ancestor <li>).
function orgNodeLi(page: Page, name: string): Locator {
  return page.locator(
    `xpath=//span[@class="org-node__name" and normalize-space(text())="${name}"]/ancestor::li[1]`,
  );
}

// Count of <ul class="org-tree"> ancestors — the node's structural depth (root = 1).
async function treeDepth(li: Locator): Promise<number> {
  return li.evaluate((el) => {
    let depth = 0;
    for (let n = el.parentElement; n !== null; n = n.parentElement) {
      if (n.classList.contains('org-tree')) depth += 1;
    }
    return depth;
  });
}

// The node's OWN card: the first .org-node__card inside the li (cards further
// down the DOM belong to descendant nodes, not this one).
const ownCard = (li: Locator): Locator => li.locator('.org-node__card').first();

// Parse the org id from a node's Edit link (href is /Admin/Organizations/Edit/{id}).
async function orgIdOf(li: Locator): Promise<string> {
  const href = (await ownCard(li).locator('.org-node__actions a').getAttribute('href')) ?? '';
  const m = href.match(/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i);
  if (!m) throw new Error(`Could not parse org id from href: ${href}`);
  return m[1];
}

// The `request` fixture does not carry the browser's cookies in this setup, and
// the app 302-redirects unauthenticated API calls to /Account/Login (which a
// request context then follows, masking the failure). Build an API context from
// the logged-in page's storage state so calls run authenticated.
async function authedApi(page: Page): Promise<APIRequestContext> {
  const state = await page.context().storageState();
  return playwrightRequest.newContext({ storageState: state });
}

test.describe('Admin Organizations — tree hierarchy (spec 036)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(testUsers.orgAdmin.email);
    await page.getByLabel('Password').fill(testUsers.orgAdmin.password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL((url) => url.pathname === '/' || url.pathname.includes('/Courses'), {
      timeout: 10_000,
    });
    await page.goto('/Admin/Organizations/Index');
  });

  test('renders a single top-level root node with a Root badge (C-01, C-07)', async ({ page }) => {
    // Scope to the top-level tree (the only ul.org-tree carrying the aria-label);
    // nested child lists also carry the org-tree class.
    const topLevel = page.locator('ul.org-tree[aria-label="Organization hierarchy"] > li');
    await expect(topLevel).toHaveCount(1);

    const rootLi = topLevel.first();
    await expect(rootLi).toHaveClass(/org-node--root/);
    await expect(rootLi.locator('.badge', { hasText: 'Root' })).toBeVisible();
  });

  test('renders every seeded organization exactly once in the tree (C-01)', async ({ page }) => {
    const seeded = ['Root Organization', 'Finance', 'Sales', 'Billing'];
    for (const name of seeded) {
      const names = page.locator(
        `xpath=//span[@class="org-node__name" and normalize-space(text())="${name}"]`,
      );
      await expect(names, `org "${name}" must render exactly once`).toHaveCount(1);
    }

    // Every node <li> carries exactly one name and every name lives in a node <li>:
    // no missing nodes, no duplicates, no empty placeholders.
    const liCount = await page.locator('li.org-node').count();
    const nameCount = await page.locator('span.org-node__name').count();
    expect(liCount).toBe(nameCount);
    expect(liCount).toBeGreaterThanOrEqual(seeded.length);
  });

  test('nests Billing under Finance, not under Sales (C-02, C-03)', async ({ page }) => {
    const finance = orgNodeLi(page, 'Finance');
    const sales = orgNodeLi(page, 'Sales');
    const billing = orgNodeLi(page, 'Billing');

    // Billing's <li> is a DOM descendant of Finance's <li> ...
    await expect(
      finance.locator('xpath=.//li[.//span[@class="org-node__name" and normalize-space(text())="Billing"]]'),
    ).toHaveCount(1);
    // ... and not anywhere inside Sales' subtree.
    await expect(
      sales.locator('xpath=.//li[.//span[@class="org-node__name" and normalize-space(text())="Billing"]]'),
    ).toHaveCount(0);

    // Structural depth = number of ul.org-tree ancestors (root li = 1, its
    // children = 2, grandchildren = 3): Finance/Sales one level below root,
    // Billing one level below Finance.
    await expect(treeDepth(finance)).resolves.toBe(2);
    await expect(treeDepth(sales)).resolves.toBe(2);
    await expect(treeDepth(billing)).resolves.toBe(3);
  });

    test('draws connector lines on non-root nodes only (C-06)', async ({ page }) => {
    const root = page.locator('ul.org-tree > li.org-node--root');
    const finance = orgNodeLi(page, 'Finance');

    // getComputedStyle is not in this project's TS lib set, so reach it
    // through the element's window with a minimal structural type (browser-side only).
    const pseudoBorder = (li: Locator, pseudo: '::before' | '::after', prop: 'borderTopWidth' | 'borderLeftWidth') =>
      li.evaluate((el, [p, pr]) => {
        const view = (
          el as unknown as {
            ownerDocument: {
              defaultView: { getComputedStyle: (e: unknown, p: string) => Record<string, string> };
            };
          }
        ).ownerDocument.defaultView;
        return view.getComputedStyle(el, p)[pr];
      }, [pseudo, prop] as const);

    const elbowWidth = (li: Locator) => pseudoBorder(li, '::before', 'borderTopWidth');
    const spineWidth = (li: Locator) => pseudoBorder(li, '::after', 'borderLeftWidth');

    // Finance is a non-last child: its elbow and spine are drawn with border tokens.
    expect(await elbowWidth(finance)).toBe('1px');
    expect(await spineWidth(finance)).toBe('1px');

    // The root has no connector lines at all.
    expect(await elbowWidth(root)).toBe('0px');
    expect(await spineWidth(root)).toBe('0px');
  });

  test('groups siblings Finance and Sales under the same parent (C-04)', async ({ page }) => {
    const finance = orgNodeLi(page, 'Finance');
    const sales = orgNodeLi(page, 'Sales');

    const parentInfo = (li: Locator) =>
      li.evaluate((el) => {
        const p = el.parentElement;
        return p ? { tag: p.tagName, cls: p.className } : null;
      });

    const fp = await parentInfo(finance);
    const sp = await parentInfo(sales);
    expect(fp, 'Finance li must have a parent element').not.toBeNull();
    expect(fp!.tag).toBe('UL');
    expect(fp!.cls).toContain('org-tree');
    expect(sp).toEqual(fp);
  });

  test('renders a disabled org and its descendants muted with Disabled badges (C-08)', async ({ page }) => {
    // Finance id: parsed from the index page's Edit link (path form /Admin/Organizations/Edit/{id}).
    const finance = orgNodeLi(page, 'Finance');
    const financeId = await orgIdOf(finance);
    const sales = orgNodeLi(page, 'Sales');
    const root = page.locator('ul.org-tree > li.org-node--root');

    // Antiforgery token from the Create page form (valid for this user session).
    await page.goto('/Admin/Organizations/Create');
    const token = await page.locator('input[name="__RequestVerificationToken"]').inputValue();

    const api = await authedApi(page);
    const disableOrEnable = (action: 'disable' | 'enable') =>
      api.post(`/Admin/Organizations/Chart?handler=${action}&id=${financeId}`, {
        headers: { RequestVerificationToken: token },
      });

    const billingUnderFinance = () =>
      orgNodeLi(page, 'Finance').locator(
        'xpath=.//li[.//span[@class="org-node__name" and normalize-space(text())="Billing"]]',
      );

    try {
      await disableOrEnable('disable');

      // Reload: Finance and its child Billing carry the disabled treatment (C-08, FR-007) ...
      await page.goto('/Admin/Organizations/Index');
      await expect(orgNodeLi(page, 'Finance')).toHaveClass(/org-node--disabled/);
      await expect(ownCard(orgNodeLi(page, 'Finance')).locator('.badge', { hasText: 'Disabled' })).toBeVisible();
      await expect(billingUnderFinance()).toHaveClass(/org-node--disabled/);
      await expect(ownCard(billingUnderFinance()).locator('.badge', { hasText: 'Disabled' })).toBeVisible();

      // ... while the unaffected sibling and the root do not.
      await expect(sales).not.toHaveClass(/org-node--disabled/);
      await expect(root).not.toHaveClass(/org-node--disabled/);

      // Disabled nodes remain visible in place.
      await expect(ownCard(orgNodeLi(page, 'Finance')).locator('.org-node__name')).toBeVisible();
      await expect(ownCard(billingUnderFinance()).locator('.org-node__name')).toBeVisible();
    } finally {
      await disableOrEnable('enable');
      await api.dispose();
    }
  });

  test('create-organization flow lands the new node in the correct nesting (B-03)', async ({ page }) => {
    const finance = orgNodeLi(page, 'Finance');
    const sales = orgNodeLi(page, 'Sales');

    const name = `E2E Child ${Date.now()}`;

    // Create through the existing Create flow (UI).
    await page.goto('/Admin/Organizations/Create');
    await page.getByLabel('Name').fill(name);
    await page.getByLabel('Parent Organization').selectOption({ label: 'Finance' });
    await page.getByRole('button', { name: 'Create' }).click();
    // Create redirects to the organizations index (served at /Admin/Organizations).
    await page.waitForURL((url) =>
      url.pathname === '/Admin/Organizations' || url.pathname === '/Admin/Organizations/Index',
    );

    const created = orgNodeLi(page, name);
    try {
      // Exactly once, nested under Finance at the same depth as Billing (3), not under Sales.
      await expect(page.locator(`xpath=//span[@class="org-node__name" and normalize-space(text())="${name}"]`)).toHaveCount(1);
      await expect(finance.locator(`xpath=.//li[.//span[@class="org-node__name" and normalize-space(text())="${name}"]]`)).toHaveCount(1);
      await expect(sales.locator(`xpath=.//li[.//span[@class="org-node__name" and normalize-space(text())="${name}"]]`)).toHaveCount(0);
      await expect(treeDepth(created)).resolves.toBe(3);

      // The new node's Edit action targets the existing edit route.
      await expect(ownCard(created).locator('.org-node__actions a')).toHaveAttribute('href', /\/Admin\/Organizations\/Edit\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
    } finally {
      // Soft-delete the test org via the admin API so the dev DB stays clean.
      const createdId = await orgIdOf(created);
      const api = await authedApi(page);
      try {
        const del = await api.delete(`/api/organizations/${createdId}`);
        expect(del.status()).toBe(204);
      } finally {
        await api.dispose();
      }
    }
  });

  test('fits a 375px viewport without horizontal overflow (C-11, SC-004)', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/Admin/Organizations/Index');

    const tree = page.locator('ul.org-tree[aria-label="Organization hierarchy"]');
    const fits = await tree.evaluate((el) => {
      // Minimal structural type — this project's TS lib set has no DOM lib.
      const node = el as unknown as {
        scrollWidth: number;
        clientWidth: number;
        ownerDocument: { documentElement: { scrollWidth: number } };
      };
      return {
        scrollWidth: node.scrollWidth,
        clientWidth: node.clientWidth,
        docScrollWidth: node.ownerDocument.documentElement.scrollWidth,
      };
    });

    // No horizontal overflow: not on the tree container, not on the page.
    expect(fits.scrollWidth, 'tree container must not overflow horizontally').toBeLessThanOrEqual(fits.clientWidth + 1);
    expect(fits.docScrollWidth, 'page must not scroll horizontally').toBeLessThanOrEqual(375);

    // Every node name stays visible with the compressed indentation.
    const names = page.locator('span.org-node__name');
    const count = await names.count();
    expect(count).toBeGreaterThanOrEqual(4);
    for (let i = 0; i < count; i++) {
      await expect(names.nth(i)).toBeVisible();
    }
  });
});
