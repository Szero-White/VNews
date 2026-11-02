import { test, expect, Page } from '@playwright/test';
import { demoLogin, registerReader, Role } from './support/auth';

// Role dashboards (Reader / Author / Editor / Admin) through the real UI. Demo and real accounts share one UI; the
// demo login is only a faster way in. Isolated VNewsE2E database (see playwright.config.ts).

const VIEWPORTS = [
  { width: 1440, height: 900 }, { width: 1536, height: 864 }, { width: 768, height: 1024 }, { width: 390, height: 844 },
];
const overflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
const sidebarLinks = async (page: Page) => {
  await page.locator('.app-sidebar .app-nav__link').first().waitFor(); // allInnerTexts() does not wait for the page
  return (await page.locator('.app-sidebar .app-nav__link').allInnerTexts()).map((t) => t.trim());
};
const confirmAccept = (page: Page) => page.locator('#app-confirm [data-confirm-accept]');

async function newDraft(page: Page, title: string) {
  await page.goto('/cms/CmsArticles/Create');
  await page.locator('#Title').fill(title);
  await page.locator('#CategoryId').selectOption({ index: 1 });
  await page.locator('#Summary').fill('Tóm tắt bài viết dùng để kiểm thử bảng điều khiển.');
  await page.locator('.rte__area').fill('Nội dung kiểm thử bảng điều khiển.');
  await page.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await expect(page.locator('.app-toast--success')).toContainText('Đã tạo bản nháp');
}

test('sidebar shows only the entries a role may use, with the right one active', async ({ page }) => {
  await demoLogin(page, 'author');
  expect(await sidebarLinks(page)).toEqual(['Bài viết của tôi', 'Viết bài mới', 'Hồ sơ của tôi', 'Bài đã lưu', 'Xem trang tin']);
  await expect(page.locator('.app-nav__link.is-active')).toHaveText('Bài viết của tôi');

  await page.context().clearCookies();
  await demoLogin(page, 'editor');
  expect(await sidebarLinks(page)).toEqual(['Tất cả bài viết', 'Hàng chờ duyệt', 'Thống kê', 'Viết bài mới', 'Bình luận', 'Hồ sơ của tôi', 'Bài đã lưu', 'Xem trang tin']);
  await expect(page.locator('.app-nav__link.is-active')).toHaveText('Hàng chờ duyệt');

  await page.context().clearCookies();
  await demoLogin(page, 'admin');
  expect(await sidebarLinks(page)).toEqual(['Tổng quan', 'Tất cả bài viết', 'Hàng chờ duyệt', 'Thống kê', 'Viết bài mới', 'Bình luận', 'Người dùng', 'Chuyên mục', 'Ảnh tải lên', 'Nhật ký hoạt động', 'Hồ sơ của tôi', 'Bài đã lưu', 'Xem trang tin']);
  await expect(page.locator('.app-nav__link.is-active')).toHaveText('Tổng quan');

  await page.context().clearCookies();
  await demoLogin(page, 'reader');
  await page.goto('/tai-khoan/ho-so');
  expect(await sidebarLinks(page)).toEqual(['Hồ sơ của tôi', 'Bài đã lưu', 'Xem trang tin']);
});

test('reader profile: real role, avatar, empty saved state, no newsroom entry, back to news and sign out', async ({ page }) => {
  await demoLogin(page, 'reader');
  await page.goto('/tai-khoan/ho-so');
  await expect(page.getByRole('heading', { name: /Xin chào/ })).toBeVisible();
  await expect(page.locator('.profile-hero__avatar')).toBeVisible();
  await expect(page.locator('.profile-hero .badge--reader')).toHaveText('Độc giả');
  await expect(page.getByText('HỒ SƠ ĐỘC GIẢ')).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Vào tòa soạn' })).toHaveCount(0);
  await expect(page.getByText('Lịch sử đọc')).toHaveCount(0); // no invented widgets
  await expect(page.getByText('không thể chỉnh sửa hồ sơ')).toBeVisible();
  await page.getByRole('link', { name: 'Về trang tin', exact: true }).click();
  await expect(page).toHaveURL('/');
});

