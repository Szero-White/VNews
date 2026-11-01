import { test, expect } from '@playwright/test';

// This checks a genuine editorial variant and real routes, not pixel identity
// with an external site whose content and advertising change every visit.
test('technology gets a full-width magazine module with unique linked articles', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/?visualFixture=1');
  const feature = page.locator('[data-home-technology]');
  await expect(feature).toHaveCount(1);
  await expect(feature.getByRole('heading', { name: 'Khoa học công nghệ' })).toBeVisible();
  const articles = feature.locator('article');
  await expect(articles).toHaveCount(5);
  const hrefs = await articles.locator('h3 a').evaluateAll(nodes => nodes.map(node => node.getAttribute('href')));
  expect(hrefs.length).toBe(5);
  expect(new Set(hrefs).size).toBe(5);
  expect(hrefs.every(href => !!href && !href.includes('#'))).toBeTruthy();
  await expect(feature.locator('.technology-feature__topics a')).toHaveCount(4);
  const content = page.locator('.home-parallel__content');
  await expect(content.locator('.topic-spotlight--cong-nghe')).toHaveCount(0);
  const techBounds = await feature.boundingBox();
  const streamBounds = await page.locator('.home-parallel__stream').boundingBox();
  expect(techBounds!.width).toBeGreaterThan(streamBounds!.width * 2);
});

test('mobile technology module is readable and does not overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/?visualFixture=1');
  const feature = page.locator('[data-home-technology]');
  await feature.scrollIntoViewIfNeeded();
  await expect(feature.locator('article')).toHaveCount(5);
  const widths = await page.evaluate(() => ({
    content: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }));
  expect(widths.content).toBeLessThanOrEqual(widths.client + 1);
});
