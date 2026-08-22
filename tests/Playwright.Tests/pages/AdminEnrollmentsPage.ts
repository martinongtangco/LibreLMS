import { Page, Locator } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Page Object for the Admin Enrollments page (/Admin/Enrollments/Index).
 *
 * Provides selectors and actions for enrollment management:
 * viewing the enrollment table, bulk enrollment, and filtering.
 */
export class AdminEnrollmentsPage extends BasePage {
  // Table
  readonly enrollmentTable: Locator;

  // Buttons / links
  readonly bulkEnrollButton: Locator;

  // Pagination (spec 032): shared _AdminPagination partial controls
  readonly paginationNav: Locator;
  readonly previousLink: Locator;
  readonly nextLink: Locator;
  readonly pageIndicator: Locator;
  readonly pageSizeSelect: Locator;

  constructor(page: Page) {
    super(page);
    this.enrollmentTable = page.locator('.data-table');
    this.bulkEnrollButton = page.getByRole('link', { name: 'Bulk Enroll' });
    this.paginationNav = page.locator('nav.admin-pagination');
    this.previousLink = page.locator('nav.admin-pagination a').filter({ hasText: 'Previous' });
    this.nextLink = page.locator('nav.admin-pagination a').filter({ hasText: 'Next' });
    this.pageIndicator = page.locator('nav.admin-pagination span');
    this.pageSizeSelect = page.locator('select[name="pageSize"]');
  }

  /**
   * Navigate to the Admin Enrollments page with a query string (no leading '?').
   */
  async gotoWithQuery(query: string): Promise<void> {
    await this.page.goto(`/Admin/Enrollments/Index${query ? `?${query}` : ''}`);
    await this.page.locator('h1', { hasText: 'Enrollment Management' }).waitFor({ state: 'visible' });
  }

  /**
   * Number of rows currently rendered in the enrollment table.
   */
  async getRowCount(): Promise<number> {
    return this.enrollmentTable.locator('tbody tr').count();
  }

  /**
   * Set the student-name filter and submit the filter form (resets to page 1).
   */
  async filterByStudent(student: string): Promise<void> {
    await this.page.getByPlaceholder('Search by student name...').fill(student);
    await this.page.getByRole('button', { name: 'Filter' }).click();
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Set the course-title filter and submit the filter form (resets to page 1).
   */
  async filterByCourse(course: string): Promise<void> {
    await this.page.getByPlaceholder('Search by course title...').fill(course);
    await this.page.getByRole('button', { name: 'Filter' }).click();
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Navigate to the Admin Enrollments page and verify it loaded.
   */
  async goto(): Promise<void> {
    await this.page.goto('/Admin/Enrollments/Index');
    await this.isOnEnrollmentsPage();
  }

  /**
   * Assert the page is the Admin Enrollments page.
   */
  async isOnEnrollmentsPage(): Promise<boolean> {
    const url = this.page.url();
    return url.includes('/Admin/Enrollments/Index');
  }

  /**
   * Check if a specific enrollment (student → course) appears in the table.
   */
  async hasEnrollment(studentName: string, courseTitle: string): Promise<boolean> {
    const rows = this.enrollmentTable.locator('tbody tr');
    const count = await rows.count();

    for (let i = 0; i < count; i++) {
      const row = rows.nth(i);
      const studentText = await row.locator('td:first-child').innerText();
      const courseText = await row.locator('td:nth-child(3)').innerText();
      const hasStudent = studentText.includes(studentName);
      const hasCourse = courseText.includes(courseTitle);

      if (hasStudent && hasCourse) {
        return true;
      }
    }

    return false;
  }

  /**
   * Click the "Bulk Enroll" link to navigate to the bulk enrollment page.
   */
  async clickBulkEnroll(): Promise<void> {
    await this.bulkEnrollButton.click();
    await this.page.waitForURL(
      (url) => url.pathname === '/Admin/Enrollments/BulkEnroll',
      { timeout: 10_000 }
    );
  }

  /**
   * Get all student names from the enrollment table.
   */
  async getStudentNames(): Promise<string[]> {
    const rows = this.enrollmentTable.locator('tbody tr');
    const count = await rows.count();
    const names: string[] = [];

    for (let i = 0; i < count; i++) {
      names.push(await rows.nth(i).locator('td:first-child').innerText());
    }

    return names;
  }

  /**
   * Get all course titles from the enrollment table.
   */
  async getCourseTitles(): Promise<string[]> {
    const rows = this.enrollmentTable.locator('tbody tr');
    const count = await rows.count();
    const titles: string[] = [];

    for (let i = 0; i < count; i++) {
      titles.push(await rows.nth(i).locator('td:nth-child(3)').innerText());
    }

    return titles;
  }
}
