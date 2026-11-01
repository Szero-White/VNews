import { test, expect, Page } from '@playwright/test';

// Article cover image experience in the CMS form: current image preview, new-file preview + name, replace, remove/undo,
// list markers, alignment, no JS errors. Isolated VNewsE2E database.

const PNG = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', 'base64');
const VIEWPORTS = [{ width: 1440, height: 900 }, { width: 1536, height: 864 }, { width: 768, height: 1024 }, { width: 390, height: 844 }];

function watch(page: Page) {
  const problems: string[] = [];
  page.on('pageerror', (e) => problems.push('pageerror: ' + e.message));
  // Network-level failures (a blocked or slow third-party font host) are not application errors; a missing local file would be a 404.
  const networkNoise = /net::ERR_(SOCKET_NOT_CONNECTED|NAME_NOT_RESOLVED|INTERNET_DISCONNECTED|CONNECTION_w+|TIMED_OUT|NETWORK_CHANGED)/;
  page.on('console', (m) => { if (m.type() === 'error' && !networkNoise.test(m.text())) problems.push('console: ' + m.text()); });
  return problems;
}
async function login(page: Page, role: 'author' | 'admin' = 'author') {
  await page.goto('/tai-khoan/dang-nhap');
  await page.locator(`.demo-login--page [data-demo-role="${role}"]`).click();
  await page.waitForURL((u) => !u.pathname.includes('dang-nhap'));
}
async function fillArticle(page: Page, title: string) {
  await page.locator('#Title').fill(title);
  await page.locator('#CategoryId').selectOption({ index: 1 });
  await page.locator('#Summary').fill('Tóm tắt bài viết dùng để kiểm thử ảnh bìa.');
  await page.locator('#Body').fill('<p>Nội dung bài viết kiểm thử ảnh bìa, đủ dài để qua bước kiểm tra độ dài tối thiểu.</p>');
}
const cover = (page: Page) => page.locator('[data-cover]');
const hiddenValue = (page: Page) => page.locator('input[name="CoverImageUrl"]').inputValue();

