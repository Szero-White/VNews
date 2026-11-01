import { test, expect } from '@playwright/test';

test('desktop masthead exposes working search and published headlines', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const search = page.locator('.masthead__desktop-search');
  await expect(search).toBeVisible();
  await search.locator('input[name="q"]').fill('công nghệ');
  await search.locator('button[type="submit"]').click();
  await expect(page).toHaveURL(/q=/);
  await page.goto('/');
  const panel = page.locator('#reader-news-panel');
  await panel.locator('summary').click();
  await expect(panel.locator('#reader-news-content-latest')).toBeVisible();
  await expect(panel.locator('#reader-news-content-latest a[href^="/tin/"]').first()).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(panel).not.toHaveAttribute('open', '');
});

test('recently read is a real browser-local reading history', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const story = page.locator('.news-home .hero-lead__title a').first();
  await story.click();
  await expect(page.locator('article[data-news-article]')).toBeVisible();
  const title = await page.locator('article[data-news-article]').getAttribute('data-news-title');
  await page.goto('/');
  const panel = page.locator('#reader-news-panel');
  await panel.locator('summary').click();
  await panel.locator('[data-reader-news-tab="seen"]').click();
  await expect(panel.locator('#reader-news-recent-list a').first()).toContainText(title || '');
});

test('mobile keeps the compact real search instead of showing desktop form', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.locator('.masthead__desktop-search')).toBeHidden();
  await expect(page.locator('.masthead__search--mobile-left')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});
