import { test, expect } from '@playwright/test';

// Functional smoke tests on local VNews (do not require destructive CMS writes).
test('homepage has multiple editorial layouts and linked sections', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('.front-page')).toBeVisible();
  await expect(page.locator('.home-parallel')).toBeVisible();
  await expect(page.locator('.today-digest')).toBeVisible();
  await expect(page.locator('.home-parallel__content > .topic-spotlight').first()).toBeVisible();
  await expect(page.locator('.photo-gallery-v2')).toBeVisible();
  await expect(page.locator('.podcast-grid')).toBeVisible();
  // Technology is rendered separately as a full-width magazine module;
  // the other nine seeded categories remain in the parallel column.
  await expect(page.locator('.home-parallel__content > .topic-spotlight')).toHaveCount(9);
  await expect(page.locator('[data-home-technology]')).toHaveCount(1);
});

test('latest page is real and article links open', async ({ page }) => {
  await page.goto('/tin-moi-nhat');
  await expect(page.locator('h1')).toContainText('Tin mới nhất');
  const link = page.locator('.editorial-listing__articles .story-row__title a').first();
  await expect(link).toBeVisible();
  await link.click();
  await expect(page.locator('.article-detail__title')).toBeVisible();
});

test('save article and open saved page in same browser', async ({ page }) => {
  await page.goto('/tin-moi-nhat');
  await page.locator('.story-row__title a').first().click();
  const title = await page.locator('h1').innerText();
  const save = page.locator('[data-action="save"]');
  await save.click();
  await expect(save).toHaveAttribute('aria-pressed','true');
  await page.goto('/da-luu');
  await expect(page.locator('.saved-article__title').first()).toContainText(title);
  await page.locator('.saved-article__delete').first().click();
  await expect(page.locator('.saved-article')).toHaveCount(0);
});

test('search form and results work against published DB content', async ({ page }) => {
  await page.goto('/tim-kiem');
  await expect(page.locator('#page-search')).toBeVisible();
  await page.locator('#page-search').fill('giáo dục');
  await page.locator('.search-page-form button[type="submit"]').click();
  await expect(page).toHaveURL(/\/tim-kiem\?/);
  expect(new URL(page.url()).searchParams.get('q')).toBe('giáo dục');
  await expect(page.locator('.listing-page__title')).toContainText('Tìm kiếm');
});

test('mobile navigation can open, click and close', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  const toggle = page.locator('.masthead__menu-toggle');
  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-expanded', 'true');
  await expect(page.locator('#primary-nav')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(toggle).toHaveAttribute('aria-expanded', 'false');
  const noOverflow = await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1);
  expect(noOverflow).toBeTruthy();
});
