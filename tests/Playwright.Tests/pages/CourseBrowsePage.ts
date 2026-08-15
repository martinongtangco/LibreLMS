import { Page, Locator } from '@playwright/test';
import { BasePage } from './BasePage';

/**
 * Page Object for the Browse Courses page (/Courses/Index).
 *
 * Encapsulates selectors and actions for searching, filtering,
 * and reading course listings. Uses HTMX-aware waits.
 */
export class CourseBrowsePage extends BasePage {
  // ── Locators ──────────────────────────────────────────────────────
  readonly searchInput: Locator;
  readonly categorySelect: Locator;
  readonly clearButton: Locator;
  readonly courseList: Locator;
  readonly nextButton: Locator;
  readonly previousButton: Locator;
  readonly pageIndicator: Locator;

  constructor(page: Page) {
    super(page);

    this.searchInput = page.locator('#search-input');
    this.categorySelect = page.locator('select[name="category"]');
    this.clearButton = page.getByRole('link', { name: 'Clear' });
    this.courseList = page.locator('#course-list');
    this.nextButton = this.courseList.locator('nav.pagination button', { hasText: 'Next' });
    this.previousButton = this.courseList.locator('nav.pagination button', { hasText: 'Previous' });
    this.pageIndicator = this.courseList.locator('nav.pagination .text-muted');
  }

  /**
   * Dynamic locator for a course card by its title.
   * Course titles appear inside h3 > a within .card divs.
   */
  courseCard(courseTitle: string): Locator {
    return this.page.locator('.card').filter({ hasText: courseTitle });
  }

  // ── Assertions ────────────────────────────────────────────────────

  async isOnBrowsePage(): Promise<boolean> {
    const url = this.page.url();
    return url.endsWith('/Courses/Index') || url.includes('/Courses/Index');
  }

  // ── Actions ───────────────────────────────────────────────────────

  /**
   * Type a search query into the search box and wait for HTMX results.
   * HTMX uses keyup + changed + 300ms debounce.
   */
  async searchFor(query: string): Promise<void> {
    // Clear the input first
    await this.searchInput.click();
    await this.searchInput.fill('');

    // Type character by character to trigger keyup events that HTMX listens for
    await this.searchInput.pressSequentially(query, { delay: 30 });

    // Wait for the HTMX debounce (300ms) + request + DOM update
    await this.waitForHtmxSettle(15_000);
    await this.page.waitForTimeout(500);
  }

  /**
   * Select a category from the dropdown and wait for HTMX results.
   */
  async selectCategory(category: string): Promise<void> {
    await this.categorySelect.selectOption({ label: category });
    await this.waitForHtmxSettle();
  }

  /**
   * Click the Clear link to reset search and filters.
   */
  async clearFilters(): Promise<void> {
    await this.clearButton.click();
    await this.waitForHtmxSettle();
  }

  // ── Getters ───────────────────────────────────────────────────────

  /**
   * Return the visible course titles in the current listing.
   */
  async getCourseTitles(): Promise<string[]> {
    const cards = this.courseList.locator('.card');
    const count = await cards.count();
    const titles: string[] = [];

    for (let i = 0; i < count; i++) {
      const title = await cards.nth(i).locator('h3').innerText();
      titles.push(title.trim());
    }

    return titles;
  }

  /**
   * Return the number of visible course cards.
   */
  async getCourseCount(): Promise<number> {
    return await this.courseList.locator('.card').count();
  }

  // ── Pagination (bug 028) ──────────────────────────────────────────

  /**
   * Click the Next page button and wait for the HTMX swap to settle.
   * Precondition: the button is visible (not on the last page).
   */
  async clickNext(): Promise<void> {
    await this.nextButton.click();
    await this.waitForHtmxSettle(15_000);
  }

  /**
   * Click the Previous page button and wait for the HTMX swap to settle.
   * Precondition: the button is visible (not on the first page).
   */
  async clickPrevious(): Promise<void> {
    await this.previousButton.click();
    await this.waitForHtmxSettle(15_000);
  }

  /**
   * Return the pagination indicator text, e.g. "Page 2 of 2 (13 total)".
   */
  async getPageIndicatorText(): Promise<string> {
    return (await this.pageIndicator.innerText()).trim();
  }
}
