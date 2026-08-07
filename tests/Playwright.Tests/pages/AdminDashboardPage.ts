import { Page, Locator } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * AdminDashboardPage: Page Object for the Admin Dashboard (/Admin/Dashboard/Index).
 *
 * Exposes locators for metric cards, the all-courses table, and completion rate.
 * Metric cards use data-label-based lookups so tests can assert specific values.
 */
export class AdminDashboardPage extends BasePage {
  private readonly metricCardsContainer: Locator;
  readonly allCoursesTable: Locator;

  constructor(page: Page) {
    super(page);
    this.metricCardsContainer = page.locator('.metric-cards');
    this.allCoursesTable = page.locator('.courses-table');
  }

  /**
   * Locate a specific metric card by its label text (e.g., "Organizations").
   */
  metricCard(label: string): Locator {
    return this.metricCardsContainer
      .locator('.metric-card')
      .filter({ hasText: label });
  }

  /**
   * Locate the value element inside a metric card identified by label.
   */
  metricValue(label: string): Locator {
    return this.metricCard(label).locator('.metric-value');
  }

  /**
   * Return all visible metric labels on the dashboard.
   */
  async getMetricLabels(): Promise<string[]> {
    return this.metricCardsContainer
      .locator('.metric-label')
      .allTextContents();
  }

  /**
   * Return the numeric value string for a given metric label.
   * Throws if the metric card is not found.
   */
  async getMetricValue(label: string): Promise<string> {
    const value = await this.metricValue(label).textContent();
    if (value === null) {
      throw new Error(`Metric value for "${label}" not found on dashboard`);
    }
    return value;
  }

  /**
   * Assert that the current page is the Admin Dashboard.
   * Checks URL path and the presence of the "Dashboard" heading.
   */
  async isOnDashboardPage(): Promise<boolean> {
    return (
      (await this.page.url()).includes('/Admin/Dashboard') &&
      (await this.page.locator('h1').filter({ hasText: 'Dashboard' }).isVisible())
    );
  }
}
