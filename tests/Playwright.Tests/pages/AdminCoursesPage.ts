import { Page, Locator } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Page Object for the Admin Courses page (/Admin/Courses/Index).
 *
 * Encapsulates the course management table, the sortable column headers,
 * the shared pagination controls (spec 032), and row deletion.
 */
export class AdminCoursesPage extends BasePage {
  readonly coursesTable: Locator;
  readonly titleSortLink: Locator;
  readonly categorySortLink: Locator;

  // Pagination (spec 032): shared _AdminPagination partial controls
  readonly paginationNav: Locator;
  readonly previousLink: Locator;
  readonly nextLink: Locator;
  readonly pageIndicator: Locator;
  readonly pageSizeSelect: Locator;

  constructor(page: Page) {
    super(page);
    this.coursesTable = page.locator('.data-table');
    this.titleSortLink = this.coursesTable.locator('thead th a').filter({ hasText: 'Title' });
    this.categorySortLink = this.coursesTable.locator('thead th a').filter({ hasText: 'Category' });
    this.paginationNav = page.locator('nav.admin-pagination');
    this.previousLink = page.locator('nav.admin-pagination a').filter({ hasText: 'Previous' });
    this.nextLink = page.locator('nav.admin-pagination a').filter({ hasText: 'Next' });
    this.pageIndicator = page.locator('nav.admin-pagination span');
    this.pageSizeSelect = page.locator('select[name="pageSize"]');
  }

  /**
   * Navigate to the Admin Courses page with a query string (no leading '?').
   */
  async gotoWithQuery(query: string): Promise<void> {
    await this.page.goto(`/Admin/Courses/Index${query ? `?${query}` : ''}`);
    await this.page.locator('h1', { hasText: 'Course Management' }).waitFor({ state: 'visible' });
  }

  /**
   * Number of rows currently rendered in the course table.
   */
  async getRowCount(): Promise<number> {
    return this.coursesTable.locator('tbody tr').count();
  }

  /**
   * Course titles of the rendered rows (first cell).
   */
  async getCourseTitles(): Promise<string[]> {
    const rows = this.coursesTable.locator('tbody tr');
    const count = await rows.count();
    const titles: string[] = [];
    for (let i = 0; i < count; i++) {
      titles.push((await rows.nth(i).locator('td').first().innerText()).trim());
    }
    return titles;
  }

  /**
   * Category values of the rendered rows (second cell badge).
   */
  async getCategories(): Promise<string[]> {
    const rows = this.coursesTable.locator('tbody tr');
    const count = await rows.count();
    const categories: string[] = [];
    for (let i = 0; i < count; i++) {
      categories.push((await rows.nth(i).locator('td').nth(1).innerText()).trim());
    }
    return categories;
  }

  /**
   * Accepts the confirm() dialog the Delete form raises, then clicks the
   * Delete button of the given row (0-based) and waits for the reload.
   */
  async deleteRow(rowIndex: number): Promise<void> {
    const dialogPromise = this.page
      .waitForEvent('dialog')
      .then((dialog) => dialog.accept());
    await this.coursesTable.locator('tbody tr').nth(rowIndex).getByRole('button', { name: 'Delete' }).click();
    await dialogPromise;
    await this.page.waitForLoadState('networkidle');
  }
}
