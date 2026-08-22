import { Page, Locator } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Page Object for the Admin Learners page (/Admin/Learners/Index).
 *
 * Encapsulates selectors and actions for the learner management table,
 * search/filter controls, and the create learner button.
 */
export class AdminLearnersPage extends BasePage {
  readonly learnerTable: Locator;
  readonly createButton: Locator;

  // Pagination (spec 032): shared _AdminPagination partial controls
  readonly paginationNav: Locator;
  readonly previousLink: Locator;
  readonly nextLink: Locator;
  readonly pageIndicator: Locator;
  readonly pageSizeSelect: Locator;

  constructor(page: Page) {
    super(page);
    this.learnerTable = page.locator('.data-table');
    this.createButton = page.getByRole('button', { name: 'Create Learner' });
    this.paginationNav = page.locator('nav.admin-pagination');
    this.previousLink = page.locator('nav.admin-pagination a').filter({ hasText: 'Previous' });
    this.nextLink = page.locator('nav.admin-pagination a').filter({ hasText: 'Next' });
    this.pageIndicator = page.locator('nav.admin-pagination span');
    this.pageSizeSelect = page.locator('select[name="pageSize"]');
  }

  /**
   * Navigate to the Admin Learners page with a query string (no leading '?').
   */
  async gotoWithQuery(query: string): Promise<void> {
    await this.page.goto(`/Admin/Learners/Index${query ? `?${query}` : ''}`);
    await this.page.locator('h1', { hasText: 'Learner Management' }).waitFor({ state: 'visible' });
  }

  /**
   * Number of rows currently rendered in the learner table.
   */
  async getRowCount(): Promise<number> {
    return this.learnerTable.locator('tbody tr').count();
  }

  /**
   * Set the name/email search and submit the filter form (resets to page 1).
   */
  async searchFor(term: string): Promise<void> {
    await this.page.getByPlaceholder('Search by name or email...').fill(term);
    await this.page.getByRole('button', { name: 'Filter' }).click();
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Dynamic locator for a specific learner row by name.
   */
  learnerRow(name: string): Locator {
    return this.learnerTable.locator('tr').filter({ hasText: name });
  }

  /**
   * Assert we are on the Learner Management page.
   */
  async isOnLearnersPage(): Promise<boolean> {
    return this.page.url().includes('/Admin/Learners/Index');
  }

  /**
   * Return visible learner names from the data table.
   */
  async getLearnerNames(): Promise<string[]> {
    const rows = this.learnerTable.locator('tbody tr');
    const count = await rows.count();
    const names: string[] = [];
    for (let i = 0; i < count; i++) {
      const nameCell = rows.nth(i).locator('td').first();
      names.push(await nameCell.textContent() ?? '');
    }
    return names;
  }

  /**
   * Click the Create Learner button and wait for navigation.
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.waitForNavigation('/Admin/Learners/Create');
  }
}