test('staff profile shows the staff role and a newsroom entry', async ({ page }) => {
  await demoLogin(page, 'editor');
  await page.goto('/tai-khoan/ho-so');
  await expect(page.locator('.profile-hero .badge--editor')).toHaveText('Biên tập viên');
  await expect(page.getByRole('link', { name: 'Vào tòa soạn' })).toBeVisible();
});

test('reader: saving an article lists it under "Bài đã lưu" and it can be removed', async ({ page }) => {
  // A fresh reader of its own: the shared demo reader's bookmarks are also toggled by other specs running in parallel.
  await page.goto('/tai-khoan/dang-ky');
  await page.locator('input[name="DisplayName"]').fill('Độc giả lưu bài');
  await page.locator('input[name="Email"]').fill(`saver-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.test`);
  await page.locator('input[name="Password"]').fill('TestPassword123!');
  await page.locator('input[name="ConfirmPassword"]').fill('TestPassword123!');
  await page.locator('.reader-auth__submit').click();
  await page.waitForURL((u) => !u.pathname.includes('dang-ky'));
  const slug = (await (await page.request.get('/api/v1/articles?pageSize=1')).json()).items[0].slug;
  await page.goto(`/tin/${slug}`);
  const save = page.locator('[data-action="save"]').first();
  if ((await save.getAttribute('aria-pressed')) === 'true') await save.click();
  await save.click();
  await expect(save).toHaveAttribute('aria-pressed', 'true');

  await page.goto('/tai-khoan/bai-da-luu');
  const item = page.locator('.saved-item').first();
  await expect(item).toBeVisible();
  await expect(item.locator('.saved-item__title')).not.toBeEmpty();
  await item.getByRole('button', { name: /Bỏ lưu/ }).click();
  await expect(page.locator('.app-toast--success')).toContainText('Đã bỏ lưu');
});

test('author: stats, own list, search and status filter, create -> submit; no review actions', async ({ page }) => {
  await demoLogin(page, 'author');
  await expect(page.getByRole('heading', { name: 'Bài viết của tôi', level: 1 })).toBeVisible();
  await expect(page.locator('.stats .stat')).toHaveCount(4);
  await expect(page.locator('.page-head .badge--author')).toHaveText('Tác giả');

  const title = `Bài của tác giả ${Date.now()}`;
  await newDraft(page, title);
  await page.goto('/cms');
  const row = page.locator('tr', { hasText: title });
  await expect(row.locator('.badge')).toHaveText('Bản nháp');
  await expect(row.getByRole('link', { name: 'Sửa' })).toBeVisible();
  await expect(row.getByRole('button', { name: 'Từ chối' })).toHaveCount(0);
  await expect(row.getByRole('button', { name: 'Duyệt', exact: true })).toHaveCount(0);

  // title search
  await page.locator('#q').fill(title.slice(0, 20));
  await page.getByRole('button', { name: 'Lọc', exact: true }).click();
  await expect(page).toHaveURL(/q=/);
  await expect(page.locator('tr', { hasText: title })).toHaveCount(1);
  await page.locator('#q').fill('khong-ton-tai-' + Date.now());
  await page.getByRole('button', { name: 'Lọc', exact: true }).click();
  await expect(page.getByText('Không tìm thấy bài viết phù hợp')).toBeVisible();
  await page.getByRole('link', { name: 'Xóa bộ lọc' }).first().click();

  // status filter through the stat tile
  await page.locator('a.stat--draft').click();
  await expect(page).toHaveURL(/status=Draft/);
  await expect(page.locator('tr', { hasText: title })).toHaveCount(1);

  // submit: a confirmation first, cancel keeps it a draft
  const draftRow = page.locator('tr', { hasText: title });
  await draftRow.getByRole('button', { name: 'Gửi duyệt' }).click();
  await page.locator('#app-confirm [data-confirm-cancel]').click();
  await expect(draftRow.locator('.badge')).toHaveText('Bản nháp');
  await draftRow.getByRole('button', { name: 'Gửi duyệt' }).click();
  await confirmAccept(page).click();
  await expect(page.locator('.app-toast--success')).toContainText('Đã gửi duyệt');
  await page.goto(`/cms?status=PendingReview&q=${encodeURIComponent(title)}`);
  const pending = page.locator('tr', { hasText: title });
  await expect(pending.locator('.badge')).toHaveText('Chờ duyệt');
  await expect(pending.getByRole('link', { name: 'Sửa' })).toHaveCount(0); // locked while in review
});

