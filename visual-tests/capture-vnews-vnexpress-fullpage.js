/**
 * VNews x VnExpress — full-page screenshots for visual review.
 * Place this file under PROJECT/visual-tests/ and run:
 *   node .\visual-tests\capture-vnews-vnexpress-fullpage.js
 * Requires the project's existing @playwright/test dependency.
 * No source, database or Git changes; only writes to artifacts/REVIEW-TO-SEND/.
 */
'use strict';
const { chromium } = require('@playwright/test');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const output = path.join(root, 'artifacts', 'REVIEW-TO-SEND', 'fullpage-comparison');
const localUrl = process.env.VNEWS_BASE_URL || 'http://localhost:5299/';
const referenceUrl = 'https://vnexpress.net/';
const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const screens = [
  { name: 'desktop', viewport: { width: 1440, height: 900 }, isMobile: false },
  { name: 'mobile', viewport: { width: 390, height: 844 }, isMobile: true },
];

const mobileUA = 'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Mobile Safari/537.36';

async function warmLazyContent(page) {
  // Scroll the entire document to make below-fold image loading observable.
  // New sections may extend the page during the scan.
  let last = -1;
  let repeat = 0;
  for (let i = 0; i < 165; i++) {
    const state = await page.evaluate(() => ({
      top: window.scrollY,
      height: Math.max(document.body.scrollHeight, document.documentElement.scrollHeight),
      viewport: window.innerHeight,
    }));
    const step = Math.max(300, Math.floor(state.viewport * 0.85));
    const target = Math.min(state.height - state.viewport, state.top + step);
    if (target <= state.top + 1) {
      repeat++;
      if (repeat >= 3) break;
    } else repeat = 0;
    await page.evaluate((y) => window.scrollTo(0, y), Math.max(0, target));
    await wait(100);
    if (last === state.top && repeat > 1) break;
    last = state.top;
  }
  await page.evaluate(() => window.scrollTo(0, 0));
  await wait(350);
  await page.evaluate(async () => { if (document.fonts?.ready) await document.fonts.ready; });
}

async function capture(browser, site, spec) {
  const ctx = await browser.newContext({
    viewport: spec.viewport,
    deviceScaleFactor: 1,
    isMobile: spec.isMobile,
    hasTouch: spec.isMobile,
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    ...(spec.isMobile ? { userAgent: mobileUA } : {}),
  });
  const page = await ctx.newPage();
  const pageErrors = [];
  page.on('pageerror', (error) => pageErrors.push(String(error).slice(0, 400)));
  const name = `${site.name}-${spec.name}`;
  try {
    console.log(`OPEN ${site.url} (${spec.viewport.width}x${spec.viewport.height})`);
    const response = await page.goto(site.url, { waitUntil: 'domcontentloaded', timeout: 45000 });
    if (!response || response.status() >= 400) throw new Error(`HTTP ${response?.status() ?? 'no response'}`);
    await wait(1200);
    await warmLazyContent(page);
    const metrics = await page.evaluate(() => {
      const rect = (selector) => {
        const e = document.querySelector(selector);
        if (!e) return null;
        const r = e.getBoundingClientRect();
        return { x: Math.round(r.x), y: Math.round(r.y + window.scrollY), width: Math.round(r.width), height: Math.round(r.height) };
      };
      return {
        scrollHeight: Math.max(document.body.scrollHeight, document.documentElement.scrollHeight),
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
        scrollY: window.scrollY,
        title: document.title,
        hero: rect('.hero-lead__image-link img, .section_topstory .thumb-art img, .section_topstory article img'),
      };
    });
    const top = path.join(output, `${name}-top.png`);
    const full = path.join(output, `${name}-full.png`);
    await page.screenshot({ path: top, animations: 'disabled', timeout: 45000 });
    try {
      await page.screenshot({ path: full, fullPage: true, animations: 'disabled', timeout: 90000 });
      console.log(`OK: ${full} (height ${metrics.scrollHeight}px)`);
      return { success: true, url: page.url(), status: response.status(), files: [top, full], metrics, pageErrors };
    } catch (e) {
      // Extremely long pages may exceed Chromium's single-image limits.
      // Still capture all sections without changing the website or requiring sharp.
      const parts = [];
      const tileHeight = 6000;
      let docHeight = Math.min(metrics.scrollHeight, 100000);
      for (let y = 0, part = 1; y < docHeight; y += tileHeight, part++) {
        const height = Math.min(tileHeight, docHeight - y);
        const partPath = path.join(output, `${name}-part-${String(part).padStart(2, '0')}.png`);
        await page.screenshot({ path: partPath, clip: { x: 0, y, width: spec.viewport.width, height }, animations: 'disabled', timeout: 90000 });
        parts.push(partPath);
      }
      console.warn(`FULL screenshot too tall; wrote ${parts.length} consecutive parts for ${name}`);
      return { success: true, partialTiles: true, warning: String(e).slice(0, 300), url: page.url(), status: response.status(), files: [top, ...parts], metrics, pageErrors };
    }
  } catch (e) {
    console.error(`FAILED ${name}: ${String(e).slice(0, 900)}`);
    return { success: false, error: String(e).slice(0, 1000), pageErrors };
  } finally {
    await ctx.close();
  }
}

async function main() {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  const report = { capturedAt: new Date().toISOString(), note: 'Live content and ads change; visual parity must use comparable content and a fixed reference.', results: {} };
  try {
    for (const spec of screens) {
      for (const site of [{ name: 'vnexpress', url: referenceUrl }, { name: 'vnews', url: localUrl }]) {
        report.results[`${site.name}-${spec.name}`] = await capture(browser, site, spec);
      }
    }
  } finally {
    await browser.close();
  }
  const reportPath = path.join(output, 'fullpage-capture-report.json');
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), 'utf8');
  console.log(`REPORT: ${reportPath}`);
  if (Object.values(report.results).some((result) => !result.success)) process.exitCode = 1;
}
main().catch((error) => { console.error(error); process.exitCode = 1; });
