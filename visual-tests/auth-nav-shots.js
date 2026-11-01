// Screenshots for the header / navigation / mega menu / sign-in / demo work: VnExpress (reference) and VNews.
// Usage (from visual-tests): VNEWS_URL=http://localhost:5399 node auth-nav-shots.js
// VNews must run on an isolated database (VNewsE2E / VNewsDemo) with DemoAuth__Enabled=true; the script signs in
// with the demo buttons only. Output: artifacts/survey-2026-11f/.
const { chromium, devices } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const VNEWS = process.env.VNEWS_URL || 'http://localhost:5399';
// SHOTS_DIR picks the artifacts folder; SKIP_REF=1 skips the vnexpress.net reference captures.
const out = path.resolve(__dirname, '..', 'artifacts', process.env.SHOTS_DIR || 'survey-2026-11f');
fs.mkdirSync(out, { recursive: true });

const VIEWPORTS = [
  { name: '1536x864', options: { viewport: { width: 1536, height: 864 } }, mobile: false },
  { name: '1440x900', options: { viewport: { width: 1440, height: 900 } }, mobile: false },
  { name: '390x844', options: { ...devices['iPhone 13'], viewport: { width: 390, height: 844 }, deviceScaleFactor: 1 }, mobile: true },
];

async function newPage(browser, vp) {
  const ctx = await browser.newContext({ ...vp.options, locale: 'vi-VN' });
  return { ctx, page: await ctx.newPage() };
}

(async () => {
  const browser = await chromium.launch();

  // ---- reference: header and all-sections menu of vnexpress.net ----
  for (const vp of process.env.SKIP_REF ? [] : VIEWPORTS) {
    const { ctx, page } = await newPage(browser, vp);
    try {
      await page.goto('https://vnexpress.net/thoi-su', { waitUntil: 'load', timeout: 60000 });
      await page.waitForTimeout(4500);
      await page.screenshot({ path: path.join(out, `ref-header-${vp.name}.png`) });
      if (!vp.mobile) {
        // The all-sections button is the hamburger at the right end of the navigation row.
        const point = await page.evaluate(() => {
          const nav = document.querySelector('.main-nav');
          const b = nav.getBoundingClientRect();
          const candidates = [...nav.querySelectorAll('a, span, button, i')].filter((e) => { const r = e.getBoundingClientRect(); return r.width > 8 && r.width < 60 && r.left > window.innerWidth - 120 && r.top >= b.top && r.bottom <= b.bottom; });
          const target = candidates[candidates.length - 1];
          if (!target) return null;
          const r = target.getBoundingClientRect();
          return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
        });
        if (point) { await page.mouse.click(point.x, point.y); await page.waitForTimeout(1500); }
        await page.screenshot({ path: path.join(out, `ref-mega-${vp.name}.png`) });
      }
    } catch (e) { console.log('reference failed', vp.name, String(e.message).slice(0, 100)); }
    await ctx.close();
  }

  // ---- VNews ----
  for (const vp of VIEWPORTS) {
    const { ctx, page } = await newPage(browser, vp);
    for (const [name, url] of [['home', '/'], ['thoi-su', '/chuyen-muc/thoi-su'], ['tin-moi-nhat', '/tin-moi-nhat'], ['tim-kiem', '/tim-kiem?q=gia+vang']]) {
      await page.goto(VNEWS + url, { waitUntil: 'networkidle' });
      await page.screenshot({ path: path.join(out, `after-${name}-${vp.name}.png`) });
    }
    await page.goto(VNEWS + '/chuyen-muc/cong-nghe', { waitUntil: 'networkidle' });
    if (!vp.mobile) {
      await page.click('#mega-menu-toggle');
      await page.waitForTimeout(300);
      await page.screenshot({ path: path.join(out, `after-mega-open-${vp.name}.png`) });
      await page.keyboard.press('Escape');
    } else {
      await page.locator('.masthead__menu-toggle').click();
      await page.waitForTimeout(300);
      await page.screenshot({ path: path.join(out, `after-menu-open-${vp.name}.png`) });
      await page.locator('.masthead__menu-toggle').click();
    }
    await ctx.close();
  }

  // ---- sign-in page, dialog, demo badge and the newsroom per role (desktop + mobile) ----
  for (const vp of VIEWPORTS.filter((v) => v.name !== '1536x864')) {
    const { ctx, page } = await newPage(browser, vp);
    await page.goto(VNEWS + '/tai-khoan/dang-nhap', { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(out, `after-login-${vp.name}.png`), fullPage: true });
    await page.goto(VNEWS + '/tai-khoan/dang-ky', { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(out, `after-register-${vp.name}.png`), fullPage: true });
    await page.goto(VNEWS + '/chuyen-muc/cong-nghe', { waitUntil: 'networkidle' });
    await page.locator('.masthead__account-link').first().click();
    await page.waitForTimeout(400);
    await page.screenshot({ path: path.join(out, `after-popup-${vp.name}.png`) });
    await page.locator('#reader-login-dialog input[name="Email"]').fill('editor@vnews.local');
    await page.locator('#reader-login-dialog input[name="Password"]').fill('sai-mat-khau');
    await page.locator('#reader-login-dialog .reader-login-dialog__submit').click();
    await page.waitForTimeout(900);
    await page.screenshot({ path: path.join(out, `after-popup-error-${vp.name}.png`) });
    await ctx.close();

    for (const role of ['reader', 'author', 'editor', 'admin']) {
      const session = await newPage(browser, vp);
      await session.page.goto(VNEWS + '/tai-khoan/dang-nhap', { waitUntil: 'networkidle' });
      await session.page.locator(`.demo-login--page [data-demo-role="${role}"]`).click();
      await session.page.waitForLoadState('networkidle');
      await session.page.waitForTimeout(500);
      await session.page.screenshot({ path: path.join(out, `after-demo-${role}-${vp.name}.png`) });
      await session.ctx.close();
    }
  }

  await browser.close();
  console.log('done');
})();
