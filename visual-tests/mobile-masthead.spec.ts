import { test, expect } from '@playwright/test';

// Verifies the mobile masthead DOM order matches the VnExpress reference
// (references/vnexpress-mobile.png): category nav strip above the
// location/weather row, plus the interactive controls around it.

test.describe('mobile masthead 390px', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test('category nav sits above the location/weather row', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const nav = page.locator('#primary-nav');
    const weatherRow = page.locator('.mobile-weather-row');

    const navBox = await nav.boundingBox();
    const weatherBox = await weatherRow.boundingBox();

    expect(navBox, 'primary nav must be visible on mobile').not.toBeNull();
    expect(weatherBox, 'mobile weather row must be visible on mobile').not.toBeNull();
    expect(navBox!.y, 'nav must be above the weather row (lower y)').toBeLessThan(weatherBox!.y);
  });

  test('category nav strip is horizontally scrollable when it overflows', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const navInner = page.locator('.category-nav__list');
    const { scrollWidth, clientWidth } = await navInner.evaluate((el) => ({
      scrollWidth: el.scrollWidth,
      clientWidth: el.clientWidth,
    }));
    expect(scrollWidth, 'nav content must overflow its visible width on mobile').toBeGreaterThan(clientWidth);

    await navInner.evaluate((el) => { el.scrollLeft = 100; });
    const scrolledLeft = await navInner.evaluate((el) => el.scrollLeft);
    expect(scrolledLeft, 'nav strip must actually scroll').toBeGreaterThan(0);
  });

  test('hamburger opens the utility drawer and its links navigate', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const toggle = page.locator('.masthead__menu-toggle');
    const drawer = page.locator('#utility-drawer');

    await expect(drawer).not.toHaveClass(/is-open/);
    await toggle.click();
    await expect(drawer).toHaveClass(/is-open/);

    const latestLink = drawer.getByRole('link', { name: 'Tin mới nhất' });
    await expect(latestLink).toBeVisible();
    await latestLink.click();
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/tin-moi-nhat');
  });

  test('mobile search opens and submits a query', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const details = page.locator('.masthead__search--mobile-left');
    await details.locator('summary').click();
    await expect(details).toHaveAttribute('open', '');

    const input = details.locator('input[name="q"]');
    await input.fill('the thao');
    await details.locator('button[type="submit"]').click();
    await page.waitForLoadState('networkidle');
    expect(page.url()).toContain('/tim-kiem');
    expect(page.url()).toContain('q=the');
  });

  test('login link is reachable from the masthead', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    const loginLink = page.locator('.masthead__right [data-reader-login-open]');
    await expect(loginLink).toBeVisible();
    // The href remains a usable fallback without JavaScript, while the
    // normal interaction opens the Identity-backed dialog on this page.
    await expect(loginLink).toHaveAttribute('href', /\/(tai-khoan\/dang-nhap|Account\/Login)/i);
    await loginLink.click();
    await expect(page.getByRole('dialog', { name: 'Đăng nhập tài khoản' })).toBeVisible();
    await expect(page).toHaveURL(/\/$/);
  });

  test('no horizontal overflow with the reordered masthead', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
    expect(scrollWidth).toBeLessThanOrEqual(clientWidth + 2);
  });
});
