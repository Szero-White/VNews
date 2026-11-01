import { Page } from '@playwright/test';

// Shared helpers for the specs that sign in, register or watch for script errors. One place, so every spec waits for the same things.

export type Role = 'reader' | 'author' | 'editor' | 'admin';

/** The seeded staff accounts of the isolated test database (development seed; password is public in the repo on purpose). */
export const SEEDED = { author: 'author@vnews.local', editor: 'editor@vnews.local', admin: 'admin@vnews.local' } as const;
export const SEEDED_PASSWORD = 'Demo@12345';

/** Collects page errors and console errors. Network-level failures (a blocked or slow third-party font host) are noise, not application errors. */
export function watchProblems(page: Page): string[] {
  const problems: string[] = [];
  const networkNoise = /net::ERR_(SOCKET_NOT_CONNECTED|NAME_NOT_RESOLVED|INTERNET_DISCONNECTED|CONNECTION_\w+|TIMED_OUT|NETWORK_CHANGED)/;
  page.on('pageerror', (e) => problems.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error' && !networkNoise.test(m.text())) problems.push('console: ' + m.text()); });
  return problems;
}

/** One-click demo sign-in from the sign-in page; resolves once the redirect has left the sign-in page. */
export async function demoLogin(page: Page, role: Role): Promise<void> {
  await page.goto('/tai-khoan/dang-nhap');
  await page.locator(`.demo-login--page [data-demo-role="${role}"]`).click();
  await page.waitForURL((u) => !u.pathname.includes('dang-nhap'));
}

/** Normal sign-in through the form. */
export async function realLogin(page: Page, email: string, password = SEEDED_PASSWORD): Promise<void> {
  await page.goto('/tai-khoan/dang-nhap');
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill(password);
  await page.locator('.reader-auth__submit').click();
  await page.waitForURL((u) => !u.pathname.includes('dang-nhap'));
}

/** Registers a brand-new reader (unique e-mail) and leaves the page signed in. Returns the e-mail. */
export async function registerReader(page: Page, displayName: string): Promise<string> {
  const email = `reader-${Date.now()}-${Math.floor(Math.random() * 1e6)}@example.test`;
  await page.goto('/tai-khoan/dang-ky');
  await page.locator('input[name="DisplayName"]').fill(displayName);
  await page.locator('input[name="Email"]').fill(email);
  await page.locator('input[name="Password"]').fill('TestPassword123!');
  await page.locator('input[name="ConfirmPassword"]').fill('TestPassword123!');
  await page.locator('.reader-auth__submit').click();
  await page.waitForURL((u) => !u.pathname.includes('dang-ky'));
  return email;
}

// ---- small helpers shared by the sign-in / demo specs ----

export const badge = (page: Page) => page.locator('.demo-badge');

/** Leaves demo mode with the "Thoát demo" button of the floating badge. */
export async function signOutFromBadge(page: Page): Promise<void> {
  await badge(page).getByRole('button', { name: 'Thoát demo' }).click();
}

/** Horizontal overflow of the document in pixels (0 = none). */
export const horizontalOverflow = (page: Page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);

/** The slug of the newest published article (from the public API). */
export async function newestArticleSlug(page: Page): Promise<string> {
  return (await (await page.request.get('/api/v1/articles?pageSize=1')).json()).items[0].slug;
}
