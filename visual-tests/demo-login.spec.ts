import { test, expect } from '@playwright/test';
import { badge, demoLogin, signOutFromBadge, newestArticleSlug } from './support/auth';

// One-click demo sign-in: the four role cards, each role landing in its own area, leaving and switching demo mode. Isolated VNewsE2E database; DemoAuth is enabled only for that server.

test('login page: four role cards with descriptions, buttons and no secrets anywhere in the page', async ({ page }) => {
  await page.goto('/tai-khoan/dang-nhap');
  await expect(page.getByRole('heading', { name: 'Trải nghiệm sản phẩm' })).toBeVisible();
  const cards = page.locator('.demo-login--page .demo-card');
  await expect(cards).toHaveCount(4);
  for (const [title, button, desc] of [
    ['Độc giả', 'Vào demo Độc giả', 'Đọc tin, lưu bài viết'],
    ['Tác giả', 'Vào demo Tác giả', 'Viết bài, lưu bản nháp'],
    ['Biên tập viên', 'Vào demo Biên tập viên', 'duyệt hoặc từ chối'],
    ['Quản trị viên', 'Vào demo Quản trị viên', 'Quản lý chuyên mục và người dùng'],
  ]) {
    const card = cards.filter({ has: page.locator('.demo-card__title', { hasText: title }) });
    await expect(card).toHaveCount(1);
    await expect(card).toContainText(desc);
    await expect(card.getByRole('button', { name: button })).toBeVisible();
  }
  const html = await page.content();
  expect(html).not.toContain('Demo@12345');
  expect(html).not.toContain('demo.vnews.local');
  // Every card posts a form with a token to the server; nothing is wired by script.
  expect(await cards.evaluateAll((els) => els.map((e) => [(e as HTMLFormElement).method, new URL((e as HTMLFormElement).action).pathname, !!e.querySelector('input[name="__RequestVerificationToken"]')])))
    .toEqual(Array(4).fill(['post', '/tai-khoan/demo/dang-nhap', true]));
  expect(await page.locator('script').evaluateAll((els) => els.filter((e) => /demo/i.test(e.textContent ?? '')).length)).toBe(0);
});

test('Demo Reader: lands on the public site, shows the demo badge, saves an article to the account, cannot enter the CMS', async ({ page }) => {
  await demoLogin(page, 'reader');
  await expect(page).toHaveURL('/');
  await expect(badge(page)).toContainText('DEMO');
  await expect(badge(page)).toContainText('Độc giả');
  await expect(page.locator('.masthead__account-label')).toHaveText('Tài khoản'); // signed in, not "Đăng nhập"

  const slug = await newestArticleSlug(page);
  const bookmarksLoaded = page.waitForResponse((r) => r.url().includes('/api/me/bookmarks') && r.request().method() === 'GET');
  await page.goto(`/tin/${slug}`);
  await bookmarksLoaded; // the button reflects the account's saved list only after this request
  await page.waitForTimeout(200);
  const save = page.locator('.article-bottom-actions [data-action="save"]');
  // The demo reader's account persists between runs: start from "not saved" so the assertion is about this click.
  if ((await save.getAttribute('aria-pressed')) === 'true') {
    await save.click();
    await expect(save).toHaveAttribute('aria-pressed', 'false');
  }
  await save.click();
  await expect(page.locator('#site-toast')).toContainText('Đã lưu vào tài khoản');
  await expect(save).toHaveAttribute('aria-pressed', 'true');
  expect(await (await page.request.get('/api/me/bookmarks')).json()).toEqual(expect.arrayContaining([expect.objectContaining({ slug })]));
  await page.goto('/da-luu');
  await expect(page.locator('.saved-article__title').first()).toBeVisible();

  for (const path of ['/cms', '/cms/CmsUsers', '/cms/CmsArticles/Create']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: /403/ })).toBeVisible();
  }
});

test('Demo Author: lands in the newsroom, creates a draft, sends it for review; no admin areas', async ({ page }) => {
  await demoLogin(page, 'author');
  await expect(page).toHaveURL(/\/cms/);
  await expect(page.locator('.app-topbar')).toBeVisible();
  await expect(badge(page)).toContainText('Tác giả');

  const title = `Bản nháp demo tác giả ${Date.now()}`;
  await page.locator('.page-head__actions').getByRole('link', { name: 'Viết bài mới' }).click();
  await page.locator('#Title').fill(title);
  await page.locator('#CategoryId').selectOption({ index: 1 });
  await page.locator('#Summary').fill('Tóm tắt của bản nháp do tác giả demo tạo ra.');
  await page.locator('.rte__area').fill('Nội dung bản nháp của tác giả demo.');
  await page.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await expect(page.locator('.app-toast--success')).toContainText('Đã tạo bản nháp');

  await page.goto('/cms');
  const row = page.locator('tr', { hasText: title });
  await expect(row).toContainText('Bản nháp');
  await row.getByRole('button', { name: 'Gửi duyệt' }).click();
  await page.locator('#app-confirm [data-confirm-accept]').click();
  await expect(page.locator('tr', { hasText: title })).toContainText('Chờ duyệt');

  await expect(page.getByRole('link', { name: 'Người dùng' })).toHaveCount(0); // no admin menu for authors
  for (const path of ['/cms/CmsUsers', '/cms/CmsCategories']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: /403/ })).toBeVisible();
  }
});

