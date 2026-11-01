import { test, expect, Page } from '@playwright/test';

// Authentication UI design system: one UI font, radii 12 (modal) / 8 (controls, cards), spacing scale 4/8/12/16/24/32,
// aligned fields, four identical demo cards, a dialog that opens at the top and scrolls only its content.
// Real measurements (computed style + bounding boxes), not element counts.

const VIEWPORTS = [{ width: 1440, height: 900 }, { width: 1536, height: 864 }, { width: 390, height: 844 }];

async function openDialog(page: Page, url = '/chuyen-muc/cong-nghe') {
  await page.goto(url);
  await page.locator('.masthead__account-link').first().click();
  await expect(page.locator('#reader-login-dialog')).toBeVisible();
}
const style = (page: Page, selector: string, props: string[]) =>
  page.locator(selector).first().evaluate((e, p) => { const c = getComputedStyle(e); return Object.fromEntries(p.map((k) => [k, c.getPropertyValue(k)])); }, props);
const box = (page: Page, selector: string) => page.locator(selector).first().evaluate((e) => { const b = e.getBoundingClientRect(); return { x: b.x, y: b.y, w: b.width, h: b.height, r: b.right, b: b.bottom }; });
const near = (a: number, b: number, tol: number, label: string) => expect(Math.abs(a - b), `${label}: ${a} vs ${b}`).toBeLessThanOrEqual(tol);
const font = (s: Record<string, string>) => [s['font-family'].split(',')[0].replace(/"/g, '').trim(), s['font-size'], s['font-weight'], s['line-height']];

for (const vp of VIEWPORTS) {
  test(`dialog @${vp.width}x${vp.height}: opens at the top with logo, title and close in view; fits the screen; no horizontal scroll`, async ({ page }) => {
    await page.setViewportSize(vp);
    await openDialog(page);

    const dialog = await box(page, '#reader-login-dialog');
    expect(dialog.y).toBeGreaterThanOrEqual(0);
    expect(dialog.b).toBeLessThanOrEqual(vp.height);
    expect(dialog.x).toBeGreaterThanOrEqual(0);
    expect(dialog.r).toBeLessThanOrEqual(vp.width);
    expect(dialog.h).toBeLessThanOrEqual(vp.height - 32 + 1);

    // Fresh opening: scrolled to the top, header parts and the title fully visible inside the dialog.
    expect(await page.locator('[data-reader-login-scroll]').evaluate((e) => e.scrollTop)).toBe(0);
    for (const selector of ['.reader-login-dialog__brand', '.reader-login-dialog__close', '#reader-login-dialog-title']) {
      const b = await box(page, `#reader-login-dialog ${selector}`);
      expect(b.y, selector).toBeGreaterThanOrEqual(dialog.y);
      expect(b.b, selector).toBeLessThanOrEqual(dialog.b);
      expect(b.x, selector).toBeGreaterThanOrEqual(dialog.x);
      expect(b.r, selector).toBeLessThanOrEqual(dialog.r);
    }
    const title = await box(page, '#reader-login-dialog-title');
    const header = await box(page, '.reader-login-dialog__header');
    expect(title.y).toBeGreaterThanOrEqual(header.b); // the title is not hidden under the header
    expect(await page.locator('#reader-login-dialog').evaluate((e) => e.scrollHeight - e.clientHeight)).toBeLessThanOrEqual(1); // the dialog itself never scrolls
    expect(await page.locator('[data-reader-login-scroll]').evaluate((e) => e.scrollWidth - e.clientWidth)).toBeLessThanOrEqual(0); // no horizontal scrollbar
  });

  test(`dialog @${vp.width}x${vp.height}: one UI font, hierarchy, radii and aligned equal-width controls`, async ({ page }) => {
    await page.setViewportSize(vp);
    await openDialog(page);

    expect(font(await style(page, '#reader-login-dialog-title', ['font-family', 'font-size', 'font-weight', 'line-height']))).toEqual(['Arial', '22px', '700', '28px']);
    expect(font(await style(page, '#reader-login-dialog-description', ['font-family', 'font-size', 'font-weight', 'line-height']))).toEqual(['Arial', '14px', '400', '20px']);
    expect(font(await style(page, '.reader-login-dialog__field label', ['font-family', 'font-size', 'font-weight', 'line-height']))).toEqual(['Arial', '14px', '700', '20px']);
    expect(font(await style(page, '#reader-login-email', ['font-family', 'font-size', 'font-weight', 'line-height']))).toEqual(['Arial', '14px', '400', '20px']);
    expect(font(await style(page, '.reader-login-dialog__submit', ['font-family', 'font-size', 'font-weight', 'line-height']))).toEqual(['Arial', '15px', '700', '20px']);

    expect((await style(page, '#reader-login-dialog', ['border-radius']))['border-radius']).toBe('12px');
    for (const selector of ['#reader-login-email', '#reader-login-password', '.reader-login-dialog__submit', '.reader-login-dialog__error']) {
      expect((await style(page, `#reader-login-dialog ${selector}`, ['border-radius']))['border-radius'], selector).toBe('8px');
    }
    // all four corners identical (no longhand override), checked per corner
    for (const selector of ['#reader-login-dialog', '#reader-login-email', '.reader-login-dialog__submit']) {
      const corners = await style(page, selector, ['border-top-left-radius', 'border-top-right-radius', 'border-bottom-left-radius', 'border-bottom-right-radius']);
      expect(new Set(Object.values(corners)).size, selector).toBe(1);
    }

    // Email, password and the button: same x, same width, 44px high; 16px between fields, label 8px above its input.
    const email = await box(page, '#reader-login-email');
    const password = await box(page, '#reader-login-password');
    const submit = await box(page, '.reader-login-dialog__submit');
    near(email.x, password.x, 0.5, 'input x');
    near(email.x, submit.x, 0.5, 'button x');
    near(email.w, password.w, 0.5, 'input width');
    near(email.w, submit.w, 0.5, 'button width');
    expect([email.h, password.h, submit.h]).toEqual([44, 44, 44]);
    const label = await box(page, 'label[for="reader-login-email"]');
    near(label.x, email.x, 0.5, 'label aligned with its input');
    near(email.y - label.b, 8, 0.5, 'label to input gap');
    const passwordLabel = await box(page, 'label[for="reader-login-password"]');
    near(passwordLabel.y - email.b, 16, 0.5, 'field to field gap');

    // Equal padding left/right inside the content area.
    const content = await box(page, '[data-reader-login-scroll]');
    const padding = vp.width <= 480 ? 16 : 24;
    near(email.x - content.x, padding, 0.5, 'left padding');
    near(content.r - email.r, padding, 1.5, 'right padding (a scrollbar may take a few px)');
  });

  test(`dialog @${vp.width}x${vp.height}: four demo cards are identical in size, spacing, border and button`, async ({ page }) => {
    await page.setViewportSize(vp);
    await openDialog(page);
    await page.locator('#reader-login-dialog .demo-login').scrollIntoViewIfNeeded();

    const cards = await page.locator('#reader-login-dialog .demo-card').evaluateAll((els) => els.map((e) => {
      const b = e.getBoundingClientRect(); const c = getComputedStyle(e);
      const t = e.querySelector('.demo-card__title')!.getBoundingClientRect(); const btn = e.querySelector('.demo-card__button')!.getBoundingClientRect();
      const bc = getComputedStyle(e.querySelector('.demo-card__button')!);
      return { x: b.x, y: b.y, w: b.width, h: b.height, radius: c.borderRadius, border: c.borderTopWidth + c.borderLeftWidth + c.borderRightWidth + c.borderBottomWidth, padding: [c.paddingTop, c.paddingRight, c.paddingBottom, c.paddingLeft].join(),
        titleDy: t.y - b.y, titleH: t.height, btnW: btn.width, btnH: btn.height, btnDy: btn.y - b.y, btnDx: btn.x - b.x, btnLines: Math.round(btn.height / 20), btnRadius: bc.borderRadius, btnOverflow: (e.querySelector('.demo-card__button') as HTMLElement).scrollWidth - (e.querySelector('.demo-card__button') as HTMLElement).clientWidth };
    }));
    expect(cards).toHaveLength(4);
    for (const c of cards) {
      near(c.w, cards[0].w, 0.5, 'card width'); near(c.h, cards[0].h, 0.5, 'card height');
      expect(c.radius).toBe('8px'); expect(c.border).toBe(cards[0].border); expect(c.padding).toBe(cards[0].padding);
      near(c.titleDy, cards[0].titleDy, 0.5, 'title position in card'); expect(c.titleH).toBe(cards[0].titleH);
      near(c.btnW, cards[0].btnW, 0.5, 'button width'); expect(c.btnH).toBe(40); near(c.btnDy, cards[0].btnDy, 0.5, 'button position in card'); near(c.btnDx, cards[0].btnDx, 0.5, 'button x in card');
      expect(c.btnRadius).toBe('8px'); expect(c.btnOverflow).toBeLessThanOrEqual(0); // text never wraps or overflows
    }
    // 2 x 2 grid with a 12px gap both ways.
    near(cards[1].x - (cards[0].x + cards[0].w), 12, 0.5, 'column gap'); near(cards[0].y, cards[1].y, 0.5, 'row 1 aligned');
    near(cards[2].y - (cards[0].y + cards[0].h), 12, 0.5, 'row gap'); near(cards[2].x, cards[0].x, 0.5, 'column 1 aligned'); near(cards[3].x, cards[1].x, 0.5, 'column 2 aligned');

    const labels = await page.locator('#reader-login-dialog .demo-card__button').evaluateAll((els) => els.map((e) => [e.textContent!.trim(), e.getAttribute('aria-label')]));
    expect(labels).toEqual([['Vào demo', 'Vào demo Độc giả'], ['Vào demo', 'Vào demo Tác giả'], ['Vào demo', 'Vào demo Biên tập viên'], ['Vào demo', 'Vào demo Quản trị viên']]);
  });
}

test('dialog scrolls only its content: header and close stay put, reopening starts at the top, Escape and focus still work', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 600 });
  await openDialog(page);
  const content = page.locator('[data-reader-login-scroll]');
  expect(await content.evaluate((e) => e.scrollHeight > e.clientHeight)).toBe(true); // short screen: content scrolls
  const closeBefore = await box(page, '.reader-login-dialog__close');

  await content.evaluate((e) => { e.scrollTop = e.scrollHeight; });
  const closeAfter = await box(page, '.reader-login-dialog__close');
  expect([closeAfter.x, closeAfter.y]).toEqual([closeBefore.x, closeBefore.y]); // close does not move while scrolling
  await expect(page.locator('#reader-login-dialog .demo-card__button').last()).toBeInViewport();
  const lastCard = await box(page, '#reader-login-dialog .demo-card--admin');
  const dialog = await box(page, '#reader-login-dialog');
  expect(lastCard.b).toBeLessThanOrEqual(dialog.b); // nothing cut off at the bottom

  await page.keyboard.press('Escape');
  await expect(page.locator('#reader-login-dialog')).not.toBeVisible();
  await page.locator('.masthead__account-link').first().click();
  expect(await content.evaluate((e) => e.scrollTop)).toBe(0); // reopened at the top
  await expect(page.locator('#reader-login-dialog input[name="Email"]')).toBeFocused();
  await page.locator('.reader-login-dialog__close').click();
  await expect(page.locator('#reader-login-dialog')).not.toBeVisible();
});

