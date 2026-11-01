import { test, expect, Page } from '@playwright/test';

// "Tất cả chuyên mục" desktop menu. Reference: the all-sections menu of vnexpress.net (panel under the navigation,
// sections in columns, utilities column, close button, dimmed page). Real interactions, not just DOM counts.

const menu = (page: Page) => page.locator('#mega-menu');
const toggle = (page: Page) => page.locator('#mega-menu-toggle');
const overflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
async function categories(page: Page): Promise<{ name: string; slug: string }[]> {
  return (await page.request.get('/api/v1/categories')).json();
}
async function openMenu(page: Page, url = '/chuyen-muc/cong-nghe') {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(url);
  await toggle(page).click();
  await expect(menu(page)).toBeVisible();
}
const focusInsideMenu = (page: Page) => page.evaluate(() => document.getElementById('mega-menu')!.contains(document.activeElement));

test('closed by default; the toggle is labelled and exposes its state', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
  await expect(toggle(page)).toBeVisible();
  await expect(toggle(page)).toContainText('Tất cả chuyên mục');
  await expect(toggle(page)).toHaveAttribute('aria-expanded', 'false');
  await expect(toggle(page)).toHaveAttribute('aria-controls', 'mega-menu');
  await expect(menu(page)).toBeHidden();
  await expect(page.locator('#mega-menu-overlay')).toBeHidden();
});

test('opens under the navigation with a dimmed page, every real category in columns, and a utilities column', async ({ page }) => {
  const all = await categories(page);
  await openMenu(page);

  await expect(toggle(page)).toHaveAttribute('aria-expanded', 'true');
  await expect(page.locator('#mega-menu-title')).toHaveText('Tất cả chuyên mục');
  await expect(menu(page)).toHaveAttribute('role', 'dialog');
  await expect(menu(page)).toHaveAttribute('aria-modal', 'true');

  // Panel sits directly under the navigation and spans the viewport.
  const navBox = (await page.locator('#primary-nav').boundingBox())!;
  const menuBox = (await menu(page).boundingBox())!;
  expect(Math.abs(menuBox.y - (navBox.y + navBox.height))).toBeLessThanOrEqual(2);
  expect(menuBox.width).toBeGreaterThanOrEqual(1438);

  // Overlay: covers the viewport, dark, sits below the navigation layer.
  const overlay = await page.locator('#mega-menu-overlay').evaluate((e) => {
    const b = e.getBoundingClientRect();
    const c = getComputedStyle(e);
    return { x: b.x, y: b.y, w: b.width, h: b.height, bg: c.backgroundColor, z: Number(c.zIndex), navZ: Number(getComputedStyle(document.getElementById('primary-nav')!).zIndex) };
  });
  expect([overlay.x, overlay.y, overlay.w, overlay.h]).toEqual([0, 0, 1440, 900]);
  expect(overlay.bg).toMatch(/^rgba\(0, 0, 0, 0?\.5\d*\)$/);
  expect(overlay.z).toBeLessThan(overlay.navZ);

  // Categories: exactly the database's, in order, with real links, arranged in more than one column.
  const links = menu(page).locator('.mega-menu__list a');
  await expect(links).toHaveCount(all.length);
  expect(await links.allInnerTexts()).toEqual(all.map((c) => c.name));
  expect(await links.evaluateAll((els) => els.map((e) => e.getAttribute('href')))).toEqual(all.map((c) => `/chuyen-muc/${c.slug}`));
  const xs = await links.evaluateAll((els) => Array.from(new Set(els.map((e) => Math.round(e.getBoundingClientRect().x)))));
  expect(xs.length).toBeGreaterThanOrEqual(2);
  await expect(menu(page).locator('.mega-menu__list a[aria-current="page"]')).toHaveText('Công nghệ');

  // Utilities: only pages that exist, no placeholder links.
  const utility = await menu(page).locator('.mega-menu__utility a').evaluateAll((els) => els.map((e) => [e.textContent!.trim(), e.getAttribute('href')]));
  expect(utility.length).toBeGreaterThanOrEqual(6);
  for (const [, href] of utility) {
    expect(href).toMatch(/^\//);
    expect(href).not.toContain('#');
    expect((await page.request.get(href!)).status(), href!).toBe(200);
  }

  // The page behind is locked and cannot be reached.
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).overflow)).toBe('hidden');
  await expect(page.locator('.site-main')).toHaveAttribute('inert', '');
  expect(await focusInsideMenu(page)).toBe(true);
  expect(await overflow(page)).toBeLessThanOrEqual(1);
});

test('Escape, the close button, the toggle, a click on the dimmed page and a click outside all close it and restore focus', async ({ page }) => {
  for (const how of ['escape', 'button', 'toggle', 'overlay']) {
    await openMenu(page);
    if (how === 'escape') await page.keyboard.press('Escape');
    if (how === 'button') await page.locator('[data-mega-close]').click();
    if (how === 'toggle') await toggle(page).click();
    if (how === 'overlay') await page.mouse.click(720, 800); // dimmed page, well below the panel
    await expect(menu(page), how).toBeHidden();
    await expect(page.locator('#mega-menu-overlay'), how).toBeHidden();
    await expect(toggle(page), how).toHaveAttribute('aria-expanded', 'false');
    expect(await page.evaluate(() => getComputedStyle(document.documentElement).overflow), how).not.toBe('hidden');
    await expect(page.locator('.site-main'), how).not.toHaveAttribute('inert', '');
    if (how !== 'overlay') await expect(toggle(page), how).toBeFocused(); // focus returns to the opener
  }
});

