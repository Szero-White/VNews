import { test, expect } from '@playwright/test';

// Footer layout reference: the footer of https://vnexpress.net/ (Vietnamese edition, captured 2026-11-01):
// a wide multi-column link grid. VNews keeps its own brand/content and shows only real links.

test('desktop footer is a multi-column grid with all 10 database categories', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const footer = page.locator('footer.site-footer');
  await footer.scrollIntoViewIfNeeded();

  const categoriesApi = await (await page.request.get('/api/v1/categories')).json();
  const links = footer.locator('.site-footer__col--categories a');
  await expect(links).toHaveCount(categoriesApi.length);
  expect(categoriesApi.length).toBeGreaterThanOrEqual(10);
  for (const c of categoriesApi) {
    await expect(footer.locator(`a[href="/chuyen-muc/${c.slug}"]`)).toHaveCount(1);
  }

  // Several side-by-side columns: brand, categories, 3 link groups share one row.
  const tops = await footer.locator('.site-footer__grid > *').evaluateAll(els => els.map(e => Math.round(e.getBoundingClientRect().top)));
  expect(tops.length).toBeGreaterThanOrEqual(5);
  expect(new Set(tops).size).toBe(1);

  // Categories flow into two columns (10 items, not one tall list).
  const catBox = await footer.locator('.site-footer__col--categories ul').boundingBox();
  expect(catBox!.height).toBeLessThan(10 * 30);

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
});

test('mobile footer stacks into compact columns without horizontal overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  const footer = page.locator('footer.site-footer');
  await footer.scrollIntoViewIfNeeded();
  await expect(footer.locator('.site-footer__col--categories a')).toHaveCount(10);

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);

  // Every link stays inside the viewport; column links have a comfortable tap height.
  const boxes = await footer.locator('.site-footer__col a').evaluateAll(els => els.map(e => { const r = e.getBoundingClientRect(); return { right: r.right, h: r.height }; }));
  for (const b of boxes) {
    expect(b.right).toBeLessThanOrEqual(391);
    expect(b.h).toBeGreaterThanOrEqual(26);
  }
});

test('footer links open real pages and footer pages are reachable by clicking', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const hrefs = await page.locator('footer.site-footer a').evaluateAll(els => Array.from(new Set(els.map(e => e.getAttribute('href')))));
  expect(hrefs.length).toBeGreaterThanOrEqual(16);
  for (const href of hrefs) {
    const response = await page.request.get(href!);
    expect(response.status(), href!).toBe(200);
  }

  for (const [label, heading] of [['Giới thiệu', 'Giới thiệu VNews'], ['Điều khoản sử dụng', 'Điều khoản sử dụng'], ['Chính sách bảo mật', 'Chính sách bảo mật']]) {
    await page.goto('/');
    await page.locator('footer.site-footer').getByRole('link', { name: label, exact: true }).click();
    await expect(page.locator('h1.static-page__title')).toHaveText(heading);
  }
});

test('footer shows no invented RSS, licence, contact or app-download content', async ({ page }) => {
  await page.goto('/');
  const text = await page.locator('footer.site-footer').innerText();
  for (const bad of [/RSS/i, /Số giấy phép|Giấy phép hoạt động|Giấy phép xuất bản/i, /Tổng biên tập/i, /App Store/i, /Google Play/i, /Tải ứng dụng/i, /Hotline/i]) {
    expect(text).not.toMatch(bad);
  }
});