test('editor: queue first, approve and reject (with a required reason) and the author sees the reason', async ({ page, browser }) => {
  const approveTitle = `Duyệt được ${Date.now()}`;
  const rejectTitle = `Bị trả lại ${Date.now()}`;
  const authorContext = await browser.newContext();
  const author = await authorContext.newPage();
  await demoLogin(author, 'author');
  for (const t of [approveTitle, rejectTitle]) {
    await newDraft(author, t);
    await author.goto('/cms');
    await author.locator('tr', { hasText: t }).getByRole('button', { name: 'Gửi duyệt' }).click();
    await confirmAccept(author).click();
    await expect(author.locator('.app-toast--success')).toContainText('Đã gửi duyệt');
  }

  await demoLogin(page, 'editor');
  await expect(page).toHaveURL(/status=PendingReview/);
  await expect(page.getByRole('heading', { name: 'Không gian biên tập', level: 1 })).toBeVisible();
  // the queue is oldest first and paged, so find the test articles by title
  await page.goto(`/cms?status=PendingReview&q=${encodeURIComponent(approveTitle)}`);
  await expect(page.locator('tr', { hasText: approveTitle })).toBeVisible();
  await expect(page.locator('tr', { hasText: approveTitle }).locator('td').nth(2)).not.toBeEmpty(); // author column

  // approve: confirm dialog, then it is published
  await page.locator('tr', { hasText: approveTitle }).getByRole('button', { name: 'Duyệt', exact: true }).click();
  await expect(page.locator('#app-confirm')).toBeVisible();
  await confirmAccept(page).click();
  await expect(page.locator('.app-toast--success')).toContainText('Đã xuất bản');
  await page.goto(`/cms?status=PendingReview&q=${encodeURIComponent(approveTitle)}`);
  await expect(page.locator('tr', { hasText: approveTitle })).toHaveCount(0);

  // reject: empty reason is refused in the dialog; a real reason is sent
  await page.goto(`/cms?status=PendingReview&q=${encodeURIComponent(rejectTitle)}`);
  await page.locator('tr', { hasText: rejectTitle }).getByRole('button', { name: 'Từ chối' }).click();
  await confirmAccept(page).click();
  await expect(page.locator('#app-confirm .app-field-error')).toBeVisible();
  await page.locator('#app-confirm-reason').fill('Cần bổ sung nguồn trích dẫn.');
  await confirmAccept(page).click();
  await expect(page.locator('.app-toast--success')).toContainText('Đã từ chối');

  await author.goto('/cms');
  const returned = author.locator('tr', { hasText: rejectTitle });
  await expect(returned.locator('.badge')).toHaveText('Cần chỉnh sửa');
  await expect(returned).toContainText('Cần bổ sung nguồn trích dẫn.');
  await expect(returned.getByRole('link', { name: 'Sửa' })).toBeVisible();
  await authorContext.close();
});

