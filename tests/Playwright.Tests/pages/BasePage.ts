import { Page } from '@playwright/test';

/**
 * BasePage: shared utilities for all Page Objects.
 *
 * Provides helpers for HTMX settlement and full-page navigation waits.
 */
export class BasePage {
  protected readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /**
   * Wait for HTMX indicators to disappear, signaling the partial update is done.
   * If no indicator exists, returns immediately.
   */
  async waitForHtmxSettle(timeoutMs = 10_000): Promise<void> {
    const indicator = this.page.locator('.htmx-indicator');
    if (await indicator.count() > 0) {
      await indicator.waitFor({ state: 'hidden', timeout: timeoutMs }).catch(() => {
        // If the indicator never appeared, the update might already be done
      });
    }
    // Small buffer to let DOM settle after HTMX swap
    await this.page.waitForTimeout(200);
  }

  /**
   * Wait for a full-page navigation to a specific URL path.
   * Use this for non-HTMX navigations (login redirect, etc).
   */
  async waitForNavigation(urlPath: string, timeoutMs = 10_000): Promise<void> {
    await this.page.waitForURL(
      (url) => url.pathname === urlPath,
      { timeout: timeoutMs }
    );
  }
}
