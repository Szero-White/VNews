import { expect, test } from '@playwright/test';

// House-ad creative is deliberate and clearly labeled, not a tracked ad impression.
test('desktop top banner displays a working first-party campaign', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const slot = page.locator('[data-ad-slot="top"]');
  await expect(slot).toBeVisible();
  await expect(slot).toHaveAttribute('data-ad-state', 'house');
  await expect(slot.getByText('QUẢNG CÁO MINH HỌA')).toBeVisible();
  await expect(slot.getByRole('link')).toHaveAttribute('href', /moi-nhat/i);
  const box = await slot.boundingBox();
  expect(box?.height).toBe(294);
  const imageResponse = await page.request.get('/images/banners/vnews-house-campaign.svg');
  expect(imageResponse.ok()).toBeTruthy();
  expect(imageResponse.headers()['content-type']).toMatch(/image\/svg\+xml|image\/svg/i);
});

test('mobile banner maintains the first-story layout without horizontal overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  const slot = page.locator('[data-ad-slot="top"]');
  await expect(slot).toBeVisible();
  const box = await slot.boundingBox();
  expect(box?.height).toBe(184);
  await expect(page.locator('.hero-lead__image-link img').first()).toBeVisible();
  const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  expect(scrollWidth).toBeLessThanOrEqual(391);
});
