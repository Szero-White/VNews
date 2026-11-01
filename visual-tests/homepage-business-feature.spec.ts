import { test, expect } from '@playwright/test';

test('business has a dedicated linked editorial panel without invented market values', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/?visualFixture=1');
  const business = page.locator('[data-home-business]');
  await expect(business).toHaveCount(1);
  // Scoped to the panel's own section heading, not any article headline that happens to
  // contain the word "Kinh doanh" (getByRole name-matching is substring-based by default).
  await expect(business.locator('.business-feature__header .section-title')).toHaveText('Kinh doanh');
  await expect(page.locator('.home-parallel__content > .topic-spotlight')).toHaveCount(9);
  await expect(business.locator('.business-feature__topics a')).toHaveCount(5);
  await expect(business.locator('.business-feature__market-grid a')).toHaveCount(3);
  await expect(business.getByText('VNews chưa cung cấp giá thị trường theo thời gian thực.')).toBeVisible();
  const articles = business.locator('article');
  await expect(articles).toHaveCount(2);
  const linkedHeadlines = business.locator('article h3 a, .business-feature__headlines a');
  await expect(linkedHeadlines).toHaveCount(5);
  const urls = await linkedHeadlines.evaluateAll(nodes => nodes.map(node => node.getAttribute('href')));
  expect(new Set(urls).size).toBe(5);
  expect(urls.every(url => !!url && url.startsWith('/') && !url.includes('#'))).toBeTruthy();
  const stream = await page.locator('.home-parallel__stream').boundingBox();
  const feature = await business.boundingBox();
  expect(feature!.x).toBeGreaterThan(stream!.x + stream!.width);
});

test('mobile business module retains links and fits a 390px viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/?visualFixture=1');
  const business = page.locator('[data-home-business]');
  await business.scrollIntoViewIfNeeded();
  await expect(business.locator('.business-feature__lead-image img')).toBeVisible();
  await expect(business.locator('.business-feature__headlines li')).toHaveCount(3);
  await expect(business.locator('.business-feature__market-grid a')).toHaveCount(3);
  await expect(business.locator('.business-feature__topics a').first()).toHaveAttribute('href', /\/tim-kiem\?/);
  const sizes = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(sizes.scroll).toBeLessThanOrEqual(sizes.client + 1);
});
