import { test, expect } from '@playwright/test';

// Public-site regression checks are behavior-oriented; they intentionally don't
// infer visual similarity from a passing test. Visual review remains separate.
test('mobile masthead exposes real reader and saved-article destinations', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');

  const account = page.locator('.masthead__right .masthead__account-link');
  const saved = page.locator('.masthead__saved-mobile');
  await expect(account).toBeVisible();
  await expect(saved).toBeVisible();
  await expect(saved).toHaveAttribute('href', /da-luu/i);
  await expect(page.locator('.masthead__identity')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);

  // Reader account entrypoint uses the real Identity-backed login dialog.
  await account.click();
  await expect(page.getByRole('dialog', { name: 'Đăng nhập tài khoản' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: 'Đăng nhập tài khoản' })).not.toBeVisible();
  await saved.click();
  await expect(page).toHaveURL(/da-luu/i);
});

test('category page lead and list stay connected to actual articles', async ({ page }) => {
  await page.goto('/');
  const technology = page.locator('.category-nav__list a').filter({ hasText: /công nghệ/i }).first();
  await expect(technology).toBeVisible();
  await technology.click();
  await expect(page.locator('.category-page')).toBeVisible();

  // The lead feature and the list below it are real sections; the lead headline opens its article.
  await expect(page.locator('#category-lead')).toBeVisible();
  await expect(page.locator('#category-feed')).toBeVisible();

  const headline = page.locator('#category-lead .cat-lead__title a');
  await expect(headline).toBeVisible();
  await headline.click();
  await expect(page.locator('article[data-news-article]')).toBeVisible();
  await expect(page.locator('.article-tools [data-action="save"]')).toBeVisible();
});


test('desktop brand aligns with lead story without narrowing category navigation', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const brand = await page.locator('.masthead__brand').boundingBox();
  const image = await page.locator('.news-home .hero-lead__image-link img').first().boundingBox();
  const nav = await page.locator('#primary-nav').boundingBox();
  expect(brand).not.toBeNull();
  expect(image).not.toBeNull();
  expect(nav).not.toBeNull();
  // Desktop reference uses the x=170 guide for both masthead and lead image.
  expect(Math.abs(brand!.x - image!.x)).toBeLessThanOrEqual(3);
  expect(image!.x).toBeGreaterThanOrEqual(167);
  expect(image!.x).toBeLessThanOrEqual(173);
  expect(nav!.width).toBeGreaterThanOrEqual(1435);
});
