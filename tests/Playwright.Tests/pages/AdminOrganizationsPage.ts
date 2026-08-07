import { expect, Locator, Page } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Page Object for Admin Organizations page (/Admin/Organizations/Index).
 *
 * Manages organization tree view, creation, and navigation.
 */
export class AdminOrganizationsPage extends BasePage {
  readonly createButton: Locator;
  readonly orgChartButton: Locator;

  constructor(page: Page) {
    super(page);
    // These are <a> links styled as buttons in the Razor page
    this.createButton = page.getByRole('link', { name: 'Create Organization' });
    this.orgChartButton = page.getByRole('link', { name: 'Org Chart View' });
  }

  /**
   * Navigate to the Organizations admin page.
   */
  async goto(): Promise<void> {
    await this.page.goto('/Admin/Organizations/Index');
    await this.waitForHtmxSettle();
  }

  /**
   * Assert the current page is the Organizations admin page.
   */
  async isOnOrganizationsPage(): Promise<void> {
    await expect(
      this.page.getByRole('heading', { name: 'Organization Management' })
    ).toBeVisible();
  }

  /**
   * Assert that an organization with the given name is visible on the page.
   */
  async hasOrganization(name: string): Promise<void> {
    await expect(this.page.getByText(name)).toBeVisible();
  }

  /**
   * Click the "Create Organization" button and navigate to the create form.
   */
  async clickCreate(): Promise<void> {
    await this.createButton.click();
    await this.page.waitForURL(/\/Admin\/Organizations\/Create/);
  }
}
