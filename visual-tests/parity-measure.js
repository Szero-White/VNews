// Measures the article page of vnexpress.net (reference) and VNews at the same viewports, saves screenshots + JSON.
// Usage (from visual-tests): node parity-measure.js <phase> [vnews|ref|both]   e.g. node parity-measure.js before
// VNews is read from VNEWS_URL (default: the isolated E2E server on :5399, never the dev database server).
const { chromium, devices } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const phase = process.argv[2] || 'before';
const which = process.argv[3] || 'both';
const out = path.resolve(__dirname, '..', 'artifacts', 'survey-2026-11c');
fs.mkdirSync(out, { recursive: true });

const VNEWS = process.env.VNEWS_URL || 'http://localhost:5399';
const REF_ARTICLE = 'https://vnexpress.net/doa-dam-chet-tai-xe-xe-cong-nghe-khong-cho-va-lop-giup-do-nguoi-di-duong-5139304.html';
const VIEWPORTS = [
  { name: '1536x864', width: 1536, height: 864 },
  { name: '1440x900', width: 1440, height: 900 },
  { name: '390x844', width: 390, height: 844, mobile: true },
];

// Runs in the page: returns geometry (absolute document coordinates) and computed type styles.
function collect(sel) {
  const box = (e) => { if (!e) return null; const b = e.getBoundingClientRect(); return { x: Math.round(b.x), y: Math.round(b.y + scrollY), w: Math.round(b.width), h: Math.round(b.height) }; };
  const q = (s) => (s ? document.querySelector(s) : null);
  const type = (e) => { if (!e) return null; const c = getComputedStyle(e); return { family: c.fontFamily.split(',')[0].replace(/"/g, ''), size: c.fontSize, lh: c.lineHeight, weight: c.fontWeight }; };
  const xem = sel.xemTitle ? [...document.querySelectorAll(sel.xemTitle)].find((e) => /^Xem nhiều$/.test(e.textContent.trim())) : null;
  const thumbs = [...document.querySelectorAll(sel.xemThumbs)].map(box).filter((b) => b && b.w > 0).slice(0, 5);
  const pitch = thumbs.length > 1 ? Math.round((thumbs[thumbs.length - 1].y - thumbs[0].y) / (thumbs.length - 1)) : null;
  const navItems = [...document.querySelectorAll(sel.navItems)];
  const nav = q(sel.nav);
  return {
    navBottom: nav ? Math.round(nav.getBoundingClientRect().bottom + scrollY) : null,
    navHeight: nav ? Math.round(nav.getBoundingClientRect().height) : null,
    navItemCount: navItems.length,
    navLastRight: navItems.length ? Math.round(Math.max(...navItems.map((a) => a.getBoundingClientRect().right))) : null,
    breadcrumb: box(q(sel.breadcrumb)),
    date: box(q(sel.date)),
    title: box(q(sel.title)),
    titleType: type(q(sel.title)),
    summaryType: type(q(sel.summary)),
    bodyType: type(q(sel.body)),
    articleCol: box(q(sel.articleCol)),
    sidebar: box(q(sel.sidebar)),
    xemTitle: box(xem),
    xemTitleType: type(xem),
    thumbs: thumbs.length ? { count: thumbs.length, w: thumbs[0].w, h: thumbs[0].h, pitch, firstY: thumbs[0].y } : null,
    xemItemTitleType: type(q(sel.xemItemTitle)),
    scrollOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
  };
}

const REF_SEL = {
  nav: '.main-nav', navItems: '.main-nav ul.parent > li > a', breadcrumb: '.breadcrumb', date: '.date', title: 'h1.title-detail', summary: '.description',
  body: 'p.Normal', articleCol: 'article.fck_detail', sidebar: '.sidebar-2', xemTitle: '.title-box-category a',
  xemThumbs: '.box-xem .thumb-art', xemItemTitle: '.box-xem .title-news a',
};
const VN_SEL = {
  nav: '#primary-nav', navItems: '#primary-nav .category-nav a', breadcrumb: '.article-topbar .breadcrumb', date: '.article-topbar__date', title: 'h1.article-detail__title',
  summary: '.article-detail__summary', body: '.article-detail__body p', articleCol: '.article-layout__main', sidebar: '.article-layout__aside',
  xemTitle: '.aside-title__text', xemThumbs: '.article-popular__thumb', xemItemTitle: '.article-popular__item',
};

async function run(browser, siteName, url, sel, vp) {
  const opts = vp.mobile
    ? { ...devices['iPhone 13'], viewport: { width: vp.width, height: vp.height }, deviceScaleFactor: 1, locale: 'vi-VN' }
    : { viewport: { width: vp.width, height: vp.height }, locale: 'vi-VN' };
  const ctx = await browser.newContext(opts);
  const page = await ctx.newPage();
  let data = null;
  for (let attempt = 0; attempt < 4; attempt++) {
    await page.goto(url, { waitUntil: siteName === 'ref' ? 'load' : 'networkidle', timeout: 60000 });
    await page.waitForTimeout(siteName === 'ref' ? 5000 : 500);
    // The reference lazy-loads its sidebar: scroll through the page, then return to the top.
    for (let i = 0; i < 6; i++) { await page.mouse.wheel(0, 500); await page.waitForTimeout(450); }
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.waitForTimeout(600);
    data = await page.evaluate(collect, sel);
    if (siteName !== 'ref' || vp.mobile || data.xemTitle) break;
  }
  const base = `${phase}-${siteName}-${vp.name}`;
  await page.screenshot({ path: path.join(out, `${base}.png`) });
  await page.screenshot({ path: path.join(out, `${base}-full.png`), fullPage: true });
  await ctx.close();
  return data;
}

(async () => {
  const browser = await chromium.launch();
  const result = {};
  // Same demo article every run: the oldest-listed seeded technology article that has a cover image.
  const list = await (await fetch(VNEWS + '/api/v1/articles?pageSize=50')).json();
  const pick = list.items.filter((i) => i.categorySlug === 'cong-nghe' && i.coverImageUrl).sort((a, b) => a.slug.localeCompare(b.slug))[0] ?? list.items[0];
  const VNEWS_ARTICLE = '/tin/' + pick.slug;
  console.error('VNews article:', VNEWS_ARTICLE);
  for (const vp of VIEWPORTS) {
    result[vp.name] = {};
    if (which !== 'vnews') result[vp.name].ref = await run(browser, 'ref', REF_ARTICLE, REF_SEL, vp);
    if (which !== 'ref') result[vp.name].vnews = await run(browser, 'vnews', VNEWS + VNEWS_ARTICLE, VN_SEL, vp);
  }
  fs.writeFileSync(path.join(out, `${phase}-measurements.json`), JSON.stringify(result, null, 1));
  console.log(JSON.stringify(result));
  await browser.close();
})();
