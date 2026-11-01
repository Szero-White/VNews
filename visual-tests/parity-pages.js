// Before/after screenshots of the main public pages at 1440x900 and 390x844, plus side-by-side comparison images.
// Usage (from visual-tests): BEFORE_URL=http://localhost:5398 AFTER_URL=http://localhost:5399 node parity-pages.js <out-folder-name>
// Both servers must use an isolated database (VNewsE2E), never VNewsDb. Images land in artifacts/<out-folder-name>.
const { chromium, devices } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');

const BEFORE = process.env.BEFORE_URL || 'http://localhost:5398';
const AFTER = process.env.AFTER_URL || 'http://localhost:5399';
const out = path.resolve(__dirname, '..', 'artifacts', process.argv[2] || 'survey-2026-11e');
fs.mkdirSync(out, { recursive: true });

const VIEWPORTS = [
  { name: '1440x900', options: { viewport: { width: 1440, height: 900 } } },
  { name: '390x844', options: { ...devices['iPhone 13'], viewport: { width: 390, height: 844 }, deviceScaleFactor: 1 } },
];

async function scrollThrough(page) {
  const height = await page.evaluate(() => document.documentElement.scrollHeight);
  for (let y = 0; y < height; y += 500) { await page.evaluate((v) => window.scrollTo(0, v), y); await page.waitForTimeout(90); }
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.waitForTimeout(300);
}

(async () => {
  const browser = await chromium.launch();
  const list = await (await fetch(AFTER + '/api/v1/articles?pageSize=50')).json();
  const article = (list.items.find((i) => i.categorySlug === 'cong-nghe' && i.coverImageUrl) ?? list.items[0]).slug;
  const pages = [
    ['home', '/'], ['thoi-su', '/chuyen-muc/thoi-su'], ['kinh-doanh', '/chuyen-muc/kinh-doanh'], ['cong-nghe', '/chuyen-muc/cong-nghe'],
    ['the-thao', '/chuyen-muc/the-thao'], ['bai-viet', `/tin/${article}`], ['tin-moi-nhat', '/tin-moi-nhat'], ['tim-kiem', '/tim-kiem?q=gia+vang'],
  ];
  const info = {};
  for (const vp of VIEWPORTS) {
    for (const [name, url] of pages) {
      for (const [phase, base] of [['before', BEFORE], ['after', AFTER]]) {
        const ctx = await browser.newContext(vp.options);
        const page = await ctx.newPage();
        await page.goto(base + url, { waitUntil: 'networkidle' });
        await scrollThrough(page);
        await page.screenshot({ path: path.join(out, `${phase}-${name}-${vp.name}.png`) });
        await page.screenshot({ path: path.join(out, `${phase}-${name}-${vp.name}-full.png`), fullPage: true });
        if (phase === 'after') {
          info[`${name}-${vp.name}`] = await page.evaluate(() => ({
            overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
            mastheadTop: Math.round(document.querySelector('.masthead').getBoundingClientRect().top + scrollY),
            navBottom: Math.round(document.querySelector('#primary-nav').getBoundingClientRect().bottom + scrollY),
            height: document.documentElement.scrollHeight,
          }));
        } else {
          info[`${name}-${vp.name}-before`] = await page.evaluate(() => ({
            overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
            mastheadTop: Math.round(document.querySelector('.masthead').getBoundingClientRect().top + scrollY),
            navBottom: Math.round(document.querySelector('#primary-nav').getBoundingClientRect().bottom + scrollY),
            height: document.documentElement.scrollHeight,
          }));
        }
        await ctx.close();
      }
      // Side by side (before | after), top 1000px of each.
      const html = `<body style="margin:0;background:#888;font:14px Arial"><div style="display:flex;gap:10px;padding:10px;align-items:flex-start">${['before', 'after']
        .map((phase) => `<figure style="margin:0"><figcaption style="color:#fff;margin-bottom:4px">${phase === 'before' ? 'Trước' : 'Sau'}: ${name} @${vp.name}</figcaption><img src="${phase}-${name}-${vp.name}-full.png" style="display:block;width:${vp.name === '390x844' ? 390 : 700}px"></figure>`)
        .join('')}</div></body>`;
      const file = path.join(out, `compare-${name}-${vp.name}.html`);
      fs.writeFileSync(file, html);
      const cmp = await browser.newPage({ viewport: { width: vp.name === '390x844' ? 820 : 1440, height: 700 } });
      await cmp.goto(pathToFileURL(file).href);
      await cmp.waitForTimeout(400);
      await cmp.screenshot({ path: path.join(out, `compare-${name}-${vp.name}.png`), fullPage: true });
      await cmp.close();
      fs.unlinkSync(file);
    }
  }
  fs.writeFileSync(path.join(out, 'pages-measurements.json'), JSON.stringify(info, null, 1));
  console.log(JSON.stringify(info));
  await browser.close();
})();
