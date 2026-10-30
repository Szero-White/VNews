// One-off measurement script: extracts structural row/column bands from the locked VnExpress
// reference screenshots via pixel sampling, classifying each row as "white-ish background" vs
// "content" by sampling multiple x points per row (robust against single-pixel text noise).
// Not part of the Playwright Test suite; run manually with `node visual-tests/measure-reference.js`.
const { chromium } = require('@playwright/test');
const path = require('path');

const TARGETS = [
  { name: 'desktop', file: 'vnexpress-desktop.png' },
  { name: 'mobile', file: 'vnexpress-mobile.png' },
];

async function measure(page, file) {
  const imgPath = path.resolve(__dirname, '..', 'references', file);
  const fileUrl = 'file:///' + imgPath.replace(/\\/g, '/');
  await page.goto(fileUrl);

  return page.evaluate(() => {
    const img = document.querySelector('img');
    const canvas = document.createElement('canvas');
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(img, 0, 0);
    const w = canvas.width, h = canvas.height;
    const row = ctx.getImageData(0, 0, w, 1);

    function isWhiteAt(x, y) {
      const d = ctx.getImageData(x, y, 1, 1).data;
      return d[0] > 238 && d[1] > 238 && d[2] > 238;
    }
    function rowWhiteFraction(y, samples) {
      let white = 0;
      for (let i = 0; i < samples; i++) {
        const x = Math.round((w - 1) * (i / (samples - 1)));
        if (isWhiteAt(x, y)) white++;
      }
      return white / samples;
    }
    // classify every row (step 2px) as mostly-white (>=0.85) or content
    const bands = [];
    let current = null;
    for (let y = 0; y < h; y += 2) {
      const frac = rowWhiteFraction(y, 24);
      const isWhite = frac >= 0.85;
      if (current === null || current.isWhite !== isWhite) {
        if (current) bands.push(current);
        current = { isWhite, start: y, end: y };
      } else {
        current.end = y;
      }
    }
    if (current) bands.push(current);
    // merge bands shorter than 6px into neighbors (noise)
    const merged = [];
    for (const b of bands) {
      if (b.end - b.start < 6 && merged.length) {
        merged[merged.length - 1].end = b.end;
      } else {
        merged.push({ ...b });
      }
    }

    // column scan at a representative hero-row y to find left margin / image-text split / right margin
    function colWhiteScan(y) {
      const edges = [];
      let prevWhite = isWhiteAt(0, y);
      for (let x = 1; x < w; x++) {
        const white = isWhiteAt(x, y);
        if (white !== prevWhite) edges.push(x);
        prevWhite = white;
      }
      return edges;
    }

    return { naturalWidth: w, naturalHeight: h, bands: merged.map(b => ({ isWhite: b.isWhite, start: b.start, end: b.end, height: b.end - b.start })) };
  });
}

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1600, height: 1000 } });
  const page = await context.newPage();
  const out = {};
  for (const t of TARGETS) {
    out[t.name] = await measure(page, t.file);
  }
  console.log(JSON.stringify(out, null, 2));
  await browser.close();
})();
