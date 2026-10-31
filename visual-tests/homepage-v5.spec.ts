import { test, expect } from '@playwright/test';

// Verifies visual geometry and working routes. Green tests do not imply that
// the design is pixel-identical to a third-party newspaper.
test('desktop masthead, editorial cover and lead story keep the reference geometry', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await expect(page.locator('.masthead__brand')).toBeVisible();
  const hero = page.locator('.news-home--v5 .hero-lead__image-link img');
  await expect(hero).toBeVisible();
  const cover = page.locator('.house-promo');
  await expect(cover).toBeVisible();
  const hb = await hero.boundingBox();
  const brand = await page.locator('.masthead__brand').boundingBox();
  expect(hb).not.toBeNull();
  expect(brand).not.toBeNull();
  expect(Math.abs(hb!.x - brand!.x)).toBeLessThanOrEqual(4);
  expect(Math.abs(hb!.width - 520)).toBeLessThanOrEqual(12);
  await expect(page.locator('.home-parallel__content > .topic-spotlight').first()).toBeVisible();
  await expect(page.locator('.hero-lead__title a').first()).toHaveAttribute('href', /\S/);
});

test('mobile home uses image-first stories and no horizontal overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.locator('.masthead__brand')).toBeVisible();
  await expect(page.locator('.masthead__saved-mobile')).toBeVisible();
  const hero = page.locator('.news-home--v5 .hero-lead');
  await expect(hero).toBeVisible();
  const image = await hero.locator('img').first().boundingBox();
  const title = await hero.locator('h1').boundingBox();
  expect(image).not.toBeNull();
  expect(title).not.toBeNull();
  expect(title!.y).toBeGreaterThanOrEqual(image!.y + image!.height);
  const width = await page.evaluate(() => ({ scroll:document.documentElement.scrollWidth, client:document.documentElement.clientWidth }));
  expect(width.scroll).toBeLessThanOrEqual(width.client + 1);
});