test('Demo Editor: lands in the review queue, sees an author\'s pending article and the review actions; no admin areas', async ({ page, browser }) => {
  // An author sends something for review first (separate session).
  const title = `Chờ duyệt cho biên tập ${Date.now()}`;
  const authorContext = await browser.newContext();
  const author = await authorContext.newPage();
  await demoLogin(author, 'author');
  await author.goto('/cms/CmsArticles/Create');
  await author.locator('#Title').fill(title);
  await author.locator('#CategoryId').selectOption({ index: 1 });
  await author.locator('#Summary').fill('Bài chờ duyệt dùng để kiểm thử vai trò biên tập viên.');
  await author.locator('.rte__area').fill('Nội dung chờ duyệt của tác giả demo.');
  await author.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await author.goto('/cms');
  await author.locator('tr', { hasText: title }).getByRole('button', { name: 'Gửi duyệt' }).click();
  await author.locator('#app-confirm [data-confirm-accept]').click();
  await expect(author.locator('.app-toast--success')).toContainText('Đã gửi duyệt');
  await authorContext.close();

  await demoLogin(page, 'editor');
  await expect(page).toHaveURL(/\/cms(\/CmsArticles)?\?status=PendingReview/);
  await expect(badge(page)).toContainText('Biên tập viên');
  await page.goto(`/cms?status=PendingReview&q=${encodeURIComponent(title)}`); // the queue is oldest first and paged
  const row = page.locator('tr', { hasText: title });
  await expect(row).toBeVisible();
  await expect(row.getByRole('button', { name: 'Duyệt', exact: true })).toBeVisible();
  await expect(row.getByRole('button', { name: 'Từ chối' })).toBeVisible();
  for (const path of ['/cms/CmsUsers', '/cms/CmsCategories']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: /403/ })).toBeVisible();
  }
});

test('Demo Admin: lands on the system overview with the whole CMS available', async ({ page }) => {
  await demoLogin(page, 'admin');
  await expect(page).toHaveURL(/\/cms\/CmsOverview/);
  await expect(page.getByRole('heading', { name: 'Tổng quan hệ thống' })).toBeVisible();
  await expect(badge(page)).toContainText('Quản trị viên');
  const sidebar = page.locator('.app-sidebar');
  await expect(sidebar.getByRole('link', { name: 'Chuyên mục' })).toBeVisible();

  await sidebar.getByRole('link', { name: 'Người dùng' }).click();
  await expect(page.getByRole('heading', { name: 'Người dùng', level: 1 })).toBeVisible();
  // the list is paged and other specs register many accounts: find the demo admin through the search box
  await page.locator('#q').fill('demo.admin@demo.vnews.local');
  await page.getByRole('button', { name: 'Lọc', exact: true }).click();
  await expect(page.locator('td', { hasText: 'demo.admin@demo.vnews.local' })).toBeVisible();

  await sidebar.getByRole('link', { name: 'Chuyên mục' }).click();
  await expect(page.locator('.app-page-title')).toBeVisible();
  await expect(page).toHaveURL(/CmsCategories/);
  await sidebar.getByRole('link', { name: 'Tất cả bài viết' }).click();
  await expect(page).toHaveURL(/\/cms/);
});

test('leaving demo mode signs out: protected pages ask for sign-in again, the badge is gone, normal sign-out works too', async ({ page }) => {
  await demoLogin(page, 'admin');
  await expect(page).toHaveURL(/CmsOverview/);
  await signOutFromBadge(page);
  await expect(page).toHaveURL('/');
  await expect(badge(page)).toHaveCount(0);
  await page.goto('/cms/CmsUsers');
  await expect(page).toHaveURL(/\/tai-khoan\/dang-nhap\?returnUrl=/i);

  // Back button after sign-out must not reveal the protected page again.
  await page.goBack();
  await page.goto('/cms');
  await expect(page).toHaveURL(/\/tai-khoan\/dang-nhap/);

  // Same through the account page's own sign-out button.
  await demoLogin(page, 'reader');
  await page.goto('/tai-khoan/ho-so');
  await page.locator('main').getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page).toHaveURL('/');
  await expect(badge(page)).toHaveCount(0);
});

test('switching demo role replaces the session', async ({ page }) => {
  await demoLogin(page, 'admin');
  await expect(page).toHaveURL(/CmsOverview/);
  await signOutFromBadge(page);
  await demoLogin(page, 'reader');
  await expect(badge(page)).toContainText('Độc giả');
  await page.goto('/cms/CmsUsers');
  await expect(page.getByRole('heading', { name: /403/ })).toBeVisible();
});

test('sign-in dialog: demo cards work from any public page', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/chuyen-muc/cong-nghe');
  await page.locator('.masthead__account-link').first().click();
  const dialog = page.locator('#reader-login-dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('.demo-card')).toHaveCount(4);
  await dialog.locator('[data-demo-role="author"]').scrollIntoViewIfNeeded();
  await dialog.locator('[data-demo-role="author"]').click();
  await expect(page).toHaveURL(/\/cms/);
  await expect(badge(page)).toContainText('Tác giả');
});
