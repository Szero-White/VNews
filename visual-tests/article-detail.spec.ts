import { test, expect, Page } from '@playwright/test';

// Layout reference: a standard article on https://vnexpress.net/ (captured 2026-11-01): breadcrumb + dateline row,
// serif headline, summary, body, sign-off, related stories and a "Xem nhiều" side column. All data is real.

async function firstArticleSlug(page: Page): Promise<string> {
  const json = await (await page.request.get('/api/v1/articles?pageSize=1')).json();
  return json.items[0].slug;
}
const overflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

test('desktop article: dateline row, serif title, sign-off, back link, side column of most-viewed', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  const slug = await firstArticleSlug(page);
  await page.goto(`/tin/${slug}`);
  const apiArticle = await (await page.request.get(`/api/v1/articles/${slug}`)).json();

  await expect(page.locator('.article-topbar .breadcrumb')).toContainText(apiArticle.categoryName);
  await expect(page.locator('.article-topbar__date')).toHaveText(/^(Chủ nhật|Thứ \p{L}+), \d{1,2}\/\d{1,2}\/\d{4}, \d{2}:\d{2} \(GMT\+7\)$/u);
  await expect(page.locator('h1.article-detail__title')).toHaveText(apiArticle.title);
  expect(await page.locator('h1.article-detail__title').evaluate(e => getComputedStyle(e).fontFamily)).toMatch(/^Merriweather/);
  await expect(page.locator('.article-detail__summary')).not.toBeEmpty();
  await expect(page.locator('.article-detail__body')).not.toBeEmpty();
  await expect(page.locator('.article-signoff__name')).toHaveText(apiArticle.authorName);

  const back = page.locator('.article-back-link');
  await expect(back).toContainText(`Trở lại ${apiArticle.categoryName}`);
  expect(await back.getAttribute('href')).toBe(`/chuyen-muc/${apiArticle.categorySlug}`);

  // Two columns: article ~760px wide, side column to its right.
  const main = (await page.locator('.article-layout__main').boundingBox())!;
  const aside = (await page.locator('.article-layout__aside').boundingBox())!;
  expect(main.width).toBeGreaterThan(600);
  expect(main.width).toBeLessThanOrEqual(780);
  expect(aside.x).toBeGreaterThan(main.x + main.width);

  // "Xem nhiều": real articles, current one excluded, every link works.
  const hrefs = await page.locator('.popular-list__item').evaluateAll(els => els.map(e => e.getAttribute('href')!));
  expect(hrefs.length).toBe(5);
  expect(hrefs).not.toContain(`/tin/${slug}`);
  for (const href of hrefs) expect((await page.request.get(href)).status(), href).toBe(200);

  // Related stories come from the same category and exclude the current article.
  const related = await page.locator('.article-related__card').evaluateAll(els => els.map(e => e.getAttribute('href')!));
  expect(related.length).toBeGreaterThan(0);
  expect(related).not.toContain(`/tin/${slug}`);

  expect(await overflow(page)).toBeLessThanOrEqual(1);
});

// The dev database has no tag rows, so only the "no data => no block" and "links work if present" rules are
// checked here; the tag block with data is covered by ArticleDetailIntegrationTests (isolated database).
test('tag block appears only with real tags and its links work', async ({ page }) => {
  const slug = await firstArticleSlug(page);
  await page.goto(`/tin/${slug}`);
  const links = await page.locator('.article-tags a').evaluateAll(els => els.map(e => e.getAttribute('href')!));
  await expect(page.locator('.article-tags')).toHaveCount(links.length > 0 ? 1 : 0);
  for (const href of links) expect((await page.request.get(href)).status(), href).toBe(200);
});

test('mobile article: single column, no overflow, side column stacks below', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const slug = await firstArticleSlug(page);
  await page.goto(`/tin/${slug}`);
  expect(await overflow(page)).toBeLessThanOrEqual(1);

  const main = (await page.locator('.article-layout__main').boundingBox())!;
  const aside = (await page.locator('.article-layout__aside').boundingBox())!;
  expect(main.width).toBeGreaterThan(330);
  expect(aside.y).toBeGreaterThan(main.y + main.height - 1);
  // The cover is optional (articles created in the CMS may have none); boundingBox() would wait for a missing element.
  const cover = await page.locator('.article-detail__cover').count() ? await page.locator('.article-detail__cover').boundingBox() : null;
  if (cover) expect(cover.x + cover.width).toBeLessThanOrEqual(391);
  for (const card of await page.locator('.article-related__card').all()) {
    const box = (await card.boundingBox())!;
    expect(box.x + box.width).toBeLessThanOrEqual(391);
  }
  expect(await page.locator('h1.article-detail__title').evaluate(e => getComputedStyle(e).fontFamily)).toMatch(/^Merriweather/);
});

test('reading tools still work: font size, save, share', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']).catch(() => {});
  await page.setViewportSize({ width: 1440, height: 900 });
  const slug = await firstArticleSlug(page);
  await page.goto(`/tin/${slug}`);

  const body = page.locator('#article-body');
  const before = parseFloat(await body.evaluate(e => getComputedStyle(e).fontSize));
  await page.locator('[data-action="font-up"]').click();
  expect(parseFloat(await body.evaluate(e => getComputedStyle(e).fontSize))).toBe(before + 1);
  await page.locator('[data-action="font-down"]').click();
  await page.locator('[data-action="font-down"]').click();
  expect(parseFloat(await body.evaluate(e => getComputedStyle(e).fontSize))).toBe(before - 1);

  const save = page.locator('.article-bottom-actions [data-action="save"]');
  await expect(save).toHaveAttribute('aria-pressed', 'false');
  await save.click();
  await expect(save).toHaveAttribute('aria-pressed', 'true');
  await save.click();
  await expect(save).toHaveAttribute('aria-pressed', 'false');

  await page.locator('.article-tools [data-action="share"]').click();
  await expect(page.locator('#site-toast')).toBeVisible();

  await expect(page.locator('.article-tools [data-action="speak"]')).toBeVisible();
});
