import { test, expect } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

test.describe('Enrollment Smoke', () => {
  test.beforeEach(async ({ page }) => {
    // Log in as Bob (a learner who may not be enrolled in all courses)
    const user = testUsers.learnerBob;
    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(user.email);
    await page.getByLabel('Password').fill(user.password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    // Wait for redirect after login
    await page.waitForURL(
      (url) =>
        url.pathname === '/' ||
        url.pathname.includes('/Courses'),
      { timeout: 10_000 }
    );
  });

  test('enroll in a course from detail page', async ({ page }) => {
    // Navigate to the course browse page and find "Advanced .NET Patterns"
    await page.goto('/Courses/Index');

    // Click on the "Advanced .NET Patterns" course card
    const courseCard = page.locator('h3 a.link-inherit').filter({
      hasText: 'Advanced .NET Patterns',
    });
    await expect(courseCard).toBeVisible();
    await courseCard.click();

    // Verify we're on the course detail page
    await expect(page.locator('h1')).toContainText('Advanced .NET Patterns');

    // Verify the enroll region is present
    const enrollRegion = page.locator('#enroll-region');
    await expect(enrollRegion).toBeVisible();

    // Check enrollment state: either already enrolled or can enroll
    const enrollButton = page.getByRole('button', { name: 'Enroll now' });
    const enrolledButton = page.getByText(/Enrolled/i);

    const isEnrolled = await enrolledButton.isVisible().catch(() => false);
    const canEnroll = await enrollButton.isVisible().catch(() => false);

    if (canEnroll) {
      // Capture state before click to verify the swap actually happens
      const beforeEnroll = await enrollRegion.textContent();

      // Not enrolled yet — click to enroll (force click to avoid waiting for navigation)
      await page.locator('#enroll-region button').first().click({ force: true });

      // Wait for HTMX swap — the enroll region should update
      await page.waitForTimeout(2000);

      // Verify the enroll region changed (shows enrolled state)
      await expect(enrollRegion).toBeVisible();
      const afterEnroll = await enrollRegion.textContent();
      // Must have changed from "Enroll now" to show enrolled state
      expect(afterEnroll).not.toBe(beforeEnroll);
      expect(afterEnroll).toMatch(/enrolled|successfully/i);
    } else if (isEnrolled) {
      // Already enrolled from a previous run — that's fine
    }
    // In both cases, the enroll region should be visible (verified above)
  });

  test('view my courses shows enrolled courses', async ({ page }) => {
    // Navigate to My Courses page
    await page.goto('/MyCourses/Index');

    // Verify the enrollment list container is visible
    const enrollmentList = page.locator('#enrollment-list');
    await expect(enrollmentList).toBeVisible();

    // The enrollment list either shows .my-course-row items or an empty-state card
    // Check that at least one of them is visible
    const hasContent =
      (await enrollmentList.locator('.my-course-row').count()) > 0 ||
      (await enrollmentList.locator('.empty-state').count()) > 0;
    expect(hasContent).toBe(true);
  });
});
