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

  constructor(page: Page) {
    super(page);
    this.learnerTable = page.locator('.data-table');
    this.createButton = page.getByRole('button', { name: 'Create Learner' });
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