test('keyboard: Enter opens, Tab and Shift+Tab stay inside the menu, links are reachable', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/tin-moi-nhat');
  await toggle(page).focus();
  await page.keyboard.press('Enter');
  await expect(menu(page)).toBeVisible();
  expect(await focusInsideMenu(page)).toBe(true);

  const count = await menu(page).locator('a[href], button').count();
  for (let i = 0; i < count + 3; i++) {
    await page.keyboard.press('Tab');
    expect(await focusInsideMenu(page), `Tab #${i + 1} stays inside`).toBe(true);
  }
  for (let i = 0; i < count + 3; i++) {
    await page.keyboard.press('Shift+Tab');
    expect(await focusInsideMenu(page), `Shift+Tab #${i + 1} stays inside`).toBe(true);
  }
  await page.keyboard.press('Escape');
  await expect(toggle(page)).toBeFocused();

  // Space also toggles (native button), and Enter on a category link navigates.
  await page.keyboard.press('Space');
  await expect(menu(page)).toBeVisible();
  await page.locator('.mega-menu__list a').nth(1).focus();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/\/chuyen-muc\/kinh-doanh/);
});

test('clicking a category navigates; the next page starts with the menu closed and the category highlighted', async ({ page }) => {
  const all = await categories(page);
  await openMenu(page, '/tin-moi-nhat');
  await menu(page).getByRole('link', { name: all[3].name, exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/chuyen-muc/${all[3].slug}$`));
  await expect(menu(page)).toBeHidden();
  await expect(page.locator('.category-nav__list a[aria-current="page"]')).toHaveText(all[3].name);
  await toggle(page).click();
  await expect(menu(page).locator('.mega-menu__list a[aria-current="page"]')).toHaveText(all[3].name);

  // Going back never restores an open menu.
  await page.goBack();
  await expect(menu(page)).toBeHidden();
});

test('every category link in the menu opens a real page', async ({ page }) => {
  const all = await categories(page);
  for (const c of all) {
    await openMenu(page);
    await menu(page).locator(`a[href="/chuyen-muc/${c.slug}"]`).click();
    await expect(page.locator('h1.category-header__title')).toHaveText(c.name);
  }
});

test('shrinking the window closes it (phones use the drawer); growing it again works; it never overflows', async ({ page }) => {
  await openMenu(page);
  await page.setViewportSize({ width: 600, height: 800 });
  await expect(menu(page)).toBeHidden();
  await expect(page.locator('#mega-menu-overlay')).toBeHidden();
  await expect.poll(() => page.evaluate(() => document.documentElement.classList.contains('mega-open'))).toBe(false); // the resize handler runs a frame later
  await expect(toggle(page)).toBeHidden();

  await page.setViewportSize({ width: 1100, height: 700 });
  await toggle(page).click();
  await expect(menu(page)).toBeVisible();
  expect(await overflow(page)).toBeLessThanOrEqual(1);
  const box = (await menu(page).boundingBox())!;
  expect(box.x + box.width).toBeLessThanOrEqual(1101);
});

test('short windows: the menu scrolls inside itself instead of running off screen', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 420 });
  await page.goto('/chuyen-muc/cong-nghe');
  await toggle(page).click();
  const box = (await menu(page).boundingBox())!;
  expect(box.y + box.height).toBeLessThanOrEqual(420);
  expect(await menu(page).evaluate((e) => e.scrollHeight > e.clientHeight)).toBe(true);
  await menu(page).evaluate((e) => { e.scrollTop = e.scrollHeight; });
  await expect(menu(page).locator('.mega-menu__utility a').last()).toBeInViewport();
});

test('the page behind does not scroll while the menu is open, and scrolling works again after closing', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/tin-moi-nhat');
  await page.evaluate(() => window.scrollTo(0, 300));
  await toggle(page).click();
  await page.mouse.move(700, 700);
  await page.mouse.wheel(0, 600);
  await page.waitForTimeout(150);
  expect(await page.evaluate(() => window.scrollY)).toBe(300);
  await page.keyboard.press('Escape');
  await page.mouse.wheel(0, 400);
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(300);
});

test('the scroll arrow never sits on top of the menu button', async ({ page }) => {
  await page.setViewportSize({ width: 780, height: 800 }); // narrow enough for the strip to overflow (still desktop layout)
  await page.goto('/');
  const arrow = page.locator('#primary-nav-next');
  await expect(arrow).toBeVisible();
  const a = (await arrow.boundingBox())!;
  const t = (await toggle(page).boundingBox())!;
  expect(a.x + a.width).toBeLessThanOrEqual(t.x + 1);
});

test('the login link inside the menu closes it and opens the login dialog', async ({ page }) => {
  await openMenu(page, '/');
  await menu(page).getByRole('link', { name: 'Đăng nhập' }).click();
  await expect(menu(page)).toBeHidden();
  await expect(page.locator('#reader-login-dialog')).toBeVisible();
});

test('phones: no desktop menu button; the hamburger drawer lists the same categories', async ({ page }) => {
  const all = await categories(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(toggle(page)).toBeHidden();
  await page.locator('.masthead__menu-toggle').click();
  await expect(page.locator('#utility-drawer .utility-drawer__cat-list a')).toHaveCount(all.length);
  expect(await overflow(page)).toBeLessThanOrEqual(1);
});

test('login and register pages have no site menu', async ({ page }) => {
  for (const url of ['/tai-khoan/dang-nhap', '/tai-khoan/dang-ky']) {
    await page.goto(url);
    await expect(page.locator('#mega-menu')).toHaveCount(0);
    await expect(page.locator('#mega-menu-toggle')).toHaveCount(0);
  }
});
