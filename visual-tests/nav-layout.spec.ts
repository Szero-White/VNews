import { test, expect, Page } from '@playwright/test';

// The category navigation is ONE cluster: home icon, the real categories and the all-sections button, packed with
// the same gap and centred in the bar (measured against vnexpress.net: 24px home circle, 15px between items,
// 50px bar under a 62px header). No justify-content: space-between, no margin-left:auto, no stretched strip.

async function cluster(page: Page) {
  return page.evaluate(() => {
    const r = (e: Element) => { const b = e.getBoundingClientRect(); return { left: b.left, right: b.right, top: b.top, bottom: b.bottom, w: b.width, h: b.height }; };
    const home = r(document.querySelector('.primary-nav__home')!);
    const links = [...document.querySelectorAll('.category-nav__list a')].map(r);
    const button = document.querySelector('#mega-menu-toggle');
    const buttonBox = button ? r(button) : null;
    const nav = document.querySelector('#primary-nav')!;
    const inner = getComputedStyle(document.querySelector('.primary-nav__inner')!);
    const strip = getComputedStyle(document.querySelector('.category-nav__list')!);
    const wrapper = getComputedStyle(document.querySelector('.primary-nav__inner .category-nav')!);
    return {
      vw: document.documentElement.clientWidth, navHeight: nav.getBoundingClientRect().height,
      home, links, button: buttonBox,
      justify: inner.justifyContent, wrapperFlex: wrapper.flex, stripWidth: strip.width, stripGap: strip.columnGap,
      buttonMarginLeft: button ? getComputedStyle(button).marginLeft : null,
    };
  });
}

for (const [width, height] of [[1536, 864], [1440, 900], [1280, 800]]) {
  test(`desktop ${width}: home, 10 categories and the menu button are one gap-consistent cluster centred on the page`, async ({ page }) => {
    await page.setViewportSize({ width, height });
    await page.goto('/chuyen-muc/cong-nghe');
    const c = await cluster(page);

    expect(c.links).toHaveLength(10);
    expect(c.button).not.toBeNull();
    expect(c.navHeight).toBe(50);

    // Centred: free space on the left of the home icon equals free space on the right of the menu button.
    const left = c.home.left;
    const right = c.vw - c.button!.right;
    expect(Math.abs(left - right), `left ${left} vs right ${right}`).toBeLessThanOrEqual(2);
    expect(left).toBeGreaterThan(100); // genuinely centred, not pinned to the edge

    // Packed with one gap: home -> first category, between categories, last category -> button.
    const gaps = [c.links[0].left - c.home.right, ...c.links.slice(1).map((l, i) => l.left - c.links[i].right), c.button!.left - c.links[9].right];
    for (const gap of gaps) expect(gap).toBeGreaterThanOrEqual(14);
    for (const gap of gaps) expect(gap).toBeLessThanOrEqual(16);
    expect(Math.max(...gaps) - Math.min(...gaps)).toBeLessThanOrEqual(1.5);

    // Compact controls, like the reference.
    expect(Math.round(c.home.w)).toBe(24);
    expect(c.button!.w).toBeLessThanOrEqual(36);
    expect(c.button!.h).toBeLessThanOrEqual(36);

    // None of the layout tricks that split the bar into two ends.
    expect(c.justify).toBe('center');
    expect(c.wrapperFlex).toBe('0 1 auto');
    expect(c.buttonMarginLeft).toBe('0px');
    expect(c.stripGap).toBe('15px');

    // The whole cluster is vertically centred in the bar.
    const mid = (b: { top: number; bottom: number }) => (b.top + b.bottom) / 2;
    expect(Math.abs(mid(c.button!) - mid(c.home))).toBeLessThanOrEqual(1.5);
  });
}

