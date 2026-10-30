// One-off full-page desktop screenshot of the homepage content architecture (stream +
// all stacked category boxes), for structural review. Run manually:
// node visual-tests/capture-homepage-fullpage.js
const { chromium } = require('@playwright/test');
const path = require('path');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await page.goto('http://localhost:5299/', { waitUntil: 'networkidle' });
  await page.waitForTimeout(300);
  const outPath = path.resolve(__dirname, '..', 'artifacts', 'REVIEW-TO-SEND', '07-trangchu-fullpage-cautruc-1440.png');
  await page.screenshot({ path: outPath, fullPage: true });
  console.log('Saved', outPath);
  await browser.close();
})();
