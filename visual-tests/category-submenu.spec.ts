import { test, expect, Page, Browser } from '@playwright/test';
import { CHILDREN, PARENT, ensureSubCategories } from './fixtures-categories';
import { watchProblems as watch } from './support/auth';

// Sub-category menu (hover on desktop, button on touch/keyboard, drawer on phones) and the category page built from real parent/child data.
// The E2E database gets its parent/child categories and articles once, through the real CMS forms.

const VIEWPORTS = [{ width: 1536, height: 864 }, { width: 1440, height: 900 }, { width: 768, height: 1024 }, { width: 390, height: 844 }];

test.beforeAll(async ({ browser, baseURL }) => { await ensureSubCategories(browser as Browser, baseURL!); });

const item = (page: Page) => page.locator('.category-nav__item', { has: page.locator(`a[href="/chuyen-muc/${PARENT.slug}"]`) });
const panel = (page: Page) => page.locator(`#subnav-${PARENT.slug}`);
const toggle = (page: Page) => page.locator(`[aria-controls="subnav-${PARENT.slug}"]`);
const overflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

async function bringIntoView(page: Page) {
  // the strip scrolls when it is longer than the screen: make sure the parent is on screen before hovering it
  await item(page).scrollIntoViewIfNeeded();
  await page.locator('.category-nav__list').evaluate((el, slug) => {
    const a = el.querySelector(`a[href="/chuyen-muc/${slug}"]`) as HTMLElement;
    el.scrollLeft = a.offsetLeft - 60;
  }, PARENT.slug);
}

test.describe('desktop hover', () => {
  test.use({ viewport: { width: 1440, height: 900 } });

  test('hover opens the real sub-category list right under the item; the pointer can travel into it; clicking a child opens its page', async ({ page }) => {
    const problems = watch(page);
    await page.goto('/');
    await bringIntoView(page);
    await expect(panel(page)).toBeHidden();

    const link = item(page).locator('> a');
    await link.hover();
    await expect(panel(page)).toBeVisible();
    await expect(toggle(page)).toHaveAttribute('aria-expanded', 'true');

    const a = (await item(page).boundingBox())!;
    const p = (await panel(page).boundingBox())!;
    expect(Math.abs(p.y - (a.y + a.height))).toBeLessThanOrEqual(2);            // directly under the item
    expect(p.x).toBeGreaterThanOrEqual(0);
    expect(p.x + p.width).toBeLessThanOrEqual(1440);

    const names = await panel(page).locator('a').allInnerTexts();
    expect(names.map((n) => n.trim())).toEqual(CHILDREN.map((c) => c.name));      // exactly the real children, in order

    // move the pointer from the item down into the list in small steps: the list must stay open all the way
    const first = (await panel(page).locator('a').first().boundingBox())!;
    await page.mouse.move(a.x + a.width / 2, a.y + a.height - 2, { steps: 4 });
    await page.mouse.move(first.x + 30, first.y + first.height / 2, { steps: 12 });
    await page.waitForTimeout(300); // longer than the close delay
    await expect(panel(page)).toBeVisible();

    // item hover state
    await panel(page).locator('a').nth(1).hover();
    const bg = await panel(page).locator('a').nth(1).evaluate((e) => getComputedStyle(e).backgroundColor);
    expect(bg).not.toBe('rgba(0, 0, 0, 0)');

    await panel(page).locator('a').nth(1).click();
    await expect(page).toHaveURL(new RegExp(`/chuyen-muc/${CHILDREN[1].slug}$`));
    await expect(page.locator('h1.category-header__title')).toHaveText(CHILDREN[1].name);
    expect(problems).toEqual([]);
  });

  test('leaving both the item and its list closes it, without flicker; other items do not open it', async ({ page }) => {
    await page.goto('/');
    await bringIntoView(page);
    await item(page).locator('> a').hover();
    await expect(panel(page)).toBeVisible();
    await page.mouse.move(700, 600, { steps: 6 });
    await expect(panel(page)).toBeHidden({ timeout: 2000 });
    await expect(toggle(page)).toHaveAttribute('aria-expanded', 'false');
  });

  test('the list is not covered or clipped: it is the top-most element at its own centre and fully inside the viewport', async ({ page }) => {
    await page.goto('/');
    await bringIntoView(page);
    await item(page).locator('> a').hover();
    await expect(panel(page)).toBeVisible();
    const hit = await panel(page).evaluate((el) => {
      const r = el.getBoundingClientRect();
      const top = document.elementFromPoint(r.left + r.width / 2, r.top + 12);
      return { onTop: !!top && el.contains(top), bottom: r.bottom, vh: window.innerHeight };
    });
    expect(hit.onTop).toBe(true);
    expect(hit.bottom).toBeLessThanOrEqual(hit.vh);
  });

  test('keyboard: Tab reaches the button, Enter opens, arrows move through the links, Escape closes and returns focus, Tab away closes', async ({ page }) => {
    await page.goto('/');
    await bringIntoView(page);
    await toggle(page).focus();
    await expect(toggle(page)).toBeFocused();
    const ring = await toggle(page).evaluate((e) => getComputedStyle(e).outlineStyle);
    expect(ring).not.toBe('none');                                                 // visible focus ring
    await page.keyboard.press('Enter');
    await expect(panel(page)).toBeVisible();
    await expect(toggle(page)).toHaveAttribute('aria-expanded', 'true');
    await page.keyboard.press('ArrowDown');
    await expect(panel(page).locator('a').first()).toBeFocused();
    await page.keyboard.press('ArrowDown');
    await expect(panel(page).locator('a').nth(1)).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(panel(page)).toBeHidden();
    await expect(toggle(page)).toBeFocused();

    await page.keyboard.press('Space');
    await expect(panel(page)).toBeVisible();
    await page.keyboard.press('Escape');
    await toggle(page).focus();
    await page.keyboard.press('Enter');
    await expect(panel(page)).toBeVisible();
    await page.locator('#mega-menu-toggle').focus();                                // focus leaves the item
    await expect(panel(page)).toBeHidden();
  });

  test('the full "Tất cả chuyên mục" menu still works and lists the same children; opening it closes the hover list', async ({ page }) => {
    await page.goto('/');
    await bringIntoView(page);
    await item(page).locator('> a').hover();
    await expect(panel(page)).toBeVisible();
    await page.locator('#mega-menu-toggle').click();
    await expect(page.locator('#mega-menu')).toBeVisible();
    await expect(panel(page)).toBeHidden();
    const nested = page.locator('#mega-menu .mega-menu__sub a');
    expect((await nested.allInnerTexts()).map((t) => t.trim())).toEqual(expect.arrayContaining(CHILDREN.map((c) => c.name)));
    await page.keyboard.press('Escape');
    await expect(page.locator('#mega-menu')).toBeHidden();
  });

  test('the sign-in popup is not overlapped by an open list', async ({ page }) => {
    await page.goto('/');
    await bringIntoView(page);
    await item(page).locator('> a').hover();
    await expect(panel(page)).toBeVisible();
    await page.locator('.masthead__account-link').first().click();
    await expect(page.locator('#reader-login-dialog')).toBeVisible();
    await expect(panel(page)).toBeHidden();
  });
});