test('the same cluster on the homepage (banner kept above the header) and on an article', async ({ page }) => {
  await page.setViewportSize({ width: 1536, height: 864 });
  for (const url of ['/', '/tin-moi-nhat']) {
    await page.goto(url);
    const c = await cluster(page);
    expect(Math.abs(c.home.left - (c.vw - c.button!.right)), url).toBeLessThanOrEqual(2);
  }
  const slug = (await (await page.request.get('/api/v1/articles?pageSize=1')).json()).items[0].slug;
  await page.goto(`/tin/${slug}`);
  const c = await cluster(page);
  expect(Math.abs(c.home.left - (c.vw - c.button!.right))).toBeLessThanOrEqual(2);
});

test('the banner on the homepage is untouched and its call to action still opens a real page', async ({ page }) => {
  await page.setViewportSize({ width: 1536, height: 864 });
  await page.goto('/');
  const banner = page.locator('.top-ad-space');
  await expect(banner).toBeVisible();
  await expect(banner).toContainText('Mỗi ngày, một góc nhìn mới');
  const cta = banner.locator('a.top-ad-space__creative');
  await expect(cta).toContainText('Khám phá ngay');
  const href = (await cta.getAttribute('href'))!;
  expect((await page.request.get(href)).status()).toBe(200);
  // header and navigation sit directly under the banner
  const bannerBottom = (await banner.boundingBox())!.y + (await banner.boundingBox())!.height;
  const masthead = (await page.locator('.masthead').boundingBox())!;
  expect(Math.abs(masthead.y - bannerBottom)).toBeLessThanOrEqual(1);
  expect(Math.round(masthead.height)).toBe(62);
});

test('narrow desktop: the cluster stays packed; categories scroll inside the strip and the arrow sits against the menu button', async ({ page }) => {
  await page.setViewportSize({ width: 780, height: 800 }); // still the desktop layout, too narrow for ten items
  await page.goto('/chuyen-muc/the-thao');
  const c = await cluster(page);
  const list = await page.locator('.category-nav__list').evaluate((e) => ({ scroll: e.scrollWidth, client: e.clientWidth }));
  expect(list.scroll).toBeGreaterThan(list.client); // too narrow for ten items: the strip scrolls
  expect(c.button!.right).toBeLessThanOrEqual(c.vw);
  const arrow = (await page.locator('#primary-nav-next').boundingBox());
  if (arrow) expect(arrow.x + arrow.width).toBeLessThanOrEqual(c.button!.left + 1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
  // active category is brought into view inside the strip
  const active = (await page.locator('.category-nav__list a[aria-current="page"]').boundingBox())!;
  expect(active.x).toBeGreaterThanOrEqual(0);
  expect(active.x + active.width).toBeLessThanOrEqual(c.button!.left + 1);
});

test('mobile 390: no menu button, home + strip fill the bar and scroll, nothing overflows', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/chuyen-muc/cong-nghe');
  await expect(page.locator('#mega-menu-toggle')).toBeHidden();
  const c = await cluster(page);
  expect(c.home.left).toBeGreaterThanOrEqual(15);
  expect(c.home.left).toBeLessThanOrEqual(17);
  const list = await page.locator('.category-nav__list').evaluate((e) => ({ scroll: e.scrollWidth, client: e.clientWidth }));
  expect(list.scroll).toBeGreaterThan(list.client);
  expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
  expect(c.navHeight).toBe(50);
});

test('navigation still works: every category link, active state and the mega menu', async ({ page }) => {
  await page.setViewportSize({ width: 1536, height: 864 });
  const all = await (await page.request.get('/api/v1/categories')).json();
  await page.goto('/');
  const hrefs = await page.locator('.category-nav__list a').evaluateAll((els) => els.map((e) => e.getAttribute('href')));
  expect(hrefs).toEqual(all.map((c: any) => `/chuyen-muc/${c.slug}`));
  await page.locator('.category-nav__list a').nth(3).click();
  await expect(page.locator('.category-nav__list a[aria-current="page"]')).toHaveText(all[3].name);
  await page.locator('#mega-menu-toggle').click();
  await expect(page.locator('#mega-menu')).toBeVisible();
  await expect(page.locator('#mega-menu .mega-menu__list a')).toHaveCount(all.length);
  await page.keyboard.press('Escape');
  await expect(page.locator('#mega-menu')).toBeHidden();
});
