import { test, expect } from '@playwright/test';
import { authFixture } from '../fixtures/authFixture';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';

/**
 * Spec 030 US3 E2E — display photo (FR-008..FR-011), nav avatar visibility for
 * admin-role users (Q1 = C), the initials placeholder, and the anonymous
 * redirect (FR-013).
 *
 * Written FIRST per TDD. Serial mode: the learner tests share alice's avatar
 * file (one file per user+extension under wwwroot/avatars), so they must not
 * interleave. All tests are idempotent across runs (uploads replace in place).
 */

const PNG = path.join(__dirname, '../fixtures/avatar-64.png');
const JPG = path.join(__dirname, '../fixtures/avatar-64.jpg');

let tempDir = '';

test.describe('Profile — display photo + nav avatar (spec 030 US3)', () => {
  test.describe.configure({ mode: 'serial' });

  test.beforeAll(() => {
    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'lms-avatar-'));
    fs.writeFileSync(path.join(tempDir, 'notanimage.txt'), 'this is not an image');
    fs.writeFileSync(path.join(tempDir, 'oversize.jpg'), Buffer.alloc(6 * 1024 * 1024, 7));
  });

  test.afterAll(() => {
    fs.rmSync(tempDir, { recursive: true, force: true });
  });

  test('learner uploads a photo: success, photo on the profile AND next to the name in the nav', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Profile');

    await page.locator('#photo-input').setInputFiles(PNG);
    await page.locator('#upload-photo-btn').click();

    await expect(page.locator('#profile-success')).toHaveText('Profile photo updated.');
    const profileImg = page.locator('#profile-avatar-img');
    await expect(profileImg).toBeVisible();
    const src = (await profileImg.getAttribute('src')) ?? '';
    expect(src).toContain('/avatars/');
    expect(src.endsWith('.png')).toBeTruthy();

    // Same resulting page: the nav shows the photo next to the name (no re-login).
    const navImg = page.locator('img#nav-avatar');
    await expect(navImg).toBeVisible();
    expect(await navImg.getAttribute('src')).toBe(src);
  });

  test('replacing the photo with a different extension changes the src and the old URL 404s', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Profile');

    // Establish a known previous state: a .png avatar must exist before the swap.
    let oldSrc = await page.locator('#profile-avatar-img').getAttribute('src').catch(() => null);
    if (!oldSrc || !oldSrc.endsWith('.png')) {
      await page.locator('#photo-input').setInputFiles(PNG);
      await page.locator('#upload-photo-btn').click();
      await expect(page.locator('#profile-success')).toHaveText('Profile photo updated.');
      oldSrc = (await page.locator('#profile-avatar-img').getAttribute('src')) ?? '';
    }

    // Replace with a JPEG — the src (extension) must change.
    await page.locator('#photo-input').setInputFiles(JPG);
    await page.locator('#upload-photo-btn').click();
    await expect(page.locator('#profile-success')).toHaveText('Profile photo updated.');
    const newSrc = (await page.locator('#profile-avatar-img').getAttribute('src')) ?? '';
    expect(newSrc.endsWith('.jpg')).toBeTruthy();
    expect(newSrc).not.toBe(oldSrc);

    // The new file is served; the replaced file is gone (404).
    expect((await page.request.get(newSrc)).status()).toBe(200);
    expect((await page.request.get(oldSrc!)).status()).toBe(404);
  });

  test('invalid uploads are rejected and the previous photo stays intact', async ({ page }) => {
    await authFixture.loginAs(page, 'Learner');
    await page.goto('/Account/Profile');

    const stateBefore = await page.locator('#profile-avatar-img').getAttribute('src').catch(() => null);

    // A text file (bad extension + MIME) is rejected.
    await page.locator('#photo-input').setInputFiles(path.join(tempDir, 'notanimage.txt'));
    await page.locator('#upload-photo-btn').click();
    await expect(page.locator('#photo-error')).toHaveText('Photo must be a JPG, PNG, WebP, or GIF image.');
    let stateAfter = await page.locator('#profile-avatar-img').getAttribute('src').catch(() => null);
    expect(stateAfter).toBe(stateBefore);

    // An oversized image (>5 MB) is rejected too.
    await page.locator('#photo-input').setInputFiles(path.join(tempDir, 'oversize.jpg'));
    await page.locator('#upload-photo-btn').click();
    await expect(page.locator('#photo-error')).toHaveText('Photo must be 5 MB or smaller.');
    stateAfter = await page.locator('#profile-avatar-img').getAttribute('src').catch(() => null);
    expect(stateAfter).toBe(stateBefore);
  });

  test('admin-role user: avatar hidden in the Admin view, visible in the Learner view, always on the profile page', async ({ page }) => {
    await authFixture.loginAs(page, 'OrgAdmin');
    await page.goto('/Account/Profile');

    await page.locator('#photo-input').setInputFiles(PNG);
    await page.locator('#upload-photo-btn').click();
    await expect(page.locator('#profile-success')).toHaveText('Profile photo updated.');

    // Default view for admin-capable users is Admin → body has .role-admin →
    // the Q1=C CSS rule hides the nav avatar.
    await expect(page.locator('body')).toHaveClass(/role-admin/);
    await expect(page.locator('#nav-avatar')).toBeHidden();

    // Switch the pill to the Learner view → the avatar appears.
    await page.locator('#role-pill .role-segment[data-value="learner"]').click();
    await expect(page.locator('#nav-avatar')).toBeVisible();

    // The profile page itself shows the photo in either view.
    await page.goto('/Account/Profile');
    await expect(page.locator('#profile-avatar-img')).toBeVisible();
  });

  test('photo-less user shows an initials placeholder, never a broken image', async ({ page, context }) => {
    const email = `photoe2e+${Date.now()}@example.com`;
    const password = 'Sup3rSecret!x9';

    // Fresh verified account without any photo upload.
    await page.goto('/Account/Signup');
    await page.getByLabel('Full name').fill('Photo Check');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByLabel('Confirm password', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page.getByRole('heading', { name: 'Check your email' })).toBeVisible();

    const outbox = (await (await page.request.get('/api/dev/outbox')).json()) as Array<{ to: string; purpose: string; body: string }>;
    const verification = outbox.find((e) => e.to === email && e.purpose === 'Verification');
    expect(verification, 'a verification email should be in the dev outbox').toBeDefined();
    const match = verification!.body.match(/http:\/\/[^\s]+\/Account\/Verify\?token=[A-Za-z0-9_-]+/);
    expect(match).not.toBeNull();
    const verifyPage = await context.newPage();
    await verifyPage.goto(match![0]);
    await expect(verifyPage.getByRole('heading', { name: 'Your email is verified' })).toBeVisible();
    await verifyPage.close();

    await page.goto('/Account/Login');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password').fill(password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(
      (url) => url.pathname === '/' || url.pathname === '/Courses',
      { timeout: 10_000 }
    );

    await page.goto('/Account/Profile');
    // Initials placeholder on the profile…
    await expect(page.locator('#profile-avatar-initial')).toHaveText('P');
    // …and in the nav — no <img> avatar anywhere (never a broken image).
    await expect(page.locator('img.account-avatar')).toHaveCount(0);
    await expect(page.locator('span#nav-avatar')).toHaveText('P');
    await expect(page.locator('span#nav-avatar')).toBeVisible();
  });

  test('anonymous access to the profile redirects to login (FR-013)', async ({ page }) => {
    await page.goto('/Account/Profile');
    expect(page.url()).toContain('/Account/Login');
  });
});
