import { test, expect } from '@playwright/test';
import fs from 'fs';
import path from 'path';

// Criteria from docs/VISUAL-CONTRACT-HOME.md, derived from real DOM measurement
// of https://vnexpress.net/ (section_stream_home, #kinhdoanh) at 1440x900.
// Scope: ONLY the "Dòng tin mới nhất" stream column + the first category block.

async function waitForImages(page: import('@playwright/test').Page) {
  await page.waitForFunction(() => {
    const vh = window.innerHeight;
    const imgs = Array.from(document.querySelectorAll('img')).filter(img => {
      const r = img.getBoundingClientRect();
      return r.top < vh * 3 && r.bottom > -vh; // generous: we scroll past the hero
    });
    return imgs.every(img => img.complete);
  });
  await page.waitForTimeout(300);
}

test('stream + first category block run side-by-side as two columns (desktop 1440px)', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');

  const stream = page.locator('.stream-section, .home-parallel__stream');
  const firstCategoryPanel = page.locator('.category-panel, .home-parallel__category').first();

  await stream.scrollIntoViewIfNeeded();
  await waitForImages(page);

  const streamBox = await stream.boundingBox();
  const categoryBox = await firstCategoryPanel.boundingBox();

  expect(streamBox, 'stream section must exist').not.toBeNull();
  expect(categoryBox, 'first category panel must exist').not.toBeNull();

  const report: Record<string, any> = {};

  // 1. Same row: Y offsets must be within 24px of each other (parallel columns, not stacked)
  const yDiff = Math.abs(streamBox!.y - categoryBox!.y);
  report.yDiff = { actual: yDiff, maxAllowed: 24 };
  expect(yDiff, `stream.y=${streamBox!.y} vs category.y=${categoryBox!.y} must be within 24px (side-by-side)`).toBeLessThanOrEqual(24);

  // 2. Stream column sits to the LEFT of the category column (not above/below)
  report.streamX = streamBox!.x;
  report.categoryX = categoryBox!.x;
  expect(streamBox!.x, 'stream column must start left of category column').toBeLessThan(categoryBox!.x);
  expect(streamBox!.x + streamBox!.width, 'stream column must not overlap category column').toBeLessThanOrEqual(categoryBox!.x + 10);

  // 3. Width ratio: stream column should be ~28%-42% of the combined two-column width
  const totalWidth = streamBox!.width + categoryBox!.width;
  const streamRatio = streamBox!.width / totalWidth;
  report.streamRatio = streamRatio;
  expect(streamRatio, `stream width ratio ${streamRatio} must be between 0.28 and 0.42`).toBeGreaterThanOrEqual(0.28);
  expect(streamRatio).toBeLessThanOrEqual(0.42);

  fs.writeFileSync(
    path.resolve(__dirname, '..', 'artifacts', 'visual', 'home-parity-measurements.json'),
    JSON.stringify(report, null, 2)
  );
});

test('stream items use a left-thumb (not image-on-top) layout', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  const stream = page.locator('.stream-section, .home-parallel__stream');
  await stream.scrollIntoViewIfNeeded();
  await waitForImages(page);

  const firstItem = stream.locator('article, li').first();
  const itemBox = await firstItem.boundingBox();
  const img = firstItem.locator('img').first();
  const imgBox = await img.boundingBox();

  expect(itemBox, 'first stream item must exist').not.toBeNull();
  expect(imgBox, 'first stream item must have a thumbnail image').not.toBeNull();

  // thumb must sit at the left edge of the item (image-left layout)
  expect(Math.abs(imgBox!.x - itemBox!.x), 'thumbnail must be flush with the left edge of the item').toBeLessThanOrEqual(4);

  // thumb must be a minority of the item width (<=45%), proving it is NOT a full-width image-on-top card
  const widthRatio = imgBox!.width / itemBox!.width;
  expect(widthRatio, `thumb width ratio ${widthRatio} must be <= 0.45 (left-thumb layout, not image-top)`).toBeLessThanOrEqual(0.45);

  // aspect ratio close to VnExpress reference (145x87 = 1.667)
  const aspect = imgBox!.width / imgBox!.height;
  expect(aspect, `thumb aspect ratio ${aspect} must be between 1.4 and 1.9`).toBeGreaterThanOrEqual(1.4);
  expect(aspect).toBeLessThanOrEqual(1.9);

  // at least 6 items in the stream
  const count = await stream.locator('article, li').count();
  expect(count, 'stream must show at least 6 items').toBeGreaterThanOrEqual(6);
});

