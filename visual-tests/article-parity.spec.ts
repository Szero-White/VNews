import { test, expect, Page } from '@playwright/test';

// Layout regression for the article page against measurements taken from a standard article on
// https://vnexpress.net/ (2026-11-01, see artifacts/survey-2026-11c/before-measurements.json):
//   header+nav bottom 112px, breadcrumb 20px below it, headline 47px below the breadcrumb, 1100px content box,
//   680px text column, 80px gap, 300px side column, 32px/48px serif headline, 18px/28.8px body,
//   side-column thumbnails 110x66 with a 100px pitch, heading rule 1px under the heading text.
// These are geometry assertions, not link/element-count checks.

async function articleUrl(page: Page): Promise<string> {
  const list = await (await page.request.get('/api/v1/articles?category=cong-nghe&pageSize=50')).json(); // category filter: other specs publish cover-less articles into the newest slots
  const pick = list.items.find((i: any) => i.categorySlug === 'cong-nghe' && i.coverImageUrl) ?? list.items[0];
  return `/tin/${pick.slug}`;
}

type Box = { x: number; y: number; w: number; h: number };
const box = (page: Page, selector: string): Promise<Box> =>
  page.locator(selector).first().evaluate((e) => {
    const b = e.getBoundingClientRect();
    return { x: b.x, y: b.y + scrollY, w: b.width, h: b.height };
  });
const type = (page: Page, selector: string) =>
  page.locator(selector).first().evaluate((e) => {
    const c = getComputedStyle(e);
    return { family: c.fontFamily.split(',')[0].replace(/"/g, ''), size: c.fontSize, lh: c.lineHeight, weight: c.fontWeight };
  });
const overflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
const near = (actual: number, expected: number, tolerance: number, label: string) =>
  expect(Math.abs(actual - expected), `${label}: ${actual} vs ${expected} (±${tolerance})`).toBeLessThanOrEqual(tolerance);

for (const width of [1536, 1440]) {
  test(`desktop ${width}: header, breadcrumb, headline and two columns match the reference geometry`, async ({ page }) => {
    await page.setViewportSize({ width, height: width === 1536 ? 864 : 900 });
    await page.goto(await articleUrl(page));

    // No promotional banner above the masthead on article pages.
    await expect(page.locator('.house-promo')).toHaveCount(0);

    const nav = await box(page, '#primary-nav');
    near(nav.y + nav.h, 112, 1, 'navigation bottom');
    near(nav.h, 50, 0, 'navigation height');

    const bc = await box(page, '.article-topbar .breadcrumb');
    const title = await box(page, 'h1.article-detail__title');
    near(bc.y - (nav.y + nav.h), 20, 1, 'breadcrumb below navigation');
    near(title.y - bc.y, 47, 2, 'headline below breadcrumb');

    // 1100px content box centred; 40px inset; 680 text column, 80 gap, 300 side column.
    const col = await box(page, '.article-layout__main');
    const side = await box(page, '.article-layout__aside');
    near(col.x, (width - 1100) / 2 + 40, 1, 'article column x');
    near(col.w, 680, 0, 'article column width');
    near(side.w, 300, 0, 'side column width');
    near(side.x - (col.x + col.w), 80, 1, 'gap between columns');
    near(side.x + side.w, (width + 1100) / 2, 1, 'side column right edge');

    // Date sits on the breadcrumb line, flush with the text column's right edge.
    const date = await box(page, '.article-topbar__date');
    near(date.x + date.w, col.x + col.w, 1, 'date right edge');
    near(date.y, bc.y, 2, 'date on breadcrumb line');

    // Type.
    expect(await type(page, 'h1.article-detail__title')).toEqual({ family: 'Merriweather', size: '32px', lh: '48px', weight: '700' });
    for (const s of ['.article-detail__summary', '.article-detail__body p']) {
      const t = await type(page, s);
      expect([t.size, t.lh], s).toEqual(['18px', '28.8px']);
    }
    expect(await overflow(page)).toBeLessThanOrEqual(1);
  });
}

test('desktop: "Xem nhiều" is a thumbnail + headline list with a short rule under the heading', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(await articleUrl(page));

  const col = await box(page, '.article-layout__main');
  const heading = await box(page, '.popular-title__text');
  near(heading.y, col.y, 1, 'heading aligned with the top of the article column');
  expect(await type(page, '.popular-title__text')).toMatchObject({ family: 'Merriweather', size: '18px', lh: '28.8px', weight: '700' });

  // The rule: 1px, exactly as wide as the heading text, at its bottom edge (no negative margins / transforms).
  const rule = await page.locator('.popular-title__text').evaluate((e) => {
    const after = getComputedStyle(e, '::after');
    return { h: after.height, w: parseFloat(after.width), bottom: after.bottom, left: after.left, textW: e.getBoundingClientRect().width };
  });
  expect(rule.h).toBe('1px');
  near(rule.w, rule.textW, 1, 'rule width equals heading text width');
  expect(rule.bottom).toBe('0px');
  expect(rule.left).toBe('0px');
  expect(await page.locator('.article-layout__aside').evaluate((e) => getComputedStyle(e).borderTopWidth)).toBe('0px'); // no long bar above the heading

  const rows = page.locator('.popular-list__row');
  await expect(rows).toHaveCount(5);
  const thumbs = await page.locator('.popular-list__thumb img').evaluateAll((els) =>
    els.map((e) => { const b = e.getBoundingClientRect(); return { w: b.width, h: b.height, y: b.y + scrollY, x: b.x }; }));
  for (const t of thumbs) { near(t.w, 110, 0, 'thumb width'); near(t.h, 66, 0, 'thumb height'); }
  near((thumbs[4].y - thumbs[0].y) / 4, 100, 1, 'row pitch');
  // Thumbnails are lazy-loaded: poll until they have loaded instead of sampling the instant the page is ready (a race under load).
  await expect.poll(async () => (await page.locator('.popular-list__thumb img').evaluateAll((els) => els.map((e) => (e as HTMLImageElement).complete && (e as HTMLImageElement).naturalWidth > 0))).every(Boolean),
    { message: 'thumbnails load from real article covers' }).toBe(true);

  // Headline to the right of the thumbnail, 10px gap, serif 14px/22.4px.
  const item = await box(page, '.popular-list__item');
  near(item.x - (thumbs[0].x + 110), 10, 1, 'thumbnail to headline gap');
  expect(await type(page, '.popular-list__item')).toEqual({ family: 'Merriweather', size: '14px', lh: '22.4px', weight: '400' });
  // Thin separators between rows only.
  expect(await rows.first().evaluate((e) => getComputedStyle(e).borderBottomWidth)).toBe('1px');
  expect(await rows.last().evaluate((e) => getComputedStyle(e).borderBottomWidth)).toBe('0px');
});

test('desktop: the side column is sticky just under the navigation, without negative margins or transforms', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(await articleUrl(page));

  const style = await page.locator('.article-layout__aside').evaluate((e) => {
    const c = getComputedStyle(e);
    return { position: c.position, top: c.top, margin: [c.marginTop, c.marginRight, c.marginBottom, c.marginLeft], transform: c.transform };
  });
  expect(style.position).toBe('sticky');
  expect(style.top).toBe('70px'); // 50px navigation + 20px
  expect(style.margin.every((m) => parseFloat(m) >= 0)).toBe(true);
  expect(style.transform).toBe('none');

  await page.evaluate(() => window.scrollTo(0, 700));
  await page.waitForTimeout(200);
  const top = await page.locator('.article-layout__aside').evaluate((e) => e.getBoundingClientRect().top);
  near(top, 70, 1, 'aside top after scrolling');
  const navBottom = await page.locator('#primary-nav').evaluate((e) => e.getBoundingClientRect().bottom);
  expect(top).toBeGreaterThanOrEqual(navBottom + 19);
});