test('dialog keeps focus inside while open (Tab cycles through its own controls)', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await openDialog(page);
  const seen = new Set<string>();
  for (let i = 0; i < 24; i++) {
    await page.keyboard.press('Tab');
    expect(await page.evaluate(() => !!document.activeElement?.closest('#reader-login-dialog'))).toBe(true);
    seen.add(await page.evaluate(() => (document.activeElement as HTMLElement).getAttribute('aria-label') || (document.activeElement as HTMLElement).getAttribute('name') || (document.activeElement as HTMLElement).tagName));
  }
  expect(seen.has('Vào demo Quản trị viên')).toBe(true);
  expect(seen.has('Đóng đăng nhập')).toBe(true);
});

test('focus ring is the same on dialog and page controls', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await openDialog(page);
  await page.locator('#reader-login-email').focus();
  await page.keyboard.press('Tab'); await page.keyboard.press('Shift+Tab');
  const dialogRing = await style(page, '#reader-login-email', ['outline-width', 'outline-style', 'outline-color', 'outline-offset']);
  await page.keyboard.press('Escape');
  await page.goto('/tai-khoan/dang-nhap');
  await page.locator('input[name="Email"]').focus();
  await page.keyboard.press('Tab'); await page.keyboard.press('Shift+Tab');
  const pageRing = await style(page, 'input[name="Email"]', ['outline-width', 'outline-style', 'outline-color', 'outline-offset']);
  expect(pageRing).toEqual(dialogRing);
  expect(dialogRing['outline-width']).toBe('2px');
  expect(dialogRing['outline-offset']).toBe('2px');
});

