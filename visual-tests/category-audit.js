// Audit of category pages + navigation: VNews (isolated E2E server, :5399) vs https://vnexpress.net/ (Vietnamese edition).
// Saves screenshots and JSON to artifacts/survey-2026-11d. Read-only: it never writes to any database.
// Usage (from visual-tests): node category-audit.js
const { chromium, devices } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const out = path.resolve(__dirname, '..', 'artifacts', 'survey-2026-11d');
fs.mkdirSync(out, { recursive: true });
const VNEWS = process.env.VNEWS_URL || 'http://localhost:5399';

// [VNews slug, VnExpress slug]
const CATEGORIES = [
  ['thoi-su', 'thoi-su'], ['kinh-doanh', 'kinh-doanh'], ['cong-nghe', 'khoa-hoc-cong-nghe'], ['the-gioi', 'the-gioi'], ['the-thao', 'the-thao'],
  ['giai-tri', 'giai-tri'], ['suc-khoe', 'suc-khoe'], ['giao-duc', 'giao-duc'], ['du-lich', 'du-lich'], ['phap-luat', 'phap-luat'],
];
const VIEWPORTS = [{ name: 'desktop', width: 1440, height: 900 }, { name: 'mobile', width: 390, height: 844, mobile: true }];

const geom = (e) => { if (!e) return null; const b = e.getBoundingClientRect(); return { x: Math.round(b.x), y: Math.round(b.y + scrollY), w: Math.round(b.width), h: Math.round(b.height) }; };

function collectVnews() {
  const q = (s) => document.querySelector(s);
  const all = (s) => [...document.querySelectorAll(s)];
  const g = (e) => { if (!e) return null; const b = e.getBoundingClientRect(); return { x: Math.round(b.x), y: Math.round(b.y + scrollY), w: Math.round(b.width), h: Math.round(b.height) }; };
  const nav = q('#primary-nav');
  const navLinks = all('#primary-nav .category-nav a');
  const active = navLinks.filter((a) => a.getAttribute('aria-current') || /active|current|is-active/.test(a.className + a.parentElement.className));
  const rows = all('.story-row');
  const img = rows[0] && rows[0].querySelector('img');
  const links = all('main a[href], .site-main a[href]');
  return {
    title: q('h1') && q('h1').textContent.trim(),
    navBottom: nav ? Math.round(nav.getBoundingClientRect().bottom + scrollY) : null,
    navItems: navLinks.map((a) => a.textContent.trim()),
    navActiveCount: active.length,
    promo: !!q('.house-promo'),
    subMenu: !!q('.category-nav__sub, .sub-nav, .menu-sub'),
    lead: g(q('.editorial-listing__lead')),
    briefings: g(q('.editorial-listing__briefings')),
    feedRows: rows.length,
    rowThumb: img ? g(img) : null,
    sidebar: g(q('aside, .sidebar')),
    pagination: !!q('.pagination'),
    articleLinks: all('.hero-lead a, .story-row a, .listing-brief a').length,
    anchorOnly: all('a[href^="#"]').map((a) => a.getAttribute('href')),
    buttons: all('main button, .site-main button').map((b) => (b.textContent.trim() || b.getAttribute('aria-label') || '').slice(0, 30)),
    pageHeight: Math.round(document.documentElement.scrollHeight),
    linkCount: links.length,
  };
}

function collectRef() {
  const q = (s) => document.querySelector(s);
  const all = (s) => [...document.querySelectorAll(s)];
  const g = (e) => { if (!e) return null; const b = e.getBoundingClientRect(); return { x: Math.round(b.x), y: Math.round(b.y + scrollY), w: Math.round(b.width), h: Math.round(b.height) }; };
  const nav = q('.main-nav');
  const active = all('.main-nav li.active a, .main-nav li.current a').map((a) => a.textContent.trim());
  const sub = all('.main-nav ~ * ul.menu-sub li a, .container-menu-sub a, .menu-sub a, ul.breadcrumb li a').map((a) => a.textContent.trim()).filter(Boolean);
  const items = all('article.item-news, .item-news');
  const thumb = q('.item-news .thumb-art img, .item-news picture img, .item-news .thumb-art');
  const pag = q('#pagination, .pagination, .button-page, .btn-page');
  return {
    title: (q('h1') || q('.title-page') || q('.title-box-category') || {}).textContent?.trim().slice(0, 60),
    navBottom: nav ? Math.round(nav.getBoundingClientRect().bottom + scrollY) : null,
    navItemCount: all('.main-nav ul.parent > li').length,
    navActive: active,
    subMenu: sub.slice(0, 14),
    sidebar1: g(q('.sidebar-1')),
    sidebar2: g(q('.sidebar-2')),
    itemCount: items.length,
    thumb: thumb ? g(thumb) : null,
    pagination: pag ? { sel: pag.id || pag.className.toString().slice(0, 30), text: pag.textContent.trim().replace(/\s+/g, ' ').slice(0, 40) } : null,
    topStoryBlocks: all('.item-topstory, article.item-news-common').length,
    pageHeight: Math.round(document.documentElement.scrollHeight),
  };
}

async function open(browser, vp, url, wait) {
  const opts = vp.mobile
    ? { ...devices['iPhone 13'], viewport: { width: vp.width, height: vp.height }, deviceScaleFactor: 1, locale: 'vi-VN' }
    : { viewport: { width: vp.width, height: vp.height }, locale: 'vi-VN' };
  const ctx = await browser.newContext(opts);
  const page = await ctx.newPage();
  await page.goto(url, { waitUntil: wait ? 'load' : 'networkidle', timeout: 60000 });
  if (wait) {
    await page.waitForTimeout(4500);
    for (let i = 0; i < 4; i++) { await page.mouse.wheel(0, 700); await page.waitForTimeout(350); }
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.waitForTimeout(500);
  }
  return { ctx, page };
}

(async () => {
  const browser = await chromium.launch();
  const result = { vnews: {}, ref: {} };
  for (const vp of VIEWPORTS) {
    for (const [slug, refSlug] of CATEGORIES) {
      const key = `${slug}-${vp.name}`;
      let v = await open(browser, vp, `${VNEWS}/chuyen-muc/${slug}`, false);
      result.vnews[key] = await v.page.evaluate(collectVnews);
      await v.page.screenshot({ path: path.join(out, `vnews-${key}.png`) });
      await v.page.screenshot({ path: path.join(out, `vnews-${key}-full.png`), fullPage: true });
      await v.ctx.close();
      try {
        const r = await open(browser, vp, `https://vnexpress.net/${refSlug}`, true);
        result.ref[key] = await r.page.evaluate(collectRef);
        await r.page.screenshot({ path: path.join(out, `ref-${key}.png`) });
        await r.page.screenshot({ path: path.join(out, `ref-${key}-full.png`), fullPage: true });
        await r.ctx.close();
      } catch (e) { result.ref[key] = { error: String(e.message).slice(0, 120) }; }
    }
  }
  fs.writeFileSync(path.join(out, 'category-audit.json'), JSON.stringify(result, null, 1));
  console.log('done');
  await browser.close();
})();
