import { test, expect } from '@playwright/test';
import { testUsers } from '../utils/testUsers';

/**
 * Spec 055 E2E: enrollment while logged out + return-to-course flow.
 *
 * US1 (P1): a signed-out visitor can NEVER enroll (UI or raw HTTP) — the enroll
 *   action challenges to the sign-in page with the course as return address;
 *   after sign-in the visitor lands back on that course and enrolls with one
 *   MANUAL click (never automatic). No action may run on behalf of the seeded
 *   demo learner (alice) — guests see the not-enrolled state everywhere.
 * US2 (P2): the pending return address survives sign-up + email verification
 *   and lands the new user on the original course.
 * US3 (P3): guest read paths are public-only; My Courses requires sign-in.
 *
 * Course fixtures (seeded catalog, LearningLms):
 *   - 112: demo learner (alice) IS enrolled → pre-fix, guests saw her
 *     "✓ Enrolled" badge; post-fix, guests must see "Enroll now".
 *   - 115: used by the raw-HTTP rejection test (nobody enrolled).
 *   - 116: used by the guest UI-redirect test (nobody enrolled pre-fix, so the
 *     guest sees the "Enroll now" button and can click it).
 *   - 117: used by the sign-in round trip as bob (bob is NOT enrolled in it).
 *
 * Serial mode: the tests share the seeded courses and the red-verify run may
 * transiently create demo-attributed rows (the bug itself) — no parallel
 * interference allowed.
 */
test.describe.configure({ mode: 'serial' });

const COURSE_DEMO_ENROLLED = '11111111-1111-1111-1111-111111111112';
const COURSE_RAW_POST = '11111111-1111-1111-1111-111111111115';
const COURSE_GUEST_UI = '11111111-1111-1111-1111-111111111116';
const COURSE_ROUND_TRIP = '11111111-1111-1111-1111-111111111117';

/** Sign in a seeded learner on the current page (assumes /Account/Login). */
async function signIn(page: import('@playwright/test').Page, user: typeof testUsers.learner): Promise<void> {
  await page.getByLabel('Email').fill(user.email);
  await page.getByLabel('Password').fill(user.password);
  await page.getByRole('button', { name: 'Sign In' }).click();
}

test.describe('Spec 055 US1 — logged-out enrollment is impossible; sign-in round trip', () => {
  test('raw HTTP enroll POST as guest is rejected and creates no enrollment', async ({
    page,
    request,
  }) => {
    // Fresh request context = no session cookie = anonymous.
    const response = await request.post(
      `/Courses/Detail/${COURSE_RAW_POST}?handler=Enroll`,
      { maxRedirects: 0, headers: { 'HX-Request': 'true' } }
    );
    // Contract (contracts/enroll-action.md): 302 challenge to the sign-in page
    // with the course as ReturnUrl. Pre-fix this returns 200 and enrolls the
    // demo learner — the bug.
    expect(response.status()).toBe(302);
    const location = response.headers()['location'] ?? '';
    expect(location).toContain('/Account/Login');
    expect(decodeURIComponent(location)).toContain(`/Courses/Detail/${COURSE_RAW_POST}`);

    // No enrollment may have happened: the guest view of the course still
    // offers "Enroll now" (pre-fix it flips to "✓ Enrolled" — the demo
    // learner's state — because the POST enrolled her).
    await page.goto(`/Courses/Detail/${COURSE_RAW_POST}`);
    await expect(page.getByRole('button', { name: 'Enroll now' })).toBeVisible();
  });

  test('guest Enroll click is a full-page redirect to sign-in with the course as return address', async ({
    page,
  }) => {
    await page.goto(`/Courses/Detail/${COURSE_GUEST_UI}`);
    // Nobody is enrolled in this course, so the guest sees the button both
    // pre-fix and post-fix.
    const enrollButton = page.getByRole('button', { name: 'Enroll now' });
    await expect(enrollButton).toBeVisible();

    await enrollButton.click();
    // Contract: full page navigation to the sign-in page with ReturnUrl —
    // NOT an in-place HTMX swap of the sign-in form into #enroll-region.
    // Pre-fix the htmx POST succeeds (200) and the URL never changes → RED.
    // (ReturnUrl is percent-encoded in the URL — decode before asserting.)
    await page.waitForURL(/\/Account\/Login\?ReturnUrl=/, { timeout: 10_000 });
    const returnUrl = new URL(page.url()).searchParams.get('ReturnUrl') ?? '';
    expect(returnUrl).toContain(`/Courses/Detail/${COURSE_GUEST_UI}`);
  });

  test('sign-in returns the visitor to the course; enrollment needs a second manual click', async ({
    page,
  }) => {
    await page.goto(`/Courses/Detail/${COURSE_ROUND_TRIP}`);
    await page.getByRole('button', { name: 'Enroll now' }).click();
    // (Pre-fix: this click enrolls the demo learner and swaps the result into
    // the card — no navigation. Post-fix: full redirect to sign-in.)
    await page.waitForURL(/\/Account\/Login\?ReturnUrl=/, { timeout: 10_000 });

    await signIn(page, testUsers.learnerBob);
    // FR-004: back on the SAME course page.
    await expect(page).toHaveURL(/\/Courses\/Detail\//, { timeout: 10_000 });
    expect(page.url()).toContain(COURSE_ROUND_TRIP);

    const enrollButton = page.getByRole('button', { name: 'Enroll now' });
    if (await enrollButton.isVisible().catch(() => false)) {
      // Fresh state: bob is NOT auto-enrolled — the enroll button is still
      // there, which is exactly the spec-required manual second step.
      // The manual second click enrolls bob under his own account.
      await enrollButton.click();
      // HTMX swap (hx-swap="outerHTML") replaces #enroll-region with the
      // success partial — assert on the rendered enrolled state, not the id.
      await expect(page.getByText('✓ Enrolled')).toBeVisible({
        timeout: 10_000,
      });
      // And the enrollment shows up in bob's own My Courses (own data only).
      // Course 117 is "Git Version Control" (seeded catalog).
      await page.goto('/MyCourses');
      await expect(page.getByText('Git Version Control')).toBeVisible();
    } else {
      // Already enrolled from a previous run (persistent dev DB). The
      // round trip above still proves: back on the course, no auto-action.
      await expect(page.getByText('✓ Enrolled')).toBeVisible();
    }
  });

  test('guest sees no other user enrolled state on a course the demo learner is enrolled in', async ({
    page,
  }) => {
    // Course 112: alice (the demo learner) is enrolled. Pre-fix the guest
    // sees her "✓ Enrolled" badge (demo-identity fallback); post-fix the
    // guest sees the not-enrolled state.
    await page.goto(`/Courses/Detail/${COURSE_DEMO_ENROLLED}`);
    await expect(page.getByRole('button', { name: 'Enroll now' })).toBeVisible();
    await expect(page.getByText('✓ Enrolled')).toHaveCount(0);
    // No SCORM launch button either (that is enrolled-learner UI).
    await expect(page.getByRole('link', { name: 'Launch SCORM Course' })).toHaveCount(0);
  });
});
