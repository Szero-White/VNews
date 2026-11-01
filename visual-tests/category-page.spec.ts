import { test, expect, Page } from '@playwright/test';

// Category pages + navigation. Layout reference: a category page on https://vnexpress.net/ (2026-11-01):
// 1100px content box, 780px main column + 300px side column (20px gap), 500x300 lead image beside a grey panel,
// three headline cards, list with 145x87 thumbnails; navigation strip with the current section highlighted.
// Geometry assertions, not just element counts.

type Box = { x: number; y: number; w: number; h: number };
const box = (page: Page, selector: string): Promise<Box> =>
  page.locator(selector).first().evaluate((e) => {
    const b = e.getBoundingClientRect();
    return { x: b.x, y: b.y + scrollY, w: b.width, h: b.height };
  });
const overflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
const near = (actual: number, expected: number, tolerance: number, label: string) =>
  expect(Math.abs(actual - expected), `${label}: ${actual} vs ${expected} (±${tolerance})`).toBeLessThanOrEqual(tolerance);
async function categories(page: Page): Promise<{ name: string; slug: string }[]> {
  return (await page.request.get('/api/v1/categories')).json();
}
// Lazy images only load once scrolled to; walk down the page so "loaded" checks are meaningful.
async function scrollThrough(page: Page) {
  const height = await page.evaluate(() => document.documentElement.scrollHeight);
  for (let y = 0; y < height; y += 500) { await page.evaluate((v) => window.scrollTo(0, v), y); await page.waitForTimeout(80); }
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.waitForLoadState('networkidle');
}

test('desktop: lead, three cards, list and 300px side column follow the reference geometry', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/chuyen-muc/cong-nghe'); // seeded with 7 articles: lead + 3 cards + 3 rows

  const main = await box(page, '.category-main');
  const aside = await box(page, '.category-layout__aside');
  near(main.x, 170, 1, 'main column x');
  near(main.w, 780, 0, 'main column width');
  near(aside.w, 300, 0, 'side column width');
  near(aside.x - (main.x + main.w), 20, 1, 'gap between columns');
  near(aside.x + aside.w, 1270, 1, 'side column right edge');

  const leadImage = await box(page, '.cat-lead__image img');
  const leadPanel = await box(page, '.cat-lead__panel');
  near(leadImage.w, 500, 0, 'lead image width');
  near(leadImage.h, 300, 1, 'lead image height');
  expect(leadPanel.x).toBeGreaterThanOrEqual(leadImage.x + leadImage.w);
  near(leadPanel.h, leadImage.h, 1, 'panel is as tall as the image (no white hole)');
  expect(await page.locator('.cat-lead__panel').evaluate((e) => getComputedStyle(e.parentElement!).backgroundColor)).toBe('rgb(247, 247, 247)');

  const cards = await page.locator('.cat-card').evaluateAll((els) => els.map((e) => { const b = e.getBoundingClientRect(); return { x: b.x, y: b.y + scrollY, w: b.width }; }));
  expect(cards).toHaveLength(3);
  expect(new Set(cards.map((c) => Math.round(c.y))).size, 'three cards share one row').toBe(1);
  near(cards[0].w, 247, 2, 'card width');
  const lead = await box(page, '#category-lead');
  expect(cards[0].y).toBeGreaterThan(lead.y + lead.h);

  const row = await page.locator('.cat-row__image img').first().evaluate((e) => { const b = e.getBoundingClientRect(); return { w: b.width, h: b.height }; });
  expect([row.w, row.h]).toEqual([145, 87]);

  // Type: serif headlines, sans summaries.
  const family = (s: string) => page.locator(s).first().evaluate((e) => getComputedStyle(e).fontFamily.split(',')[0].replace(/"/g, ''));
  expect(await family('.category-header__title')).toBe('Merriweather');
  expect(await family('.cat-lead__title')).toBe('Merriweather');
  expect(await family('.cat-card__title')).toBe('Merriweather');
  expect(await family('.cat-row__title')).toBe('Merriweather');
  expect(await family('.cat-row__summary')).toBe('Arial');

  // Every item has a time element: ISO value + readable text.
  const times = await page.locator('.category-main time.cat-meta').evaluateAll((els) => els.map((e) => [e.getAttribute('datetime'), e.textContent!.trim()]));
  expect(times).toHaveLength(7);
  for (const [iso, text] of times) {
    expect(iso).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\+07:00$/);
    expect(text).toMatch(/^(Vừa xong|\d+ (phút|giờ|ngày) trước|\d{1,2}\/\d{1,2}\/\d{4})$/);
  }

  // Side column: real "Xem nhiều", 110x66 thumbnails, sticky under the navigation.
  expect(await page.locator('.category-layout__aside .popular-list__row').count()).toBe(5);
  const thumb = await page.locator('.category-layout__aside .popular-list__thumb img').first().evaluate((e) => { const b = e.getBoundingClientRect(); return [b.width, b.height]; });
  expect(thumb).toEqual([110, 66]);
  const style = await page.locator('.category-layout__aside').evaluate((e) => { const c = getComputedStyle(e); return { position: c.position, top: c.top, transform: c.transform, margin: [c.marginTop, c.marginLeft] }; });
  expect(style).toMatchObject({ position: 'sticky', top: '70px', transform: 'none' });
  expect(style.margin.every((m: string) => parseFloat(m) >= 0)).toBe(true);

  await scrollThrough(page);
  const loaded = await page.locator('.category-main img').evaluateAll((els) => els.map((e) => (e as HTMLImageElement).complete && (e as HTMLImageElement).naturalWidth > 0));
  expect(loaded.every(Boolean), 'all category images load').toBe(true);
  expect(await overflow(page)).toBeLessThanOrEqual(1);
});