test('create: placeholder first, then a local preview with the file name, uploaded and saved; the edit page shows it as the current cover', async ({ page }) => {
  const problems = watch(page);
  await login(page);
  await page.goto('/cms/CmsArticles/Create');
  await expect(cover(page).locator('[data-cover-placeholder]')).toBeVisible();
  await expect(cover(page).getByText('Chưa có ảnh bìa')).toBeVisible();
  await expect(cover(page).locator('.cover__pick')).toContainText('Chọn ảnh');
  await expect(cover(page).locator('[data-cover-remove]')).toBeHidden();

  await page.locator('#coverFile').setInputFiles({ name: 'anh-bia-thu.png', mimeType: 'image/png', buffer: PNG });
  await expect(cover(page).locator('[data-cover-file-name]')).toHaveText('anh-bia-thu.png');
  await expect(cover(page).locator('[data-cover-img]')).toBeVisible();
  await expect(cover(page).locator('[data-cover-state]')).toContainText('Ảnh mới');
  await expect(page.locator('#uploadStatus')).toContainText('Đã tải lên');
  await expect.poll(() => hiddenValue(page)).toMatch(/^\/uploads\//);
  await expect(cover(page).locator('.cover__pick')).toContainText('Thay ảnh');

  const title = `Bài có ảnh bìa ${Date.now()}`;
  await fillArticle(page, title);
  await page.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await page.waitForURL(/\/CmsArticles\/Edit\/\d+/);

  // the edit form shows the stored image at once
  await expect(cover(page).locator('[data-cover-img]')).toBeVisible();
  await expect(cover(page).locator('[data-cover-state]')).toHaveText('Ảnh hiện tại');
  await expect(cover(page).locator('.cover__pick')).toContainText('Thay ảnh');
  expect(await cover(page).locator('[data-cover-img]').evaluate((i: HTMLImageElement) => i.complete && i.naturalWidth > 0)).toBe(true);

  // the list shows a thumbnail for this article
  await page.goto('/cms?q=' + encodeURIComponent(title));
  await expect(page.locator('tr', { hasText: title }).locator('img.article-cell__thumb')).toBeVisible();
  expect(problems).toEqual([]);
});

test('edit: remove marks the cover for removal, undo restores it, removing then saving really clears it', async ({ page }) => {
  await login(page);
  await page.goto('/cms/CmsArticles/Create');
  await page.locator('#coverFile').setInputFiles({ name: 'a.png', mimeType: 'image/png', buffer: PNG });
  await expect(page.locator('#uploadStatus')).toContainText('Đã tải lên');
  const title = `Bỏ ảnh bìa ${Date.now()}`;
  await fillArticle(page, title);
  await page.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await page.waitForURL(/\/CmsArticles\/Edit\/\d+/);
  const stored = await hiddenValue(page);
  expect(stored).toMatch(/^\/uploads\//);

  await cover(page).getByRole('button', { name: 'Bỏ ảnh' }).click();
  await expect(cover(page).locator('[data-cover-state]')).toHaveText('Sẽ bỏ ảnh bìa khi lưu');
  await expect(cover(page).locator('[data-cover-placeholder]')).toBeVisible();
  expect(await hiddenValue(page)).toBe('');

  await cover(page).getByRole('button', { name: 'Hoàn tác' }).click();
  await expect(cover(page).locator('[data-cover-state]')).toHaveText('Ảnh hiện tại');
  expect(await hiddenValue(page)).toBe(stored);

  await cover(page).getByRole('button', { name: 'Bỏ ảnh' }).click();
  await page.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await expect(page.locator('.app-alert--success')).toContainText('Đã lưu');
  await expect(cover(page).locator('[data-cover-placeholder]')).toBeVisible();
  await expect(cover(page).getByText('Chưa có ảnh bìa')).toBeVisible();
  await page.goto('/cms?q=' + encodeURIComponent(title));
  await expect(page.locator('tr', { hasText: title }).locator('.article-cell__thumb--none')).toBeVisible();
});

test('a failed upload keeps the previous image and says why; the save button is usable again', async ({ page }) => {
  await login(page);
  await page.goto('/cms/CmsArticles/Create');
  await page.locator('#coverFile').setInputFiles({ name: 'evil.svg', mimeType: 'image/svg+xml', buffer: Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" onload="alert(1)"/>') });
  await expect(page.locator('#uploadStatus')).toContainText('Lỗi');
  expect(await hiddenValue(page)).toBe('');
  await expect(cover(page).locator('[data-cover-placeholder]')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tạo bản nháp' })).toBeEnabled();
});

test('a published article opens read-only with its cover shown and no upload controls', async ({ page }) => {
  await login(page, 'admin');
  // a seeded article that has a cover (category filter: other specs publish cover-less test articles into the newest slots of the first category)
  const items = (await (await page.request.get('/api/v1/articles?category=cong-nghe&pageSize=50')).json()).items as { title: string; coverImageUrl?: string }[];
  const withCover = items.find((i) => i.coverImageUrl)!;
  await page.goto('/cms?status=Published&q=' + encodeURIComponent(withCover.title));
  await page.locator('tr:has(img.article-cell__thumb) a.table__title').first().click();
  await expect(cover(page).locator('[data-cover-img]')).toBeVisible();
  await expect(cover(page).locator('[data-cover-state]')).toHaveText('Ảnh hiện tại');
  await expect(page.locator('#coverFile')).toHaveCount(0);
  await expect(cover(page).getByRole('button', { name: 'Bỏ ảnh' })).toHaveCount(0);
});

for (const vp of VIEWPORTS) {
  test(`cover field layout @${vp.width}x${vp.height}: aligned with its label, inside the card, no horizontal scroll`, async ({ page }) => {
    await page.setViewportSize(vp);
    await login(page);
    for (const path of ['/cms/CmsArticles/Create', '/cms']) {
      await page.goto(path);
      expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(0);
    }
    await page.goto('/cms/CmsArticles/Create');
    await page.locator('#coverFile').setInputFiles({ name: 'a.png', mimeType: 'image/png', buffer: PNG });
    await expect(page.locator('#uploadStatus')).toContainText('Đã tải lên');
    const boxes = await page.evaluate(() => {
      const r = (s: string) => { const b = document.querySelector(s)!.getBoundingClientRect(); return { l: b.left, r: b.right, t: b.top, b: b.bottom }; };
      return { label: r('#cover-label'), stage: r('.cover__stage'), pick: r('.cover__pick'), hint: r('[data-cover-file-name]'), card: r('form.card .card__body'), vw: document.documentElement.clientWidth };
    });
    expect(Math.abs(boxes.label.l - boxes.stage.l)).toBeLessThanOrEqual(1); // label, preview, button and hint share one left edge
    expect(Math.abs(boxes.pick.l - boxes.stage.l)).toBeLessThanOrEqual(1);
    expect(Math.abs(boxes.hint.l - boxes.stage.l)).toBeLessThanOrEqual(1);
    expect(boxes.stage.r).toBeLessThanOrEqual(boxes.card.r + 1);
    expect(boxes.stage.l).toBeGreaterThanOrEqual(boxes.card.l - 1);
    expect(boxes.stage.r).toBeLessThanOrEqual(boxes.vw);
    expect(boxes.stage.b - boxes.stage.t).toBeGreaterThan(80); // a real preview, not a sliver
  });
}

test('keyboard: the file picker button takes focus with a visible ring', async ({ page }) => {
  await login(page);
  await page.goto('/cms/CmsArticles/Create');
  await page.locator('#coverFile').focus();
  const outline = await page.locator('.cover__pick').evaluate((e) => getComputedStyle(e).outlineStyle);
  expect(outline).not.toBe('none');
});

test('login page: no reader-only label and no staff link; demo cards stay', async ({ page }) => {
  await page.goto('/tai-khoan/dang-nhap');
  await expect(page.getByRole('heading', { name: 'Chào mừng trở lại' })).toBeVisible();
  await expect(page.getByText('TÀI KHOẢN ĐỘC GIẢ')).toHaveCount(0);
  await expect(page.getByText('Nhân viên tòa soạn')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'vào CMS' })).toHaveCount(0);
  await expect(page.locator('.demo-login--page .demo-card')).toHaveCount(4);
});

test('while an upload is still running the save button is disabled; it comes back when the upload ends', async ({ page }) => {
  await login(page);
  await page.goto('/cms/CmsArticles/Create');
  let release!: () => void;
  const gate = new Promise<void>((r) => { release = r; });
  await page.route('**/CmsArticles/UploadCoverImage', async (route) => { await gate; await route.continue(); });
  await page.locator('#coverFile').setInputFiles({ name: 'slow.png', mimeType: 'image/png', buffer: PNG });
  await expect(cover(page).locator('[data-cover-state]')).toContainText('đang tải lên');
  await expect(page.getByRole('button', { name: 'Tạo bản nháp' })).toBeDisabled();
  release();
  await expect(page.locator('#uploadStatus')).toContainText('Đã tải lên');
  await expect(page.getByRole('button', { name: 'Tạo bản nháp' })).toBeEnabled();
});

test('an article without a cover shows no empty status pill over the placeholder', async ({ page }) => {
  await login(page);
  await page.goto('/cms/CmsArticles/Create');
  await expect(cover(page).locator('[data-cover-placeholder]')).toBeVisible();
  await expect(cover(page).locator('[data-cover-state]')).toBeHidden();
});
