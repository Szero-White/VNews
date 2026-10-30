// Captures the 2 homepage review screenshots (desktop 1440x900, mobile 390x844) for
// artifacts/REVIEW-TO-SEND/. Always opens a fresh page, forces scroll to (0,0) and
// asserts scrollY === 0 before each shot, so the mobile capture can never land mid-page.
// Run manually: node visual-tests/capture-homepage-review.js [baseUrl]
const { chromium } = require('@playwright/test');
const path = require('path');

const baseUrl = process.argv[2] || 'http://localhost:5299';
const outDir = path.resolve(__dirname, '..', 'artifacts', 'REVIEW-TO-SEND');

const TARGETS = [
  { name: '01-trangchu-desktop-1440x900.png', width: 1440, height: 900 },
  { name: '02-trangchu-mobile-390x844.png', width: 390, height: 844 },
];

(async () => {
  const browser = await chromium.launch();
  for (const t of TARGETS) {
    const page = await browser.newPage({ viewport: { width: t.width, height: t.height } });
    await page.goto(baseUrl + '/', { waitUntil: 'networkidle' });
    await page.waitForFunction(() => {
      const vh = window.innerHeight;
      const imgs = Array.from(document.querySelectorAll('img')).filter(img => {
        const r = img.getBoundingClientRect();
        return r.top < vh && r.bottom > 0;
      });
      return imgs.every(img => img.complete);
    });
    await page.evaluate(() => window.scrollTo(0, 0));
    const scrollY = await page.evaluate(() => window.scrollY);
    if (scrollY !== 0) {
      throw new Error(`${t.name}: scrollY is ${scrollY}, expected 0`);
    }
    await page.waitForTimeout(300);
    const filePath = path.join(outDir, t.name);
    await page.screenshot({ path: filePath });
    console.log(`Saved ${filePath} (scrollY=${scrollY})`);
    await page.close();
  }
  await browser.close();
})();
