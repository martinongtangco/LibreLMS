import { Page } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Page Object for the account control in the navbar.
 *
 * The account control is a `div[role="button"]` (not a `<button>`) containing:
 *  - A static user name span (`.account-name`)
 *  - A chevron-down icon
 *  - A dropdown panel (`.account-dropdown`) with "View Profile" and "Settings" links
 *
 * Only visible when the user is authenticated.
 */
export class AccountPage extends BasePage {
  // ── Locators ──────────────────────────────────────────────────────────────

  /** The account control button (div with role="button"). */
  readonly accountControl = this.page.locator('#account-control');

  /** The displayed user name. */
  readonly accountName = this.page.locator('.account-name');

  /** The dropdown panel containing navigation links. */
  readonly accountDropdown = this.page.locator('#account-dropdown');

  /** "View Profile" link inside the dropdown. */
  readonly profileLink = this.page.getByRole('link', { name: 'View Profile' });

  /** "Settings" link inside the dropdown. */
  readonly settingsLink = this.page.getByRole('link', { name: 'Settings' });

  // ── Actions & Assertions ─────────────────────────────────────────────────

  /**
   * Return the visible account name text.
   */
  async getAccountName(): Promise<string> {
    const text = await this.accountName.textContent();
    return text?.trim() ?? '';
  }

  /**
   * Toggle the account dropdown open/closed.
   */
  async clickAccountDropdown(): Promise<void> {
    await this.accountControl.click();
    await this.accountDropdown.waitFor({ state: 'visible' });
  }

  /**
   * Check whether the account control is visible (i.e. user is authenticated).
   */
  async isAccountVisible(): Promise<boolean> {
    return this.accountControl.isVisible();
  }
}