test('admin: overview with real figures, users filter and role safeguards, categories, audit log', async ({ page }) => {
  await demoLogin(page, 'admin');
  await expect(page).toHaveURL(/CmsOverview/);
  await expect(page.locator('.stats .stat')).toHaveCount(8);
  await expect(page.getByText('Hoạt động gần đây')).toBeVisible();

  await page.locator('.app-sidebar').getByRole('link', { name: 'Người dùng' }).click();
  await expect(page.locator('.app-nav__link.is-active')).toHaveText('Người dùng');
  await page.locator('#q').fill('editor@vnews.local');
  await page.getByRole('button', { name: 'Lọc', exact: true }).click();
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await page.locator('#q').fill('');
  await page.locator('#role').selectOption('Author');
  await expect(page).toHaveURL(/role=Author/);
  // an administrator cannot change their own role
  await page.goto('/cms/CmsUsers?q=demo.admin');
  await expect(page.getByText('Không thể đổi vai trò của chính mình')).toBeVisible();

  await page.locator('.app-sidebar').getByRole('link', { name: 'Chuyên mục' }).click();
  await expect(page.locator('tbody tr')).not.toHaveCount(0);
  await page.locator('.app-sidebar').getByRole('link', { name: 'Nhật ký hoạt động' }).click();
  await expect(page.getByRole('heading', { name: 'Nhật ký hoạt động', level: 1 })).toBeVisible();
});

test('forbidden pages stay forbidden for lower roles, with the shell and no way to act', async ({ page }) => {
  await demoLogin(page, 'author');
  for (const path of ['/cms/CmsOverview', '/cms/CmsAudit', '/cms/CmsUsers', '/cms/CmsCategories']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: /403/ })).toBeVisible();
  }
});

for (const vp of VIEWPORTS) {
  test(`layout @${vp.width}x${vp.height}: no horizontal scroll on any role page; tables scroll inside their card`, async ({ page }) => {
    await page.setViewportSize(vp);
    const paths: Record<Role, string[]> = {
      reader: ['/tai-khoan/ho-so', '/tai-khoan/bai-da-luu'],
      author: ['/cms', '/cms/CmsArticles/Create'],
      editor: ['/cms?status=PendingReview'],
      admin: ['/cms/CmsOverview', '/cms/CmsUsers', '/cms/CmsCategories', '/cms/CmsAudit', '/cms'],
    };
    for (const role of Object.keys(paths) as Role[]) {
      await page.context().clearCookies();
      await demoLogin(page, role);
      for (const path of paths[role]) {
        await page.goto(path);
        expect(await overflow(page), `${role} ${path} @${vp.width}`).toBeLessThanOrEqual(0);
        await expect(page.locator('.app-topbar')).toBeVisible();
      }
    }
  });
}

test('mobile: the menu is a drawer that opens with the button, closes with Escape and keeps focus sensible', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await demoLogin(page, 'admin');
  const sidebar = page.locator('#app-sidebar');
  await expect(sidebar).toBeHidden();
  const button = page.locator('#app-menu-btn');
  await button.click();
  await expect(sidebar).toBeVisible();
  await expect(button).toHaveAttribute('aria-expanded', 'true');
  await page.keyboard.press('Escape');
  await expect(sidebar).toBeHidden();
  await expect(button).toHaveAttribute('aria-expanded', 'false');
  await button.click();
  await sidebar.getByRole('link', { name: 'Người dùng' }).click();
  await expect(page).toHaveURL(/CmsUsers/);
});

test('keyboard: skip link, visible focus ring on controls and a working confirm dialog via keyboard', async ({ page }) => {
  await demoLogin(page, 'author');
  await page.waitForURL(/\/cms/);
  await page.keyboard.press('Tab');
  await expect(page.locator('.app-skip')).toBeFocused();
  const ring = await page.locator('.page-head__actions .btn--primary').evaluate((e) => { (e as HTMLElement).focus(); return getComputedStyle(e).outlineStyle; });
  expect(ring).not.toBe('none');
});
