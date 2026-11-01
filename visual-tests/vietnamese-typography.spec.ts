import { test, expect, Page } from '@playwright/test';

// Georgia (Windows build) has no precomposed glyphs for ấ ầ ắ ằ ế ề ố ồ (and capitals): the browser
// splits them into a base letter plus a detached mark ("Thê´"). Any visible text containing those
// letters must therefore not be set with Georgia as its first font. See artifacts/survey-2026-11b/fix.
const RISKY = /[ấầắằếềốồẤẦẮẰẾỀỐỒ]/;

async function offenders(page: Page) {
  return page.evaluate((src) => {
    const risky = new RegExp(src);
    const bad: string[] = [];
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      const text = (n.textContent || '').trim();
      const el = n.parentElement;
      if (!text || !el || !risky.test(text)) continue;
      const cs = getComputedStyle(el);
      if (cs.display === 'none' || cs.visibility === 'hidden' || el.closest('script,style,noscript')) continue;
      const first = cs.fontFamily.split(',')[0].replace(/["']/g, '').trim().toLowerCase();
      if (first === 'georgia') bad.push(`${el.tagName}.${el.className}: "${text.slice(0, 40)}"`);
    }
    return bad;
  }, RISKY.source);
}

for (const [name, path] of [
  ['home', '/'], ['category', '/chuyen-muc/kinh-doanh'], ['article', '/tin/toa-soan-so-hoa-quy-trinh-xuat-ban-de-rut-ngan-thoi-gian-dua-tin'],
  ['latest', '/tin-moi-nhat'], ['login', '/tai-khoan/dang-nhap'], ['register', '/tai-khoan/dang-ky'],
] as const) {
  for (const vp of [{ width: 1440, height: 900 }, { width: 390, height: 844 }]) {
    test(`${name} @${vp.width}: no Vietnamese text with ấầắằếềốồ is set in Georgia`, async ({ page }) => {
      await page.setViewportSize(vp);
      await page.goto(path);
      await page.waitForLoadState('networkidle');
      expect(await offenders(page)).toEqual([]);
    });
  }
}

test('reader news panel (tin mới + tin đã xem) does not use Georgia for headlines', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await page.locator('.reader-news-panel summary:visible').first().click();
  await expect(page.locator('.reader-news-panel__list li a').first()).toBeVisible();
  expect(await offenders(page)).toEqual([]);
  const family = await page.locator('.reader-news-panel__list li a').first().evaluate(e => getComputedStyle(e).fontFamily);
  expect(family.toLowerCase()).toContain('merriweather');
});

test('category page headings with ế (Thế giới) render in Merriweather, not Georgia', async ({ page }) => {
  await page.goto('/chuyen-muc/the-gioi');
  await expect(page.locator('h1.category-header__title')).toHaveText('Thế giới');
  const family = await page.locator('h1.category-header__title').evaluate(e => getComputedStyle(e).fontFamily);
  expect(family.split(',')[0].replace(/["']/g, '').trim()).toBe('Merriweather');
});
