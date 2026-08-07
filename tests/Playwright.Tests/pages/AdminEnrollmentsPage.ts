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

  constructor(page: Page) {
    super(page);
    this.enrollmentTable = page.locator('.data-table');
    this.bulkEnrollButton = page.getByRole('link', { name: 'Bulk Enroll' });
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
