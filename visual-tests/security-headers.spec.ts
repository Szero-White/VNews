import { test, expect, Page, Browser } from '@playwright/test';

// Proves the Content-Security-Policy added in Backend Reliability & Security Phase 1 does not
// break the real UI: no CSP violation is reported and page scripts still run.
function watchViolations(page: Page) {
  const problems: string[] = [];
  page.on('console', msg => {
    const text = msg.text();
    if (msg.type() === 'error' && /Content Security Policy|Refused to (load|execute|apply)/i.test(text)) problems.push(text);
  });
  page.on('pageerror', err => problems.push('pageerror: ' + err.message));
  return problems;
}

async function loginCms(page: Page, email: string) {
  await page.goto('/cms/dang-nhap');
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill('Demo@12345');
  await page.locator('form button[type="submit"]').click();
  await page.waitForURL(/\/cms/);
}

for (const path of ['/', '/tin-moi-nhat', '/chuyen-muc/cong-nghe', '/tim-kiem?q=cong', '/tai-khoan/dang-nhap', '/cms/dang-nhap']) {
  test(`no CSP violations on ${path}`, async ({ page }) => {
    const problems = watchViolations(page);
    const response = await page.goto(path);
    await page.waitForLoadState('networkidle');
    expect(response!.headers()['content-security-policy']).toContain("script-src 'self' 'nonce-");
    expect(response!.headers()['x-content-type-options']).toBe('nosniff');
    expect(problems).toEqual([]);
  });
}

test('article page: scripts (save/share/font size) still run under the policy', async ({ page }) => {
  const problems = watchViolations(page);
  await page.goto('/');
  await page.locator('.hero-lead__title a').first().click();
  await page.waitForLoadState('networkidle');
  expect(problems).toEqual([]);
  // site.js is an external same-origin script; it must have executed (it fills the date label).
  await expect(page.locator('#today-date')).not.toBeEmpty();
});

test('CMS: nonce-protected inline upload script runs and the server refuses SVG', async ({ page }) => {
  const problems = watchViolations(page);
  await loginCms(page, 'author@vnews.local');
  await page.goto('/cms/CmsArticles/Create');
  await page.waitForLoadState('networkidle');

  // The picker only offers raster images.
  await expect(page.locator('#coverFile')).toHaveAttribute('accept', 'image/jpeg,image/png,image/webp');

  // Bypass the picker's filter and send an SVG anyway: the inline script (allowed only because of
  // its nonce) posts it, and the server must answer with an error, not store it.
  await page.locator('#coverFile').setInputFiles({
    name: 'evil.svg',
    mimeType: 'image/svg+xml',
    buffer: Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" onload="alert(1)"/>'),
  });
  await expect(page.locator('#uploadStatus')).toContainText('Lỗi', { timeout: 5000 });
  await expect(page.locator('[name="CoverImageUrl"]')).toHaveValue('');
  expect(problems).toEqual([]);
});

// The suite runs against its own isolated database (see playwright.config.ts), which starts without any
// article awaiting review, so the test creates one through the real CMS UI as the demo author.
async function ensurePendingArticle(browser: Browser, baseURL: string) {
  const context = await browser.newContext({ baseURL });
  const author = await context.newPage();
  await loginCms(author, 'author@vnews.local');
  await author.goto('/cms/CmsArticles/Create');
  await author.locator('#Title').fill(`Bai cho duyet e2e ${Date.now()}`);
  await author.locator('#CategoryId').selectOption({ index: 1 });
  await author.locator('#Summary').fill('Tom tat bai cho duyet dung de kiem thu giao dien.');
  await author.locator('#Body').fill('<p>Noi dung kiem thu.</p>');
  await author.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await author.waitForURL(/\/CmsArticles\/Edit\/\d+/i);
  await author.getByRole('button', { name: /Gửi duyệt/ }).click();
  await context.close();
}

test('CMS: reject button works without an inline handler (cancel keeps the form unsent)', async ({ page, browser, baseURL }) => {
  const problems = watchViolations(page);
  await loginCms(page, 'editor@vnews.local');
  await page.goto('/cms/CmsArticles?status=PendingReview');
  if (await page.locator('form.js-reject-form').count() === 0) {
    await ensurePendingArticle(browser, baseURL!);
    await page.goto('/cms/CmsArticles?status=PendingReview');
  }
  const rejectForm = page.locator('form.js-reject-form').first();
  await expect(rejectForm).toHaveCount(1);

  let prompted = false;
  page.on('dialog', async dialog => { prompted = true; await dialog.dismiss(); });
  const urlBefore = page.url();
  await rejectForm.locator('button[type="submit"]').click();

  // The prompt only appears if the nonce-protected script attached its submit listener.
  await expect.poll(() => prompted).toBe(true);
  expect(page.url()).toBe(urlBefore);
  expect(problems).toEqual([]);
});
