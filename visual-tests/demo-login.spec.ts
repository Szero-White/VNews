import { test, expect, Page, Browser } from '@playwright/test';

// One-click demo sign-in + unified sign-in, exercised through the real UI. The Playwright server is Development,
// uses the isolated VNewsE2E database, and is the only server started with DemoAuth__Enabled=true.

const STAFF = { email: 'editor@vnews.local', password: 'Demo@12345' };
const overflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
const badge = (page: Page) => page.locator('.demo-badge');

async function demoLogin(page: Page, role: 'reader' | 'author' | 'editor' | 'admin', from = '/tai-khoan/dang-nhap') {
  await page.goto(from);
  await page.locator(`.demo-login--page [data-demo-role="${role}"]`).click();
}
async function publishedArticleSlug(page: Page): Promise<string> {
  return (await (await page.request.get('/api/v1/articles?pageSize=1')).json()).items[0].slug;
}
async function signOutFromBadge(page: Page) {
  await badge(page).getByRole('button', { name: 'Thoát demo' }).click();
}

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

  const slug = await publishedArticleSlug(page);
  const bookmarksLoaded = page.waitForResponse((r) => r.url().includes('/api/me/bookmarks') && r.request().method() === 'GET');
  await page.goto(`/tin/${slug}`);
  await bookmarksLoaded; // the button reflects the account's saved list only after this request
  await page.waitForTimeout(200);
  const save = page.locator('.article-tools [data-action="save"]');
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
  await page.locator('#Body').fill('<p>Nội dung bản nháp của tác giả demo.</p>');
  await page.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await expect(page.locator('.app-alert--success')).toContainText('Đã tạo bản nháp');

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
  await author.locator('#Body').fill('<p>Nội dung chờ duyệt của tác giả demo.</p>');
  await author.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await author.goto('/cms');
  await author.locator('tr', { hasText: title }).getByRole('button', { name: 'Gửi duyệt' }).click();
  await author.locator('#app-confirm [data-confirm-accept]').click();
  await expect(author.locator('.app-alert--success')).toContainText('Đã gửi duyệt');
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

test('sign-in dialog: wrong password shows an inline error and stays open; right password signs in without leaving the page', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/chuyen-muc/cong-nghe');
  await page.locator('.masthead__account-link').first().click();
  const dialog = page.locator('#reader-login-dialog');
  await dialog.locator('input[name="Email"]').fill(STAFF.email);
  await dialog.locator('input[name="Password"]').fill('sai-mat-khau');
  await dialog.locator('.reader-login-dialog__submit').click();

  const error = dialog.locator('[data-reader-login-error]');
  await expect(error).toBeVisible();
  await expect(error).toContainText('Email hoặc mật khẩu không đúng');
  await expect(dialog).toBeVisible();
  await expect(page).toHaveURL(/\/chuyen-muc\/cong-nghe/);
  await expect(dialog.locator('input[name="Password"]')).toBeFocused();
  await expect(dialog.locator('input[name="Password"]')).toHaveValue('');

  await dialog.locator('input[name="Password"]').fill(STAFF.password);
  await dialog.locator('.reader-login-dialog__submit').click();
  // From the dialog the reader goes back to the page they were reading, now signed in (the header changes).
  await expect(page.locator('.masthead__account-label')).toHaveText('Tài khoản');
  await expect(page).toHaveURL(/\/chuyen-muc\/cong-nghe$/);
  await expect(page.locator('#reader-login-dialog')).toHaveCount(0);
  // A newsroom account reaches the CMS from the header (the first CMS request after start-up can be slow).
  await page.locator('.masthead__utility-link', { hasText: 'Tòa soạn' }).first().click();
  await expect(page).toHaveURL(/\/cms/, { timeout: 20000 });
});

test('shared sign-in page: legacy CMS address forwards, wrong password errors, staff land in their own area', async ({ page }) => {
  await page.goto('/cms/dang-nhap');
  await expect(page).toHaveURL(/\/tai-khoan\/dang-nhap\?returnUrl=%2Fcms/);

  await page.locator('input[name="Email"]').fill(STAFF.email);
  await page.locator('input[name="Password"]').fill('sai-mat-khau');
  await page.locator('.reader-auth__submit').click();
  await expect(page.locator('.reader-auth__errors').first()).toContainText('Email hoặc mật khẩu không đúng');

  await page.locator('input[name="Password"]').fill(STAFF.password);
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL(/\/cms/);
  await expect(page.locator('.app-topbar')).toBeVisible();
});

test('an outside return URL never leaves the site after signing in', async ({ page }) => {
  await page.goto('/tai-khoan/dang-nhap?returnUrl=' + encodeURIComponent('https://evil.example/phish'));
  await page.locator('input[name="Email"]').fill(STAFF.email);
  await page.locator('input[name="Password"]').fill(STAFF.password);
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL(/localhost:5399\/cms/);
  expect(new URL(page.url()).hostname).toBe('localhost');
});