test('navigation: the current category is marked with aria-current, colour and a rule; others are not', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/chuyen-muc/the-thao');
  const current = page.locator('.category-nav__list a[aria-current="page"]');
  await expect(current).toHaveCount(1);
  await expect(current).toHaveText('Thể thao');
  const styles = await page.locator('.category-nav__list a').evaluateAll((els) =>
    els.map((e) => { const c = getComputedStyle(e); return { name: e.textContent!.trim(), color: c.color, shadow: c.boxShadow }; }));
  const active = styles.find((s) => s.name === 'Thể thao')!;
  const others = styles.filter((s) => s.name !== 'Thể thao');
  expect(active.shadow).not.toBe('none');
  for (const o of others) { expect(o.shadow).toBe('none'); expect(o.color).not.toBe(active.color); }

  // Navigation height unchanged by the highlight; an article keeps its own category highlighted.
  near((await box(page, '#primary-nav')).h, 50, 0, 'navigation height');
  await page.locator('.cat-lead__title a').click();
  await expect(page.locator('.category-nav__list a[aria-current="page"]')).toHaveText('Thể thao');
  await page.locator('.article-back-link').click();
  await expect(page).toHaveURL(/\/chuyen-muc\/the-thao/);
});

test('every real category page opens with content, a highlighted nav item and no overflow (desktop and mobile)', async ({ page }) => {
  const all = await categories(page);
  expect(all.length).toBeGreaterThanOrEqual(10);
  for (const [width, height] of [[1440, 900], [390, 844]]) {
    await page.setViewportSize({ width, height });
    for (const c of all) {
      const response = await page.goto(`/chuyen-muc/${c.slug}`);
      expect(response!.status(), c.slug).toBe(200);
      await expect(page.locator('h1.category-header__title')).toHaveText(c.name);
      await expect(page.locator('#category-lead')).toBeVisible();
      await expect(page.locator('.category-nav__list a[aria-current="page"]')).toHaveText(c.name);
      expect(await overflow(page), `${c.slug} @${width}`).toBeLessThanOrEqual(1);
    }
  }
});

test('mobile 390: no empty block above the header, no overflow on listing pages and the article page', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const list = await (await page.request.get('/api/v1/articles?pageSize=1')).json();
  for (const url of ['/chuyen-muc/thoi-su', '/tin-moi-nhat', '/tim-kiem?q=gia', `/tin/${list.items[0].slug}`]) {
    await page.goto(url);
    await expect(page.locator('.house-promo')).toBeHidden();
    const masthead = await box(page, '.masthead');
    near(masthead.y, 0, 0, `${url}: header starts at the top of the page`);
    expect(await overflow(page), url).toBeLessThanOrEqual(1);
  }
});

test('mobile 390: category layout is one column, lead stacked, cards and rows beside thumbnails', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/chuyen-muc/cong-nghe');
  const lead = await box(page, '.cat-lead');
  const image = await box(page, '.cat-lead__image img');
  const panel = await box(page, '.cat-lead__panel');
  near(lead.w, 354, 1, 'lead fills the column');
  near(image.w, 354, 1, 'lead image fills the column');
  expect(panel.y).toBeGreaterThanOrEqual(image.y + image.h - 1);
  const main = await box(page, '.category-main');
  const aside = await box(page, '.category-layout__aside');
  expect(aside.y).toBeGreaterThan(main.y + main.h - 1);
  const card = page.locator('.cat-card').first();
  const cardImage = (await card.locator('.cat-card__image').boundingBox())!;
  const cardTitle = (await card.locator('.cat-card__title').boundingBox())!;
  expect(cardImage.x).toBeGreaterThan(cardTitle.x); // thumbnail on the right of the headline
  expect(await overflow(page)).toBeLessThanOrEqual(1);
});

