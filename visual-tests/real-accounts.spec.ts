import { test, expect, Page } from '@playwright/test';

// Final QA: REAL accounts (signed in through the normal form) must get the same UI as the demo accounts, with no demo
// badge; every role page must run without JavaScript exceptions or console errors; the floating demo badge must never
// cover a control. Isolated VNewsE2E database.

type Role = 'reader' | 'author' | 'editor' | 'admin';
const REAL: Record<Exclude<Role, 'reader'>, string> = { author: 'author@vnews.local', editor: 'editor@vnews.local', admin: 'admin@vnews.local' };
const PASSWORD = 'Demo@12345';

function watch(page: Page) {
  const problems: string[] = [];
  page.on('pageerror', (e) => problems.push('pageerror: ' + e.message));
  // Network-level failures (a blocked or slow third-party font host) are not application errors; a missing local file would be a 404.
  const networkNoise = /net::ERR_(SOCKET_NOT_CONNECTED|NAME_NOT_RESOLVED|INTERNET_DISCONNECTED|CONNECTION_w+|TIMED_OUT|NETWORK_CHANGED)/;
  page.on('console', (m) => { if (m.type() === 'error' && !networkNoise.test(m.text())) problems.push('console: ' + m.text()); });
  return problems;
}
async function realLogin(page: Page, email: string, password = PASSWORD) {
  await page.goto('/tai-khoan/dang-nhap');
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill(password);
  await page.locator('.reader-auth__submit').click();
  await page.waitForURL((u) => !u.pathname.includes('dang-nhap'));
}
async function demoLogin(page: Page, role: Role) {
  await page.goto('/tai-khoan/dang-nhap');
  await page.locator(`.demo-login--page [data-demo-role="${role}"]`).click();
  await page.waitForURL((u) => !u.pathname.includes('dang-nhap'));
}
const sidebar = async (page: Page) => (await page.locator('.app-sidebar .app-nav__link').allInnerTexts()).map((t) => t.trim());
const PAGES: Record<Role, string[]> = {
  reader: ['/tai-khoan/ho-so', '/tai-khoan/bai-da-luu'],
  author: ['/cms', '/cms/CmsArticles/Create', '/tai-khoan/ho-so'],
  editor: ['/cms', '/cms?status=PendingReview', '/tai-khoan/ho-so'],
  admin: ['/cms/CmsOverview', '/cms', '/cms/CmsUsers', '/cms/CmsCategories', '/cms/CmsCategories/Create', '/cms/CmsAudit', '/tai-khoan/ho-so'],
};

for (const role of ['author', 'editor', 'admin'] as const) {
  test(`real ${role}: same sidebar as the demo ${role}, no demo badge, no JavaScript errors on any page`, async ({ browser }) => {
    const realCtx = await browser.newContext();
    const real = await realCtx.newPage();
    const problems = watch(real);
    await realLogin(real, REAL[role]);
    await real.goto(PAGES[role][0]);
    const realLinks = await sidebar(real);
    for (const path of PAGES[role]) {
      await real.goto(path);
      await expect(real.locator('.app-topbar')).toBeVisible();
      await expect(real.locator('.demo-badge')).toHaveCount(0);
    }

    const demoCtx = await browser.newContext();
    const demo = await demoCtx.newPage();
    watch(demo);
    await demoLogin(demo, role);
    await demo.goto(PAGES[role][0]);
    expect(await sidebar(demo)).toEqual(realLinks); // identical per-role UI
    await expect(demo.locator('.demo-badge')).toHaveCount(1);
    await realCtx.close();
    await demoCtx.close();
    expect(problems).toEqual([]);
  });
}

test('real reader: registers, sees profile + saved articles, renames the account, cannot enter the newsroom', async ({ page }) => {
  const problems = watch(page);
  const email = `reader-${Date.now()}@example.test`;
  await page.goto('/tai-khoan/dang-ky');
  await page.locator('input[name="DisplayName"]').fill('Độc giả kiểm thử');
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill('TestPassword123!');
  await page.locator('input[name="ConfirmPassword"]').fill('TestPassword123!');
  await page.locator('.reader-auth__submit').click();
  await page.waitForURL((u) => !u.pathname.includes('dang-ky'));

  await page.goto('/tai-khoan/ho-so');
  await expect(page.locator('.profile-hero .badge--reader')).toHaveText('Độc giả');
  await expect(page.locator('.app-user__name')).toHaveText('Độc giả kiểm thử');
  expect(await sidebar(page)).toEqual(['Hồ sơ của tôi', 'Bài đã lưu', 'Xem trang tin']);

  // validation, then a real rename that the top bar shows at once
  await page.locator('#DisplayName').fill('A');
  await page.locator('form[action$="ho-so"]').evaluate((f) => { (f as HTMLFormElement).noValidate = true; }); // test the server rule, not the browser hint
  await page.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await expect(page.locator('.app-alert--error')).toBeVisible();
  await page.locator('#DisplayName').fill('Tên mới đã đổi');
  await page.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await expect(page.locator('.app-alert--success')).toContainText('Đã cập nhật tên hiển thị');
  await expect(page.locator('.app-user__name')).toHaveText('Tên mới đã đổi');

  await page.goto('/tai-khoan/bai-da-luu');
  await expect(page.getByText('Bạn chưa lưu bài viết nào')).toBeVisible();
  for (const path of ['/cms', '/cms/CmsOverview', '/cms/CmsUsers']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: /403/ })).toBeVisible();
  }
  expect(problems.filter((p) => !p.includes('403'))).toEqual([]); // the 403 pages log a failed resource only
});