test('first category block: lead + side item side-by-side, then a 3-column list row', async ({ page }) => {
  await page.goto('/');
  await page.waitForLoadState('networkidle');
  const panel = page.locator('.category-panel, .home-parallel__category').first();
  await panel.scrollIntoViewIfNeeded();
  await waitForImages(page);

  const row1 = panel.locator('.category-panel__row1, .category-panel__body').first();
  const row1Children = row1.locator(':scope > *');
  const row1Count = await row1Children.count();
  expect(row1Count, 'row 1 of the first category block must have at least 2 side-by-side children (lead + side item)').toBeGreaterThanOrEqual(2);

  const first = row1Children.nth(0);
  const second = row1Children.nth(1);
  const firstBox = await first.boundingBox();
  const secondBox = await second.boundingBox();
  expect(firstBox).not.toBeNull();
  expect(secondBox).not.toBeNull();
  expect(Math.abs(firstBox!.y - secondBox!.y), 'lead and side item must be on the same row (not stacked)').toBeLessThanOrEqual(20);
  expect(firstBox!.x, 'lead must be left of side item').toBeLessThan(secondBox!.x);

  // lead image: must sit to the LEFT of the lead's own text (image-left, not image-on-top),
  // and occupy 40%-60% of the lead box width (reference: 225/460 = 49%).
  const leadImg = first.locator('img').first();
  const leadImgBox = await leadImg.boundingBox();
  expect(leadImgBox, 'category lead must have a thumbnail image').not.toBeNull();
  if (leadImgBox) {
    const leadImgAspect = leadImgBox.width / leadImgBox.height;
    expect(leadImgAspect, `lead image aspect ${leadImgAspect} must be between 1.4 and 1.9`).toBeGreaterThanOrEqual(1.4);
    expect(leadImgAspect).toBeLessThanOrEqual(1.9);

    const leadWidthRatio = leadImgBox.width / firstBox!.width;
    expect(leadWidthRatio, `lead image width ratio ${leadWidthRatio} must be between 0.4 and 0.6 of the lead box (image-left, not image-on-top)`).toBeGreaterThanOrEqual(0.4);
    expect(leadWidthRatio).toBeLessThanOrEqual(0.6);

    const leadTitle = first.locator('h3, h2').first();
    const leadTitleBox = await leadTitle.boundingBox();
    expect(leadTitleBox, 'category lead must have a title').not.toBeNull();
    expect(leadTitleBox!.x, 'lead title must sit to the right of the lead image (image-left layout)').toBeGreaterThanOrEqual(leadImgBox.x + leadImgBox.width - 4);
  }

  // row 2: at least 3 items arranged horizontally (same Y, different X)
  const row2 = panel.locator('.category-panel__row2');
  const row2Items = row2.locator(':scope > *');
  const row2Count = await row2Items.count();
  expect(row2Count, 'row 2 must have at least 3 list items').toBeGreaterThanOrEqual(3);

  const boxes = await Promise.all([0, 1, 2].map(i => row2Items.nth(i).boundingBox()));
  expect(boxes.every(b => b !== null)).toBeTruthy();
  const ys = boxes.map(b => b!.y);
  const maxYDiff = Math.max(...ys) - Math.min(...ys);
  expect(maxYDiff, 'row 2 items must be horizontally aligned (same row), not stacked vertically').toBeLessThanOrEqual(20);

  const xs = boxes.map(b => b!.x).sort((a, b) => a - b);
  expect(xs[0], 'row 2 items must be at different X positions (horizontal layout)').toBeLessThan(xs[2]);
});

test('no horizontal overflow at 390px and 768px after the redesign', async ({ page }) => {
  for (const width of [390, 768]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
    expect(scrollWidth, `no horizontal overflow at ${width}px`).toBeLessThanOrEqual(clientWidth + 2);
  }
});
