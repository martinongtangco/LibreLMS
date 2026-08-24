import { test, expect } from '@playwright/test';
import type { Locator, Page } from '@playwright/test';
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
    const topLevel = page.locator('ul.org-tree > li');
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
      finance.locator('xpath=.//li[span[@class="org-node__name" and normalize-space(text())="Billing"]]'),
    ).toHaveCount(1);
    // ... and not anywhere inside Sales' subtree.
    await expect(
      sales.locator('xpath=.//li[span[@class="org-node__name" and normalize-space(text())="Billing"]]'),
    ).toHaveCount(0);

    // Structural depth: Finance/Sales at depth 1 (direct children of the root list),
    // Billing one level deeper (depth 2).
    await expect(treeDepth(finance)).resolves.toBe(1);
    await expect(treeDepth(sales)).resolves.toBe(1);
    await expect(treeDepth(billing)).resolves.toBe(2);
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
});
