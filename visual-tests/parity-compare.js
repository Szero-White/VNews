// Builds side-by-side images (reference | VNews before | VNews after) from the screenshots parity-measure.js saved.
// Usage (from visual-tests): node parity-compare.js
const { chromium } = require('@playwright/test');
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');

const dir = path.resolve(__dirname, '..', 'artifacts', 'survey-2026-11c');

(async () => {
  const browser = await chromium.launch();
  for (const vp of ['1536x864', '1440x900', '390x844']) {
    const mobile = vp === '390x844';
    const imgs = [
      ['VnExpress (tham chiếu)', `before-ref-${vp}.png`],
      ['VNews trước', `before-vnews-${vp}.png`],
      ['VNews sau', `after-vnews-${vp}.png`],
    ];
    const figures = imgs
      .map(([title, file]) => `<figure style="margin:0"><figcaption style="color:#fff;margin-bottom:4px">${title}</figcaption><img src="${file}" style="display:block;${mobile ? '' : 'width:560px'}"></figure>`)
      .join('');
    const page = await browser.newPage({ viewport: { width: mobile ? 1280 : 1740, height: 700 } });
    const htmlFile = path.join(dir, `compare-${vp}.html`);
    fs.writeFileSync(htmlFile, `<body style="margin:0;background:#888;font:14px Arial"><div style="display:flex;gap:10px;padding:10px;align-items:flex-start">${figures}</div></body>`);
    await page.goto(pathToFileURL(htmlFile).href);
    await page.waitForTimeout(500);
    fs.unlinkSync(htmlFile);
    await page.screenshot({ path: path.join(dir, `compare-${vp}.png`), fullPage: true });
    await page.close();
  }
  await browser.close();
})();