test('one logo everywhere: header, dialog, sign-in page and CMS share the same mark', async ({ page }) => {
  const mark = (selector: string) => page.locator(selector).first().evaluate((e) => {
    const c = getComputedStyle(e);
    const r = e.getBoundingClientRect();
    return { text: e.textContent!.trim(), bg: c.backgroundColor, color: c.color, ratio: Math.round((r.height / r.width) * 100) / 100, family: c.fontFamily.split(',')[0] };
  });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  const header = await mark('.masthead__brand .vn-logo__mark');
  await page.locator('.masthead__account-link').first().click();
  const dialog = await mark('#reader-login-dialog .vn-logo__mark');
  await page.goto('/tai-khoan/dang-nhap');
  const auth = await mark('.reader-auth__logo .vn-logo__mark');
  await demoLogin(page, 'author');
  const cms = await mark('.app-sidebar .vn-logo__mark');

  for (const other of [dialog, auth, cms]) {
    expect(other.text).toBe('V');
    expect(other.bg).toBe(header.bg);
    expect(other.color).toBe(header.color);
    expect(other.ratio).toBe(header.ratio);
    expect(other.family).toBe(header.family);
  }
  await expect(page.locator('.app-sidebar .vn-logo__name')).toHaveText('NEWS');
});

test('typography: sign-in heading and dialog title use the same UI font as the forms; Vietnamese marks render attached', async ({ page }) => {
  await page.goto('/tai-khoan/dang-nhap');
  const h1 = page.locator('#login-title');
  await expect(h1).toHaveText('Chào mừng trở lại');
  expect((await h1.evaluate((e) => getComputedStyle(e).fontFamily)).split(',')[0].replace(/"/g, '')).toBe('Arial');
  expect(await h1.evaluate((e) => [getComputedStyle(e).fontSize, getComputedStyle(e).lineHeight, getComputedStyle(e).fontWeight])).toEqual(['28px', '36px', '700']);
  await page.goto('/');
  await page.locator('.masthead__account-link').first().click();
  const title = page.locator('#reader-login-dialog-title');
  expect((await title.evaluate((e) => getComputedStyle(e).fontFamily)).split(',')[0].replace(/"/g, '')).toBe('Arial');
});

test('mobile 390: sign-in page, demo cards and dialog fit the screen', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/tai-khoan/dang-nhap');
  expect(await overflow(page)).toBeLessThanOrEqual(1);
  const boxes = await page.locator('.demo-login--page .demo-card').evaluateAll((els) => els.map((e) => { const b = e.getBoundingClientRect(); return { x: b.x, w: b.width }; }));
  expect(new Set(boxes.map((b) => Math.round(b.x))).size).toBe(1); // one column
  for (const b of boxes) expect(b.x + b.w).toBeLessThanOrEqual(391);
  for (const button of await page.locator('.demo-card__button').all()) expect((await button.boundingBox())!.height).toBeGreaterThanOrEqual(40);

  await page.goto('/');
  await page.locator('.masthead__account-link').first().click();
  const dialog = (await page.locator('#reader-login-dialog').boundingBox())!;
  expect(dialog.x).toBeGreaterThanOrEqual(0);
  expect(dialog.x + dialog.width).toBeLessThanOrEqual(391);
  expect(dialog.y + dialog.height).toBeLessThanOrEqual(845);
  expect(await overflow(page)).toBeLessThanOrEqual(1);

  await page.keyboard.press('Escape');
  await demoLogin(page, 'reader');
  await expect(badge(page)).toBeVisible();
  const b = (await badge(page).boundingBox())!;
  expect(b.x).toBeGreaterThanOrEqual(0);
  expect(b.x + b.width).toBeLessThanOrEqual(391);
});

test('CMS pages and the sign-in page carry no site menu', async ({ page }) => {
  await demoLogin(page, 'admin');
  await expect(page.locator('#mega-menu')).toHaveCount(0);
  await expect(page.locator('#primary-nav')).toHaveCount(0);
});

test('register: a new reader signs up through the form, signs out, then signs in normally', async ({ page }) => {
  const email = `doc-gia-${Date.now()}@example.test`;
  await page.goto('/tai-khoan/dang-ky');
  await page.locator('input[name="DisplayName"]').fill('Độc giả thử nghiệm');
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill('MatKhau-Thu-123');
  await page.locator('input[name="ConfirmPassword"]').fill('MatKhau-Thu-123');
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL('/');
  await expect(page.locator('.masthead__account-label')).toHaveText('Tài khoản');
  await expect(badge(page)).toHaveCount(0); // a real account, not demo

  await page.goto('/tai-khoan/ho-so');
  await expect(page.locator('#profile-title')).toContainText('Độc giả thử nghiệm');
  await page.locator('main').getByRole('button', { name: 'Đăng xuất' }).click();
  await page.goto('/cms');
  await expect(page).toHaveURL(/\/tai-khoan\/dang-nhap/);

  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill('MatKhau-Thu-123');
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL('/'); // reader: returnUrl /cms is not honoured
  await page.goto('/cms/CmsUsers');
  await expect(page.getByRole('heading', { name: /403/ })).toBeVisible();
});

test('register shows validation errors in the same style and does not sign in', async ({ page }) => {
  await page.goto('/tai-khoan/dang-ky');
  await page.locator('input[name="DisplayName"]').fill('Lỗi');
  await page.locator('input[name="Email"]').fill('doc-gia-trung@example.test');
  await page.locator('input[name="Password"]').fill('MatKhau-Thu-123');
  await page.locator('input[name="ConfirmPassword"]').fill('KhongKhop-123');
  await page.locator('.reader-auth__submit').click();
  await expect(page).toHaveURL(/dang-ky/);
  // The client-side message appears next to the confirm-password field, in the shared error style.
  const message = page.locator('.reader-auth__errors', { hasText: /\S/ }).first();
  await expect(message).toBeVisible();
  await expect(message).toContainText('không khớp');
  await expect(page.locator('.masthead__account-label')).toHaveCount(0);
});

test('sign-in and register look like one product: same typography, fields, button, card and logo', async ({ page }) => {
  const probe = async (url: string) => {
    await page.goto(url);
    return page.evaluate(() => {
      const css = (selector: string, props: string[]) => {
        const e = document.querySelector(selector) as HTMLElement | null;
        if (!e) return null;
        const c = getComputedStyle(e);
        return Object.fromEntries(props.map((p) => [p, c.getPropertyValue(p)]));
      };
      return {
        h1: css('.reader-auth__card h1', ['font-family', 'font-size', 'line-height', 'font-weight', 'color']),
        kicker: css('.reader-auth__kicker', ['font-family', 'font-size', 'color']),
        lead: css('.reader-auth__lead', ['font-family', 'font-size', 'color']),
        label: css('.reader-auth__field label', ['font-family', 'font-size', 'font-weight']),
        input: css('.reader-auth__field input', ['font-family', 'font-size', 'height', 'border-radius', 'border-top-color']),
        button: css('.reader-auth__submit', ['font-family', 'font-size', 'height', 'background-color', 'border-radius', 'color']),
        card: css('.reader-auth__card', ['max-width', 'border-radius', 'background-color']),
        wrap: Math.round((document.querySelector('.reader-auth__form-wrap') as HTMLElement).getBoundingClientRect().width),
        logo: css('.reader-auth__logo .vn-logo__mark', ['background-color', 'width', 'height']),
        logoName: css('.reader-auth__logo .vn-logo__name', ['color']),
      };
    });
  };
  const login = await probe('/tai-khoan/dang-nhap');
  const register = await probe('/tai-khoan/dang-ky');
  // The sign-in page has no kicker label (removed on request); the registration page keeps its own, which must use the UI font.
  expect(login.kicker).toBeNull();
  expect(register.kicker!['font-family']).toContain('Arial');
  expect({ ...register, kicker: null }).toEqual({ ...login, kicker: null });
  expect(login.h1!['font-family']).toContain('Arial');
  expect(login.input!['font-family']).toContain('Arial');
  expect(login.button!['background-color']).toBe('rgb(166, 27, 70)');
  expect(login.logo!['background-color']).toBe('rgb(166, 27, 70)');
  expect(login.logoName!.color).toBe('rgb(102, 102, 102)');
});

test('the CMS uses the same typography as the site and its logo keeps the red mark', async ({ page }) => {
  await demoLogin(page, 'author');
  await page.goto('/cms/CmsArticles/Create');
  const info = await page.evaluate(() => {
    const topbar = document.querySelector('.app-sidebar')!.getBoundingClientRect();
    return {
      bodyFamily: getComputedStyle(document.body).fontFamily,
      h1Family: getComputedStyle(document.querySelector('.app-page-title')!).fontFamily,
      topbarX: topbar.x, topbarW: topbar.width, vw: document.documentElement.clientWidth,
      mark: getComputedStyle(document.querySelector('.app-sidebar .vn-logo__mark')!).backgroundColor,
      markW: document.querySelector('.app-sidebar .vn-logo__mark')!.getBoundingClientRect().width,
    };
  });
  expect(info.bodyFamily.startsWith('Arial')).toBe(true);
  expect(info.h1Family).toContain('Arial');
  expect(info.topbarX).toBe(0);
  expect(info.topbarW).toBeLessThan(info.vw);
  expect(info.mark).toBe('rgb(166, 27, 70)');
  expect(info.markW).toBeGreaterThanOrEqual(22); // 24px logo: the mark is about 1.08em of its 0.84em text size, plus skew
});

test('the demo badge never covers the last button of a page', async ({ page }) => {
  await demoLogin(page, 'author');
  await page.goto('/cms/CmsArticles/Create');
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  const submit = (await page.getByRole('button', { name: 'Tạo bản nháp' }).boundingBox())!;
  const b = (await badge(page).boundingBox())!;
  const overlaps = !(b.x + b.width < submit.x || submit.x + submit.width < b.x || b.y + b.height < submit.y || submit.y + submit.height < b.y);
  expect(overlaps).toBe(false);
});
