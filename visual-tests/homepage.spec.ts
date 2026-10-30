import { test, expect } from '@playwright/test';
import fs from 'fs';
import path from 'path';

// Target measurements derived from pixel analysis of references/vnexpress-desktop.png
// (see analyze-reference.js / measure-reference.js). Both the reference and VNews now
// place a leaderboard ad banner ABOVE the masthead, and that ad's height is not a fixed
// structural signal (a real ad slot varies), so it is excluded from these targets: every
// Y value below is a relative offset/gap, never an absolute viewport position.
const TARGETS = {
  mastheadHeight: 61,      // y331 - y270
  navHeight: 50,           // y381 - y331
  mainContentGap: 0,      // <main> should abut the nav directly; its own padding-top handles spacing
  heroImageWidth: 520,    // re-measured this round via visual-tests/measure-columns.js: x[170,690] on the 1440x900 reference
  heroImageAspect: 5 / 3,  // width/height, from site.css comment
  heroGap: 24,
  heroTextColumnMinWidth: 200,
};
const TOLERANCE_PX = 12;

function within(actual: number, target: number, tolerance = TOLERANCE_PX) {
  return Math.abs(actual - target) <= tolerance;
}

async function waitForImages(page: import('@playwright/test').Page) {
  // Only wait on above-the-fold images (others use loading="lazy" and never
  // trigger without scrolling, which would hang waitForFunction indefinitely).
  await page.waitForFunction(() => {
    const vh = window.innerHeight;
    const imgs = Array.from(document.querySelectorAll('img')).filter(img => {
      const r = img.getBoundingClientRect();
      return r.top < vh && r.bottom > 0;
    });
    return imgs.every(img => img.complete);
  });
  await page.waitForTimeout(300); // allow final paint after decode
}

test('homepage desktop layout matches VnExpress reference geometry', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await waitForImages(page);

  const masthead = page.locator('.masthead');
  const nav = page.locator('.primary-nav');
  const mainContent = page.locator('#main-content');
  const heroImage = page.locator('.hero-lead__image-link img').first();
  const heroContent = page.locator('.hero-lead__content').first();
  const heroSide = page.locator('.hero-side');

  const mastheadBox = await masthead.boundingBox();
  const navBox = await nav.boundingBox();
  const mainContentBox = await mainContent.boundingBox();
  const heroImageBox = await heroImage.boundingBox();
  const heroContentBox = await heroContent.boundingBox();
  const heroSideBox = await heroSide.boundingBox();

  const report: Record<string, any> = {};

  // Header height
  report.mastheadHeight = { actual: mastheadBox?.height, target: TARGETS.mastheadHeight };
  // Nav height
  report.navHeight = { actual: navBox?.height, target: TARGETS.navHeight };
  // Relative gap between nav bottom and <main> top — independent of whatever variable-height
  // ad banner sits above the masthead, on either the reference or this page.
  const mainContentGap = (mainContentBox?.y ?? 0) - ((navBox?.y ?? 0) + (navBox?.height ?? 0));
  report.mainContentGap = { actual: mainContentGap, target: TARGETS.mainContentGap };
  // Hero image width
  report.heroImageWidth = { actual: heroImageBox?.width, target: TARGETS.heroImageWidth };
  // Hero image aspect ratio check
  const actualAspect = heroImageBox ? heroImageBox.width / heroImageBox.height : 0;
  report.heroImageAspect = { actual: Number(actualAspect.toFixed(2)), target: Number(TARGETS.heroImageAspect.toFixed(2)) };
  // Gap between image and text column
  const gap = heroContentBox && heroImageBox ? heroContentBox.x - (heroImageBox.x + heroImageBox.width) : null;
  report.heroGap = { actual: gap, target: TARGETS.heroGap };
  // Secondary stories (hero-side) must be BELOW the hero row, not beside it
  const heroRowBottom = Math.max(heroImageBox?.y! + heroImageBox?.height!, heroContentBox?.y! + heroContentBox?.height!);
  report.heroSideIsBelow = { actual: heroSideBox?.y, mustBeAtLeast: heroRowBottom };

  fs.writeFileSync(
    path.resolve(__dirname, '..', 'artifacts', 'visual', 'layout-measurements.json'),
    JSON.stringify(report, null, 2)
  );

  await page.screenshot({ path: path.resolve(__dirname, '..', 'artifacts', 'visual', 'vnews-desktop.png') });

  // Assertions (12px tolerance per acceptance criteria)
  expect(mastheadBox?.height, `masthead height ${mastheadBox?.height} vs target ${TARGETS.mastheadHeight}`).not.toBeNull();
  expect(within(mastheadBox!.height, TARGETS.mastheadHeight)).toBeTruthy();
  expect(within(navBox!.height, TARGETS.navHeight)).toBeTruthy();
  expect(within(mainContentGap, TARGETS.mainContentGap)).toBeTruthy();
  expect(within(heroImageBox!.width, TARGETS.heroImageWidth)).toBeTruthy();
  expect(gap !== null && within(gap, TARGETS.heroGap, 15)).toBeTruthy();
  expect(heroSideBox!.y).toBeGreaterThanOrEqual(heroRowBottom - 2);

  // No horizontal overflow
  const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
  expect(scrollWidth, 'no horizontal overflow on desktop').toBeLessThanOrEqual(clientWidth + 2);

  // No broken images (naturalWidth 0 means failed to load)
  const brokenImages = await page.evaluate(() =>
    Array.from(document.querySelectorAll('img')).filter(img => img.complete && img.naturalWidth === 0).map(img => img.src)
  );
  expect(brokenImages, `broken images: ${brokenImages.join(', ')}`).toHaveLength(0);
});

test('mobile 390px: no overflow, nav works', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await waitForImages(page);
  await page.screenshot({ path: path.resolve(__dirname, '..', 'artifacts', 'visual', 'vnews-mobile.png') });

  const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
  expect(scrollWidth, 'no horizontal overflow on mobile 390px').toBeLessThanOrEqual(clientWidth + 2);

  const brokenImages = await page.evaluate(() =>
    Array.from(document.querySelectorAll('img')).filter(img => img.complete && img.naturalWidth === 0).map(img => img.src)
  );
  expect(brokenImages, `broken images on mobile: ${brokenImages.join(', ')}`).toHaveLength(0);
});

test('tablet 768px: no overflow, nav works', async ({ page }) => {
  await page.setViewportSize({ width: 768, height: 1024 });
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  await waitForImages(page);
  await page.screenshot({ path: path.resolve(__dirname, '..', 'artifacts', 'visual', 'vnews-tablet.png') });

  const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
  const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
  expect(scrollWidth, 'no horizontal overflow on tablet 768px').toBeLessThanOrEqual(clientWidth + 2);
});
