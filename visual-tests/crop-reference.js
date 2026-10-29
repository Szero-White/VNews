const { chromium } = require('@playwright/test');
const path = require('path');
const fs = require('fs');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 10600 } });
  const imgPath = path.resolve(__dirname, '..', 'artifacts', 'survey', 'vnexpress-home-desktop-full.png');
  const fileUrl = 'file:///' + imgPath.split(path.sep).join('/');
  await page.goto(fileUrl);

  const outDir = path.resolve(__dirname, '..', 'artifacts', 'survey', 'ref-crops');
  fs.mkdirSync(outDir, { recursive: true });

  const crops = [
    ['stream.png', 0, 710, 1440, 1600],
    ['khcn-block.png', 0, 3434, 1440, 743],
    ['podcasts.png', 0, 4577, 1440, 270],
    ['thoisu-block.png', 0, 4847, 1440, 700],
    ['xe-block.png', 0, 6036, 1440, 516],
    ['dulich-block.png', 0, 6552, 1440, 556],
    ['anh-block.png', 0, 7809, 1440, 601],
    ['spotlight-block.png', 0, 8410, 1440, 1138],
    ['footer.png', 0, 10082, 1440, 468],
  ];

  for (const c of crops) {
    await page.screenshot({ path: path.join(outDir, c[0]), clip: { x: c[1], y: c[2], width: c[3], height: c[4] } });
  }

  await browser.close();
  console.log('done');
})();
