import { test, expect, Page } from '@playwright/test';

// Vietnamese search: accented / unaccented / upper-case / extra-space input finds the same published articles.
// Layout reference: timkiem.vnexpress.net (captured 2026-11-01): one column, headline-first rows, thumbnail on the right.

const overflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
const titles = (page: Page) => page.locator('.search-page .story-row__title a').allInnerTexts();

async function searchVia(page: Page, typed: string) {
  await page.goto('/tim-kiem');
  await page.locator('#page-search').fill(typed);
  await page.locator('.search-page-form button[type="submit"]').click();
  await expect(page).toHaveURL(/\/tim-kiem\?q=/);
}

test('"giá vàng", "gia vang", upper case and extra spaces find the same articles', async ({ page }) => {
  const results: string[][] = [];
  for (const typed of ['giá vàng', 'gia vang', 'GIÁ  VÀNG', '  Gia   Vàng  ']) {
    await searchVia(page, typed);
    const found = await titles(page);
    expect(found.length, typed).toBeGreaterThan(0);
    results.push(found);
  }
  for (const r of results) expect(r).toEqual(results[0]);
  expect(results[0].some(t => /giá vàng/i.test(t))).toBe(true);
});

test('header search box also finds accent-less input', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await page.locator('.masthead__desktop-search input[name="q"]').fill('gia vang');
  await page.locator('.masthead__desktop-search input[name="q"]').press('Enter');
  await expect(page).toHaveURL(/\/tim-kiem\?q=gia(\+|%20)vang/);
  expect((await titles(page)).some(t => /giá vàng/i.test(t))).toBe(true);
});

test('result links open real articles and the count matches the list', async ({ page }) => {
  await page.goto('/tim-kiem?q=gia+vang');
  const count = Number(await page.locator('.search-count strong').innerText());
  expect(count).toBe((await titles(page)).length); // fewer than one page of results here
  const href = (await page.locator('.search-page .story-row__title a').first().getAttribute('href'))!;
  expect((await page.request.get(href)).status()).toBe(200);
});

test('many results paginate without repeating an article and the keyword survives paging', async ({ page }) => {
  await page.goto('/tim-kiem?q=bai+viet');
  const total = Number(await page.locator('.search-count strong').innerText());
  expect(total).toBeGreaterThan(12);
  // Up to 7 page numbers are all shown; beyond that the pager collapses (1 … current … last), and the last label is the page count.
  // (Other specs publish articles whose titles match this keyword, so the result set grows over time.)
  const pages = Math.ceil(total / 12);
  const numbers = page.locator('.pagination__item');
  if (pages <= 7) expect(await numbers.count()).toBe(pages);
  else expect(Number(await numbers.last().innerText())).toBe(pages);

  const seen: string[] = [];
  for (let p = 1; p <= pages; p++) {
    await page.goto(`/tim-kiem?q=bai+viet&page=${p}`);
    seen.push(...(await page.locator('.search-page .story-row__title a').evaluateAll(els => els.map(e => e.getAttribute('href')!))));
    expect(await page.locator('.pagination__item.is-current').innerText()).toBe(String(p));
  }
  expect(seen.length).toBe(total);
  expect(new Set(seen).size).toBe(total);

  await page.goto('/tim-kiem?q=bai+viet');
  await page.locator('.pagination__item', { hasText: /^\s*2\s*$/ }).click();
  await expect(page).toHaveURL(/q=bai(\+|%20)viet.*page=2|page=2.*q=bai(\+|%20)viet/);
  await expect(page.locator('#page-search')).toHaveValue('bai viet');
});

test('empty results, symbols and very long input stay usable', async ({ page }) => {
  await page.goto('/tim-kiem?q=zzzkhongcotuhoanto');
  await expect(page.locator('.empty-state')).toContainText('Không tìm thấy bài viết');

  for (const typed of ['%', '_', "' OR 1=1 --", '<script>alert(1)</script>', '!!!']) {
    const response = await page.goto('/tim-kiem?q=' + encodeURIComponent(typed));
    expect(response!.status(), typed).toBe(200);
    await expect(page.locator('.empty-state')).toContainText('Không tìm thấy');
    expect(await page.locator('.search-page .story-row').count(), typed).toBe(0);
  }

  const long = await page.goto('/tim-kiem?q=' + 'giá '.repeat(300));
  expect(long!.status()).toBe(200);
  await page.locator('#page-search').fill('x'.repeat(500));
  expect((await page.locator('#page-search').inputValue()).length).toBe(120); // input is capped by maxlength
});

test('desktop layout: single 760px column, thumbnail on the right of each row', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/tim-kiem?q=gia+vang');
  const column = (await page.locator('.search-page').boundingBox())!;
  expect(column.width).toBeLessThanOrEqual(762);
  const row = page.locator('.search-page .story-row').first();
  const image = (await row.locator('.story-row__image-link').boundingBox())!;
  const body = (await row.locator('.story-row__body').boundingBox())!;
  expect(image.x).toBeGreaterThan(body.x);
  expect(await page.locator('.search-page .story-row__title').first().evaluate(e => getComputedStyle(e).fontFamily)).toMatch(/^Merriweather/);
  expect(await overflow(page)).toBeLessThanOrEqual(1);
});

test('mobile layout: no overflow, thumbnail stays beside the headline', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/tim-kiem?q=gia+vang');
  expect(await overflow(page)).toBeLessThanOrEqual(1);
  const row = page.locator('.search-page .story-row').first();
  const image = (await row.locator('.story-row__image-link').boundingBox())!;
  const body = (await row.locator('.story-row__body').boundingBox())!;
  expect(image.x).toBeGreaterThan(body.x);
  expect(image.x + image.width).toBeLessThanOrEqual(391);
});
