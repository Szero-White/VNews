import { test, expect } from '@playwright/test';

// Structural regression, not a pixel-equality claim about live advertisements.
test('desktop presents all category panels before the ranked stories', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const panels = page.locator('.home-parallel__content > .topic-spotlight');
  expect(await panels.count()).toBeGreaterThanOrEqual(3);
  const last = await panels.last().boundingBox();
  const popular = page.locator('.home-popular');
  await expect(popular).toHaveCount(1);
  const popularBox = await popular.boundingBox();
  expect(popularBox!.y).toBeGreaterThan(last!.y + last!.height);
});

test('mobile shows its fifth published story instead of hiding it', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  const cards = page.locator('.home-parallel__stream .stream-list > .stream-compact');
  expect(await cards.count()).toBeGreaterThanOrEqual(5);
  await expect(cards.nth(4)).toBeVisible();
  const size = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(size.scroll).toBeLessThanOrEqual(size.client + 1);
});
