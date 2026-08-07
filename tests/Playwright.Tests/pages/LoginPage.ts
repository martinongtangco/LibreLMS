import { Locator, Page } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * LoginPage: Page Object for /Account/Login.
 *
 * Encapsulates selectors and actions for the Razor login page
 * (src/Host/Pages/Account/Login.cshtml).
 */
export class LoginPage extends BasePage {
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly signInButton: Locator;
  readonly errorMessage: Locator;

  constructor(page: Page) {
    super(page);
    this.emailInput = page.getByLabel('Email');
    this.passwordInput = page.getByLabel('Password');
    this.signInButton = page.getByRole('button', { name: 'Sign In' });
    this.errorMessage = page.locator('.error-message');
  }

  /**
   * Fill in credentials and submit the login form.
   */
  async login(email: string, password: string): Promise<void> {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.signInButton.click();
  }

  /**
   * Assert the current URL is the login page.
   */
  async isOnLoginPage(): Promise<boolean> {
    const url = this.page.url();
    return url.endsWith('/Account/Login') || url.includes('/Account/Login');
  }
}
