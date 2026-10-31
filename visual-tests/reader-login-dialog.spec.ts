import { test, expect } from '@playwright/test';

test('reader sign-in opens in a keyboard-accessible dialog without accessing CMS', async ({ page }) => {
  await page.goto('/');
  const login = page.locator('.masthead__right [data-reader-login-open]');
  await expect(login).toHaveAttribute('href', /Account\/Login/i);
  await login.click();
  const dialog = page.getByRole('dialog', { name: 'Đăng nhập tài khoản' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('form')).toHaveAttribute('method', 'post');
  await expect(dialog.locator('form')).toHaveAttribute('action', /Account\/Login/i);
  await expect(dialog.locator('input[name="__RequestVerificationToken"]')).toHaveCount(1);
  await expect(dialog.locator('input[name="Email"]')).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(login).toBeFocused();
});

test('mobile login link closes utility menu and opens reader sign-in', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.locator('.masthead__menu-toggle').click();
  await expect(page.locator('#utility-drawer')).toHaveClass(/is-open/);
  await page.locator('#utility-drawer [data-reader-login-open]').click();
  await expect(page.locator('#utility-drawer')).not.toHaveClass(/is-open/);
  const dialog = page.getByRole('dialog', { name: 'Đăng nhập tài khoản' });
  await expect(dialog).toBeVisible();
  await dialog.getByRole('link', { name: /Đăng ký miễn phí/ }).click();
  await expect(page).toHaveURL(/\/Account\/Register/i);
});
