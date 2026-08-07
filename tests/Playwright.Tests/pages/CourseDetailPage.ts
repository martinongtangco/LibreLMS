import { Page, Locator } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Page Object for the Course Detail page (/Courses/Detail/{id}).
 *
 * Covers course title, description, enrollment state, and SCORM launch option.
 */
export class CourseDetailPage extends BasePage {
  readonly courseTitle: Locator;
  readonly courseDescription: Locator;
  readonly enrollButton: Locator;
  readonly enrolledButton: Locator;
  readonly launchScormButton: Locator;
  readonly enrollRegion: Locator;

  constructor(page: Page) {
    super(page);
    this.courseTitle = page.locator('h1');
    this.courseDescription = page.locator('p.text-muted').first();
    this.enrollButton = page.getByRole('button', { name: 'Enroll now' });
    this.enrolledButton = page.getByRole('button', { name: '✓ Enrolled' });
    this.launchScormButton = page.getByRole('link', { name: 'Launch SCORM Course' });
    this.enrollRegion = page.locator('#enroll-region');
  }

  /** Navigate to the course detail page and verify it loads. */
  async isOnDetailPage(): Promise<boolean> {
    const url = this.page.url();
    return url.includes('/Courses/Detail');
  }

  /** Return the rendered course title text. */
  async getCourseTitle(): Promise<string> {
    const text = await this.courseTitle.textContent();
    return text?.trim() ?? '';
  }

  /** Check whether the "Enroll now" button is visible (not yet enrolled). */
  async isEnrollButtonVisible(): Promise<boolean> {
    return this.enrollButton.isVisible();
  }

  /** Check whether the launch SCORM button is visible (SCORM course, enrolled). */
  async isLaunchButtonVisible(): Promise<boolean> {
    return this.launchScormButton.isVisible();
  }

  /** Click the "Enroll now" button and wait for HTMX swap to settle. */
  async clickEnroll(): Promise<void> {
    await this.enrollButton.click();
    await this.waitForHtmxSettle();
  }
}