for (const vp of VIEWPORTS) {
  test(`pages @${vp.width}x${vp.height}: sign-in and register share tokens (radii, fonts, spacing) with the dialog; demo cards equal`, async ({ page }) => {
    await page.setViewportSize(vp);
    const probe = async (url: string) => {
      await page.goto(url);
      return {
        card: (await style(page, '.reader-auth__card', ['border-radius', 'padding-left', 'padding-right']))!,
        h1: font(await style(page, '.reader-auth__card h1', ['font-family', 'font-size', 'font-weight', 'line-height'])),
        input: { ...(await style(page, '.reader-auth__field input', ['border-radius', 'height'])), font: font(await style(page, '.reader-auth__field input', ['font-family', 'font-size', 'font-weight', 'line-height'])) },
        button: { ...(await style(page, '.reader-auth__submit', ['border-radius', 'height'])), font: font(await style(page, '.reader-auth__submit', ['font-family', 'font-size', 'font-weight', 'line-height'])) },
      };
    };
    const login = await probe('/tai-khoan/dang-nhap');
    const register = await probe('/tai-khoan/dang-ky');
    expect(register).toEqual(login);
    expect(login.card['border-radius']).toBe('12px');
    expect(login.input['border-radius']).toBe('8px');
    expect(login.input.height).toBe('44px');
    expect(login.button['border-radius']).toBe('8px');
    expect(login.h1).toEqual(['Arial', '28px', '700', '36px']);
    expect(login.input.font).toEqual(['Arial', '14px', '400', '20px']);

    await page.goto('/tai-khoan/dang-nhap');
    const cards = await page.locator('.demo-login--page .demo-card').evaluateAll((els) => els.map((e) => { const b = e.getBoundingClientRect(); const bt = e.querySelector('.demo-card__button')!.getBoundingClientRect(); return { w: b.width, h: b.height, bw: bt.width, bh: bt.height, radius: getComputedStyle(e).borderRadius }; }));
    expect(cards).toHaveLength(4);
    for (const c of cards) {
      near(c.w, cards[0].w, 0.5, 'card width'); near(c.bw, cards[0].bw, 0.5, 'button width');
      expect(c.bh).toBe(40); expect(c.radius).toBe('8px');
    }
    if (vp.width > 520) for (const c of cards) near(c.h, cards[0].h, 0.5, 'card height (2 x 2)');
    expect((await style(page, '.demo-login--page', ['border-radius']))['border-radius']).toBe('12px');
    expect(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(1);
  });
}