test('real author: cannot open or review another author\'s article; real editor cannot reach admin pages', async ({ browser }) => {
  const title = `Bài của người khác ${Date.now()}`;
  const editorCtx = await browser.newContext();
  const editor = await editorCtx.newPage();
  await realLogin(editor, REAL.editor);
  // the editor writes a draft (editors may write); the author must not be able to open it
  await editor.goto('/cms/CmsArticles/Create');
  await editor.locator('#Title').fill(title);
  await editor.locator('#CategoryId').selectOption({ index: 1 });
  await editor.locator('#Summary').fill('Tóm tắt kiểm thử quyền.');
  await editor.locator('#Body').fill('<p>Nội dung bài viết kiểm thử quyền truy cập, đủ dài để qua bước kiểm tra độ dài tối thiểu của hệ thống.</p>');
  await editor.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await editor.waitForURL(/\/CmsArticles\/Edit\/(\d+)/);
  const id = editor.url().match(/Edit\/(\d+)/)![1];
  for (const path of ['/cms/CmsOverview', '/cms/CmsUsers', '/cms/CmsCategories', '/cms/CmsAudit']) {
    await editor.goto(path);
    await expect(editor.getByRole('heading', { name: /403/ })).toBeVisible();
  }
  await editorCtx.close();

  const authorCtx = await browser.newContext();
  const author = await authorCtx.newPage();
  await realLogin(author, REAL.author);
  await author.goto(`/cms/CmsArticles/Edit/${id}`);
  await expect(author.getByRole('heading', { name: /403/ })).toBeVisible();
  await author.goto('/cms?q=' + encodeURIComponent(title));
  await expect(author.locator('tr', { hasText: title })).toHaveCount(0);
  await authorCtx.close();
});

test('admin: changes another user\'s role through the form; own row is locked', async ({ browser, page }) => {
  const email = `promote-${Date.now()}@example.test`;
  const reg = await browser.newContext();
  const regPage = await reg.newPage();
  await regPage.goto('/tai-khoan/dang-ky');
  await regPage.locator('input[name="DisplayName"]').fill('Người sẽ được nâng quyền');
  await regPage.locator('input[name="Email"]').fill(email);
  await regPage.locator('input[name="Password"]').fill('TestPassword123!');
  await regPage.locator('input[name="ConfirmPassword"]').fill('TestPassword123!');
  await regPage.locator('.reader-auth__submit').click();
  await regPage.waitForURL((u) => !u.pathname.includes('dang-ky'));

  await realLogin(page, REAL.admin);
  await page.goto('/cms/CmsUsers?q=' + encodeURIComponent(email));
  const row = page.locator('tbody tr', { hasText: email });
  await row.locator('select').selectOption('Author');
  await row.getByRole('button', { name: 'Lưu' }).click();
  await page.locator('#app-confirm [data-confirm-accept]').click();
  await expect(page.locator('.app-alert--success')).toContainText('Tác giả');

  // the promoted user's already-open session is invalidated at once (security stamp), and a fresh sign-in has the new role
  await regPage.goto('/cms');
  await expect(regPage).toHaveURL(/dang-nhap/);
  await realLogin(regPage, email, 'TestPassword123!');
  await regPage.goto('/cms');
  await expect(regPage.getByRole('heading', { name: 'Bài viết của tôi', level: 1 })).toBeVisible();
  await reg.close();

  await page.goto('/cms/CmsUsers?q=admin%40vnews.local');
  await expect(page.locator('tbody tr', { hasText: 'admin@vnews.local' }).getByText('Không thể đổi vai trò của chính mình')).toBeVisible();
});

test('the floating demo badge never covers a control on any role page (scrolled to the bottom, every viewport)', async ({ page }) => {
  const viewports = [{ width: 1440, height: 900 }, { width: 1536, height: 864 }, { width: 768, height: 1024 }, { width: 390, height: 844 }];
  for (const role of ['author', 'editor', 'admin', 'reader'] as Role[]) {
    await page.context().clearCookies();
    await demoLogin(page, role);
    for (const vp of viewports) {
      await page.setViewportSize(vp);
      for (const path of PAGES[role]) {
        await page.goto(path);
        await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
        const hits = await page.evaluate(() => {
          const b = document.querySelector('.demo-badge')!.getBoundingClientRect();
          const bad: string[] = [];
          document.querySelectorAll('.app-main a, .app-main button, .app-main input, .app-main select, .app-main textarea').forEach((e) => {
            const r = e.getBoundingClientRect();
            if (r.width === 0 || r.height === 0) return;
            const overlap = !(r.right <= b.left || r.left >= b.right || r.bottom <= b.top || r.top >= b.bottom);
            if (overlap) bad.push((e.textContent || (e as HTMLInputElement).name || e.tagName).trim().slice(0, 30));
          });
          return bad;
        });
        expect(hits, `${role} ${path} @${vp.width}`).toEqual([]);
      }
    }
  }
});
