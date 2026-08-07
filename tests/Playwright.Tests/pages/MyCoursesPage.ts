import { Page, Locator } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Page Object for the My Courses page (/MyCourses/Index).
 *
 * Covers enrollment list refresh, enrolled course visibility,
 * and per-course row lookups.
 */
export class MyCoursesPage extends BasePage {
  readonly refreshButton: Locator;
  readonly enrollmentList: Locator;

  constructor(page: Page) {
    super(page);
    this.refreshButton = page.getByRole('button', { name: /Refresh/i });
    this.enrollmentList = page.locator('#enrollment-list');
  }

  /**
   * Dynamic locator for a specific enrolled course row by title.
   */
  courseRow(courseTitle: string): Locator {
    return this.enrollmentList.locator('tr', { hasText: courseTitle });
  }

  /**
   * Assert the current page is My Courses by checking the page heading.
   */
  async isOnMyCoursesPage(): Promise<boolean> {
    const heading = this.page.getByRole('heading', { name: 'My Courses', level: 1 });
    return heading.isVisible();
  }

  /**
   * Return visible enrolled course titles from the enrollment list.
   */
  async getEnrolledCourseTitles(): Promise<string[]> {
    const rows = this.enrollmentList.locator('tr');
    const count = await rows.count();
    const titles: string[] = [];

    for (let i = 0; i < count; i++) {
      const text = await rows.nth(i).textContent();
      const trimmed = text?.trim();
      if (trimmed) {
        titles.push(trimmed);
      }
    }

    return titles;
  }

  /**
   * Click the Refresh button to re-fetch enrollment data via HTMX.
   */
  async refresh(): Promise<void> {
    await this.refreshButton.click();
    await this.waitForHtmxSettle();
  }
}