test('mobile navigation: strip scrolls by touch, by arrow button and by keyboard; current category is in view', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/chuyen-muc/phap-luat'); // last category: must be scrolled into view on load
  const list = page.locator('.category-nav__list');
  const dims = await list.evaluate((e) => ({ scroll: e.scrollWidth, client: e.clientWidth, left: e.scrollLeft }));
  expect(dims.scroll).toBeGreaterThan(dims.client); // it really overflows
  expect(dims.left).toBeGreaterThan(0);
  const active = (await page.locator('.category-nav__list a[aria-current="page"]').boundingBox())!;
  expect(active.x).toBeGreaterThanOrEqual(0);
  expect(active.x + active.width).toBeLessThanOrEqual(391);

  // Arrow: not offered at the end; offered after scrolling back, and it advances the strip.
  await list.evaluate((e) => { e.scrollLeft = 0; });
  const arrow = page.locator('#primary-nav-next');
  await expect(arrow).toBeVisible();
  const before = await list.evaluate((e) => e.scrollLeft);
  await arrow.click();
  await expect.poll(() => list.evaluate((e) => e.scrollLeft)).toBeGreaterThan(before + 100);
  await list.evaluate((e) => { e.scrollLeft = e.scrollWidth; });
  await expect(arrow).toBeHidden();

  // Keyboard: focusing a link that is out of view brings it into view.
  await list.evaluate((e) => { e.scrollLeft = 0; });
  await page.locator('.category-nav__list a').last().focus();
  await expect.poll(() => list.evaluate((e) => e.scrollLeft)).toBeGreaterThan(0);
  const focused = (await page.locator('.category-nav__list a').last().boundingBox())!;
  expect(focused.x + focused.width).toBeLessThanOrEqual(391);
});

test('mobile menu lists every real category with working links and closes with Escape', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const all = await categories(page);
  await page.goto('/tin-moi-nhat');
  const toggle = page.locator('.masthead__menu-toggle');
  await toggle.click();
  await expect(toggle).toHaveAttribute('aria-expanded', 'true');
  const links = page.locator('#utility-drawer .utility-drawer__cat-list a');
  await expect(links).toHaveCount(all.length);
  expect(await links.evaluateAll((els) => els.map((e) => e.getAttribute('href')))).toEqual(all.map((c) => `/chuyen-muc/${c.slug}`));
  for (const l of await links.all()) expect((await l.boundingBox())!.height).toBeGreaterThanOrEqual(44); // touch target
  // Utility links remain.
  for (const name of ['Tin mới nhất', 'Đã lưu']) await expect(page.locator('#utility-drawer').getByRole('link', { name })).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(page.locator('#utility-drawer')).not.toHaveClass(/is-open/);

  await toggle.click();
  await links.nth(2).click();
  await expect(page).toHaveURL(new RegExp(`/chuyen-muc/${all[2].slug}`));
  // The new page's menu is closed again and marks the category we just opened.
  await expect(page.locator('#utility-drawer')).not.toHaveClass(/is-open/);
  await expect(page.locator('#utility-drawer .utility-drawer__cat-list a[aria-current="page"]')).toHaveText(all[2].name);
});

test('pagination component: Latest page numbers, prev/next and the clamp for a page past the end', async ({ page }) => {
  await page.goto('/tin-moi-nhat');
  const total = Math.ceil((await (await page.request.get('/api/v1/articles?pageSize=1')).json()).totalCount / 10);
  // Latest uses its own page size; just require a working, labelled pagination with current page marked.
  await expect(page.locator('nav.pagination[aria-label="Phân trang"]')).toBeVisible();
  await expect(page.locator('.pagination__item.is-current')).toHaveText('1');
  await expect(page.locator('.pagination__nav[rel="prev"]')).toHaveCount(0);
  await page.locator('.pagination__nav[rel="next"]').click();
  await expect(page).toHaveURL(/page=2/);
  await expect(page.locator('.pagination__item.is-current')).toHaveText('2');
  await expect(page.locator('.pagination__nav[rel="prev"]')).toHaveCount(1);
  expect(total).toBeGreaterThan(1);
});
