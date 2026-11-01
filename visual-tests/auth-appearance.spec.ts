import { test, expect } from '@playwright/test';
import { badge, demoLogin, horizontalOverflow } from './support/auth';

// Sign-in / register / CMS look consistent: one logo, one UI font, phone layout, and the demo badge never covering a control.

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
  expect(await horizontalOverflow(page)).toBeLessThanOrEqual(1);
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
  expect(await horizontalOverflow(page)).toBeLessThanOrEqual(1);

  await page.keyboard.press('Escape');
  await demoLogin(page, 'reader');
  await expect(badge(page)).toBeVisible();
  const b = (await badge(page).boundingBox())!;
  expect(b.x).toBeGreaterThanOrEqual(0);
  expect(b.x + b.width).toBeLessThanOrEqual(391);
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
