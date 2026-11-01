import { test, expect } from '@playwright/test';
import { badge, demoLogin } from './support/auth';

const STAFF = { email: 'editor@vnews.local', password: 'Demo@12345' };

// Sign-in dialog and page, safe return URLs, and registration, through the real UI.

test('sign-in dialog: wrong password shows an inline error and stays open; right password signs in without leaving the page', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/chuyen-muc/cong-nghe');
  await page.locator('.masthead__account-link').first().click();
  const dialog = page.locator('#reader-login-dialog');
  await dialog.locator('input[name="Email"]').fill(STAFF.email);
  await dialog.locator('input[name="Password"]').fill('sai-mat-khau');
  await dialog.locator('.reader-login-dialog__submit').click();

  const error = dialog.locator('[data-reader-login-error]');
  await expect(error).toBeVisible();
  await expect(error).toContainText('Email hoặc mật khẩu không đúng');
  await expect(dialog).toBeVisible();
  await expect(page).toHaveURL(/\/chuyen-muc\/cong-nghe/);
  await expect(dialog.locator('input[name="Password"]')).toBeFocused();
  await expect(dialog.locator('input[name="Password"]')).toHaveValue('');

  await dialog.locator('input[name="Password"]').fill(STAFF.password);
  await dialog.locator('.reader-login-dialog__submit').click();
  // From the dialog the reader goes back to the page they were reading, now signed in (the header changes).
  await expect(page.locator('.masthead__account-label')).toHaveText('Tài khoản');
  await expect(page).toHaveURL(/\/chuyen-muc\/cong-nghe$/);
  await expect(page.locator('#reader-login-dialog')).toHaveCount(0);
  // A newsroom account reaches the CMS from the header (the first CMS request after start-up can be slow).
  await page.locator('.masthead__utility-link', { hasText: 'Tòa soạn' }).first().click();
  await expect(page).toHaveURL(/\/cms/, { timeout: 20000 });
});

test('shared sign-in page: legacy CMS address forwards, wrong password errors, staff land in their own area', async ({ page }) => {
  await page.goto('/cms/dang-nhap');
  await expect(page).toHaveURL(/\/tai-khoan\/dang-nhap\?returnUrl=%2Fcms/);

  await page.locator('input[name="Email"]').fill(STAFF.email);
  await page.locator('input[name="Password"]').fill('sai-mat-khau');
  await page.locator('.reader-auth__submit').click();
  await expect(page.locator('.reader-auth__errors').first()).toContainText('Email hoặc mật khẩu không đúng');

  await page.locator('input[name="Password"]').fill(STAFF.password);
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL(/\/cms/);
  await expect(page.locator('.app-topbar')).toBeVisible();
});

test('an outside return URL never leaves the site after signing in', async ({ page }) => {
  await page.goto('/tai-khoan/dang-nhap?returnUrl=' + encodeURIComponent('https://evil.example/phish'));
  await page.locator('input[name="Email"]').fill(STAFF.email);
  await page.locator('input[name="Password"]').fill(STAFF.password);
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL(/localhost:5399\/cms/);
  expect(new URL(page.url()).hostname).toBe('localhost');
});

test('CMS pages and the sign-in page carry no site menu', async ({ page }) => {
  await demoLogin(page, 'admin');
  await expect(page.locator('#mega-menu')).toHaveCount(0);
  await expect(page.locator('#primary-nav')).toHaveCount(0);
});

test('register: a new reader signs up through the form, signs out, then signs in normally', async ({ page }) => {
  const email = `doc-gia-${Date.now()}@example.test`;
  await page.goto('/tai-khoan/dang-ky');
  await page.locator('input[name="DisplayName"]').fill('Độc giả thử nghiệm');
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill('MatKhau-Thu-123');
  await page.locator('input[name="ConfirmPassword"]').fill('MatKhau-Thu-123');
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL('/');
  await expect(page.locator('.masthead__account-label')).toHaveText('Tài khoản');
  await expect(badge(page)).toHaveCount(0); // a real account, not demo

  await page.goto('/tai-khoan/ho-so');
  await expect(page.locator('#profile-title')).toContainText('Độc giả thử nghiệm');
  await page.locator('main').getByRole('button', { name: 'Đăng xuất' }).click();
  await page.goto('/cms');
  await expect(page).toHaveURL(/\/tai-khoan\/dang-nhap/);

  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill('MatKhau-Thu-123');
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL('/'); // reader: returnUrl /cms is not honoured
  await page.goto('/cms/CmsUsers');
  await expect(page.getByRole('heading', { name: /403/ })).toBeVisible();
});

test('register shows validation errors in the same style and does not sign in', async ({ page }) => {
  await page.goto('/tai-khoan/dang-ky');
  await page.locator('input[name="DisplayName"]').fill('Lỗi');
  await page.locator('input[name="Email"]').fill('doc-gia-trung@example.test');
  await page.locator('input[name="Password"]').fill('MatKhau-Thu-123');
  await page.locator('input[name="ConfirmPassword"]').fill('KhongKhop-123');
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL(/dang-ky/);
  // The client-side message appears next to the confirm-password field, in the shared error style.
  const message = page.locator('.reader-auth__errors', { hasText: /\S/ }).first();
  await expect(message).toBeVisible();
  await expect(message).toContainText('không khớp');
  await expect(page.locator('.masthead__account-label')).toHaveCount(0);
});
