import { expect, test } from '@playwright/test';

// These checks protect page structure and real routes. They deliberately do not
// claim pixel parity with live third-party content or changing ad campaigns.
test('desktop shell has reserved campaign area and functional public utilities', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const cover = page.locator('.page-home .top-ad-space');
  await expect(cover).toBeVisible();
  await expect(cover).toHaveAttribute('data-ad-state', 'house');
  await expect(cover.getByRole('link', { name: /VNews: Khám phá tin tức mới nhất/i })).toHaveAttribute('href', /moi-nhat/i);
  const top = await cover.boundingBox();
  expect(top?.height).toBeGreaterThanOrEqual(285);
  expect(top?.height).toBeLessThanOrEqual(300);
  await expect(page.getByRole('link', { name: 'Mới nhất', exact: true }).first()).toHaveAttribute('href', /moi-nhat/i);
  await expect(page.getByRole('link', { name: 'Bài đã lưu' }).first()).toHaveAttribute('href', /da-luu/i);
  await expect(page.locator('.primary-nav__latest')).toBeHidden();
  await expect(page.locator('.hero-lead__title a')).toHaveAttribute('href', /\S/);
});

test('mobile uses category strip and retains saved, account and full story stream', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.locator('.primary-nav__latest')).toBeVisible();
  await expect(page.locator('.masthead__saved-mobile')).toBeVisible();
  await expect(page.locator('.masthead__account-link')).toBeVisible();
  const feed = page.locator('.home-parallel__stream .stream-compact');
  expect(await feed.count()).toBeGreaterThanOrEqual(5);
  await expect(feed.nth(4)).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});