test('desktop: reading tools are one quiet row under the summary, not boxed buttons', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(await articleUrl(page));

  const summary = await box(page, '.article-detail__summary');
  const tools = await box(page, '.article-tools');
  const cover = await box(page, '.article-cover-wrap');
  expect(tools.y).toBeGreaterThan(summary.y + summary.h);
  expect(tools.h).toBeLessThanOrEqual(36);
  expect(cover.y - (tools.y + tools.h)).toBeGreaterThanOrEqual(16);
  expect(await page.locator('.article-tools').evaluate((e) => getComputedStyle(e).borderTopWidth)).toBe('0px');
  expect(await page.locator('.article-tool').first().evaluate((e) => getComputedStyle(e).borderTopWidth)).toBe('0px');

  // Text-size controls sit at the right edge of the column.
  const col = await box(page, '.article-layout__main');
  const fontUp = await box(page, '[data-action="font-up"]');
  near(fontUp.x + fontUp.w, col.x + col.w, 8, 'A+ at the right edge');
});

test('mobile 390: article starts right under the navigation, one column, readable type', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(await articleUrl(page));

  await expect(page.locator('.house-promo')).toHaveCount(0);
  await expect(page.locator('.mobile-weather-row')).toBeHidden();
  const nav = await box(page, '#primary-nav');
  const bc = await box(page, '.article-topbar .breadcrumb');
  near(nav.y + nav.h, 108, 2, 'navigation bottom'); // reference mobile header 58px + 50px strip
  near(bc.y - (nav.y + nav.h), 20, 2, 'breadcrumb below navigation');

  expect(await type(page, 'h1.article-detail__title')).toEqual({ family: 'Merriweather', size: '22px', lh: '35.75px', weight: '700' });
  const body = await type(page, '.article-detail__body p');
  expect([body.size, body.lh]).toEqual(['18px', '28.8px']);

  const col = await box(page, '.article-layout__main');
  const side = await box(page, '.article-layout__aside');
  near(col.w, 354, 1, 'column fills the viewport minus gutters');
  expect(side.y).toBeGreaterThan(col.y + col.h - 1);

  for (const t of await page.locator('.article-tool').all()) expect((await t.boundingBox())!.height).toBeGreaterThanOrEqual(36);
  const thumb = await box(page, '.popular-list__thumb img');
  expect([Math.round(thumb.w), Math.round(thumb.h)]).toEqual([110, 66]);
  expect(await overflow(page)).toBeLessThanOrEqual(1);
});

test('only the homepage has a banner above the masthead; listing pages start with header at 112px navigation bottom', async ({ page }) => {
  for (const [width, height, navBottom] of [[1440, 900, 112], [390, 844, 108]]) {
    await page.setViewportSize({ width, height });
    for (const url of ['/chuyen-muc/cong-nghe', '/tin-moi-nhat', '/tim-kiem?q=gia']) {
      await page.goto(url);
      await expect(page.locator('.house-promo')).toHaveCount(0);
      const masthead = await box(page, '.masthead');
      const nav = await box(page, '#primary-nav');
      near(masthead.y, 0, 0, url + ': masthead at the top');
      near(masthead.h, width === 1440 ? 62 : 57, 1, url + ': header height');
      near(nav.y + nav.h, navBottom, 1, url + ': navigation bottom @' + width);
    }
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await expect(page.locator('.top-ad-space')).toBeVisible(); // the homepage keeps its own banner
});
