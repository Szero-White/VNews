// One-off screenshot of the homepage Business ("Kinh doanh") module region, desktop +
// mobile, for review. Run manually: node visual-tests/capture-business-feature.js
const { chromium } = require('@playwright/test');
const path = require('path');

(async () => {
  const browser = await chromium.launch();
  const outDir = path.resolve(__dirname, '..', 'artifacts', 'REVIEW-TO-SEND');

  const desktop = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await desktop.goto('http://localhost:5299/', { waitUntil: 'networkidle' });
  const businessDesktop = desktop.locator('[data-home-business]');
  await businessDesktop.scrollIntoViewIfNeeded();
  await desktop.waitForTimeout(250);
  await businessDesktop.screenshot({ path: path.join(outDir, '08-trangchu-kinhdoanh-desktop-1440.png') });
  await desktop.close();

  const mobile = await browser.newPage({ viewport: { width: 390, height: 844 } });
  await mobile.goto('http://localhost:5299/', { waitUntil: 'networkidle' });
  const businessMobile = mobile.locator('[data-home-business]');
  await businessMobile.scrollIntoViewIfNeeded();
  await mobile.waitForTimeout(250);
  await businessMobile.screenshot({ path: path.join(outDir, '09-trangchu-kinhdoanh-mobile-390.png') });
  await mobile.close();

  await browser.close();
  console.log('done');
})();
