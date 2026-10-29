const { chromium } = require('@playwright/test');
const path = require('path');

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1600, height: 1000 } });
  const page = await context.newPage();

  const imgPath = path.resolve(__dirname, '..', 'references', 'vnexpress-desktop.png');
  const fileUrl = 'file:///' + imgPath.replace(/\\/g, '/');
  await page.goto(fileUrl);

  const result = await page.evaluate(() => {
    const img = document.querySelector('img');
    const canvas = document.createElement('canvas');
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(img, 0, 0);

    function colorAt(x, y) {
      const d = ctx.getImageData(x, y, 1, 1).data;
      return [d[0], d[1], d[2]];
    }
    function diff(a, b) { return Math.abs(a[0]-b[0]) + Math.abs(a[1]-b[1]) + Math.abs(a[2]-b[2]); }

    function scanVertical(x, y0, y1, threshold) {
      const transitions = [];
      let prev = colorAt(x, y0);
      for (let y = y0 + 1; y < y1; y++) {
        const c = colorAt(x, y);
        if (diff(c, prev) > threshold) transitions.push({ y, from: prev, to: c });
        prev = c;
      }
      return transitions;
    }
    function scanHorizontal(y, x0, x1, threshold) {
      const transitions = [];
      let prev = colorAt(x0, y);
      for (let x = x0 + 1; x < x1; x++) {
        const c = colorAt(x, y);
        if (diff(c, prev) > threshold) transitions.push({ x, from: prev, to: c });
        prev = c;
      }
      return transitions;
    }

    return {
      naturalWidth: img.naturalWidth,
      naturalHeight: img.naturalHeight,
      // vertical scans at various x to find row boundaries below the ad banner (y>260)
      vScan_x20_below270: scanVertical(20, 260, 900, 15),
      vScan_x400_below270: scanVertical(400, 260, 900, 15),
      vScan_x1300_below270: scanVertical(1300, 260, 900, 15),
      // horizontal scan just below masthead/nav to find left/right content edges
      hScan_y450: scanHorizontal(450, 0, 1440, 15),
      hScan_y300: scanHorizontal(300, 0, 1440, 15),
      // sample specific color points
      sample_y285_x30: colorAt(30, 285),
      sample_y360_x30: colorAt(30, 360),
      sample_y420_x30: colorAt(30, 420),
      sample_y420_x900: colorAt(900, 420),
    };
  });

  console.log(JSON.stringify(result));
  await browser.close();
})();