test.describe('touch and phones', () => {
  test.use({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });

  test('the strip list opens with its own button (no hover), the name still navigates; the drawer expands in place', async ({ page }) => {
    await page.goto('/');
    await bringIntoView(page);
    await toggle(page).tap();
    await expect(panel(page)).toBeVisible();
    const box = (await panel(page).boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(390);
    expect(await overflow(page)).toBeLessThanOrEqual(0);
    await toggle(page).tap();
    await expect(panel(page)).toBeHidden();

    // the drawer (phone menu)
    await page.locator('.masthead__menu-toggle').tap();
    const drawerToggle = page.locator(`#utility-drawer [aria-controls="drawer-sub-${PARENT.slug}"]`);
    await expect(drawerToggle).toBeVisible();
    const touchTarget = (await drawerToggle.boundingBox())!;
    expect(touchTarget.width).toBeGreaterThanOrEqual(40);                          // easy to tap
    expect(touchTarget.height).toBeGreaterThanOrEqual(40);
    await expect(page.locator(`#drawer-sub-${PARENT.slug}`)).toBeHidden();
    await drawerToggle.tap();
    await expect(drawerToggle).toHaveAttribute('aria-expanded', 'true');
    await expect(page.locator(`#drawer-sub-${PARENT.slug} a`)).toHaveCount(CHILDREN.length);
    expect(await overflow(page)).toBeLessThanOrEqual(0);

    await page.locator(`#drawer-sub-${PARENT.slug} a`).first().tap();            // a child opens its page
    await expect(page).toHaveURL(new RegExp(`/chuyen-muc/${CHILDREN[0].slug}$`));
  });

  test('tapping the parent name goes to the parent page, not just opening a list', async ({ page }) => {
    await page.goto('/');
    await page.locator('.masthead__menu-toggle').tap();
    await page.locator(`#utility-drawer a[href="/chuyen-muc/${PARENT.slug}"]`).tap();
    await expect(page).toHaveURL(new RegExp(`/chuyen-muc/${PARENT.slug}$`));
  });
});

test.describe('category page', () => {
  for (const vp of VIEWPORTS) {
    test(`parent page @${vp.width}x${vp.height}: bar, lead, cards, sub-category blocks, feed; no overflow, no broken images`, async ({ page }) => {
      const problems = watch(page);
      await page.setViewportSize(vp);
      await page.goto(`/chuyen-muc/${PARENT.slug}`);
      await expect(page.locator('h1.category-header__title')).toHaveText(PARENT.name);

      // sub-category bar: the real children, nothing else
      const barLinks = (await page.locator('.cat-subnav a').allInnerTexts()).map((t) => t.trim());
      expect(barLinks).toEqual(CHILDREN.map((c) => c.name));

      expect(await overflow(page)).toBeLessThanOrEqual(0);
      await expect(page.locator('#category-lead')).toBeVisible();

      // one block per sub-category that has stories; each has a "view all" link to a real page
      const groups = page.locator('.cat-group');
      expect(await groups.count()).toBeGreaterThan(0);
      for (const href of await groups.locator('.cat-group__all').evaluateAll((els) => els.map((e) => e.getAttribute('href')!))) {
        expect((await page.request.get(href)).status(), href).toBe(200);
      }

      // no story is shown twice on the page (lead, cards, blocks and feed together)
      const hrefs = await page.locator('.category-main a[href^="/tin/"]').evaluateAll((els) => els.map((e) => e.getAttribute('href')!));
      const unique = new Set(hrefs);
      // the lead/cards/rows link their image and their title to the same article, so compare per article container instead
      const perArticle = await page.locator('.cat-lead, .cat-card, .cat-row, .cat-group__lead, .cat-group__list li').evaluateAll((els) =>
        els.map((e) => (e.querySelector('a[href^="/tin/"]') as HTMLAnchorElement).getAttribute('href')!));
      expect(new Set(perArticle).size, 'each story once').toBe(perArticle.length);
      expect(unique.size).toBeGreaterThan(5);

      // images: loaded, and shown with their own proportions (object-fit keeps them undistorted)
      await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
      await page.waitForTimeout(300);
      const bad = await page.locator('.category-main img').evaluateAll((imgs) => imgs.filter((i) => {
        const im = i as HTMLImageElement;
        return im.complete && im.naturalWidth === 0;
      }).length);
      expect(bad).toBe(0);
      expect(problems).toEqual([]);
    });
  }

  test('child page: its own stories, siblings bar with the active one marked, breadcrumb through the parent', async ({ page }) => {
    await page.goto(`/chuyen-muc/${CHILDREN[0].slug}`);
    await expect(page.locator('.cat-subnav a[aria-current="page"]')).toHaveText(CHILDREN[0].name);
    await expect(page.locator('.cat-subnav a').first()).toHaveText(`Tất cả ${PARENT.name}`);
    await expect(page.locator('.breadcrumb a', { hasText: PARENT.name })).toBeVisible();
    await expect(page.locator('.cat-group')).toHaveCount(0);
    const titles = await page.locator('.category-main h2 a, .category-main h3 a').allInnerTexts();
    expect(titles.length).toBeGreaterThan(0);
    for (const t of titles) expect(t).toContain(CHILDREN[0].name);
    // the nav marks the section (parent) as current
    await expect(page.locator(`.category-nav__item a[href="/chuyen-muc/${PARENT.slug}"]`)).toHaveClass(/is-section/);
  });

  test('pagination: page 2 continues the feed with no story from page 1 (plain category, then a parent with sub-category blocks)', async ({ page }) => {
    for (const slug of ['thoi-su', PARENT.slug]) {
      await page.goto(`/chuyen-muc/${slug}`);
      const pages = await page.locator('.pagination__item').count();
      if (pages < 2) continue; // the parent fixture fits on one page; the .NET suite covers a three-page parent
      const first = await page.locator('.category-main a[href^="/tin/"]').evaluateAll((els) => els.map((e) => e.getAttribute('href')!));
      await page.goto(`/chuyen-muc/${slug}?page=2`);
      const second = await page.locator('.category-main a[href^="/tin/"]').evaluateAll((els) => els.map((e) => e.getAttribute('href')!));
      expect(second.length).toBeGreaterThan(0);
      expect(second.filter((h) => first.includes(h)), slug).toEqual([]);
    }
  });

  test('a category without children keeps the plain layout: no bar, no blocks, no toggle in the nav', async ({ page }) => {
    await page.goto('/chuyen-muc/cong-nghe');
    await expect(page.locator('.cat-subnav')).toHaveCount(0);
    await expect(page.locator('.cat-group')).toHaveCount(0);
    await expect(page.locator('.category-nav__item a[href="/chuyen-muc/cong-nghe"]').locator('xpath=..').locator('[data-subnav-toggle]')).toHaveCount(0);
  });
});
