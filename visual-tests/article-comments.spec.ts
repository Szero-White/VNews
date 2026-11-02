import { test, expect, Page } from '@playwright/test';
import { registerReader as register, watchProblems as watch } from './support/auth';

// Article reading page: byline, previous/next, comments (post, reply, withdraw, moderate), responsive layout, no JS errors.
// Isolated VNewsE2E database. Each test registers its own reader so parallel specs never share state.

const VIEWPORTS = [{ width: 1440, height: 900 }, { width: 1536, height: 864 }, { width: 768, height: 1024 }, { width: 390, height: 844 }];

/** A published article that has both a previous and a next article in its category (the middle of a timeline). */
async function middleArticle(page: Page): Promise<string> {
  const list = (await (await page.request.get('/api/v1/articles?category=cong-nghe&pageSize=50')).json()).items as { slug: string }[];
  return list[Math.floor(list.length / 2)].slug;
}
const comments = (page: Page) => page.locator('#binh-luan');

test('anonymous reader sees the byline, the comment list and a login prompt; no comment form', async ({ page }) => {
  const problems = watch(page);
  await page.goto(`/tin/${await middleArticle(page)}`);
  await expect(page.locator('.article-byline')).toContainText('phút đọc');
  await expect(page.locator('.article-byline')).toContainText('bình luận');
  await expect(comments(page).getByRole('heading', { name: /Ý kiến/ })).toBeVisible();
  await expect(comments(page).getByText('Đăng nhập để tham gia bình luận')).toBeVisible();
  await expect(page.locator('#comment-form')).toHaveCount(0);
  expect(problems).toEqual([]);
});

test('login from the comment prompt returns to the same article, and then the form is there', async ({ page }) => {
  const slug = await middleArticle(page);
  await page.goto(`/tin/${slug}`);
  await comments(page).getByRole('link', { name: 'Đăng nhập' }).click();
  const dialog = page.locator('#reader-login-dialog');
  await expect(dialog).toBeVisible();
  await dialog.locator('input[name="Email"]').fill('author@vnews.local');
  await dialog.locator('input[name="Password"]').fill('Demo@12345');
  await dialog.locator('.reader-login-dialog__submit').click();
  await expect(page).toHaveURL(new RegExp(`/tin/${slug}`));
  await expect(page.locator('#comment-form')).toBeVisible();
});

test('post, reply, withdraw: the whole comment flow as a reader', async ({ page }) => {
  const problems = watch(page);
  await register(page, 'Độc giả bình luận');
  await page.goto(`/tin/${await middleArticle(page)}`);

  const stamp = Date.now(); // texts are unique per run: the E2E database keeps earlier runs' comments
  const text = `Bài viết hữu ích ${stamp} <b>không phải thẻ</b>`;
  const reply = `Cảm ơn bạn đã chia sẻ ${stamp}.`;
  await page.locator('#comment-body').fill(text);
  await page.getByRole('button', { name: 'Gửi bình luận' }).click();
  await expect(comments(page).locator('.comments__notice')).toContainText('Đã đăng bình luận');
  const mine = comments(page).locator('.comment', { hasText: String(stamp) }).first();
  await expect(mine.locator('.comment__body')).toHaveText(text);            // shown as text, tags not interpreted
  await expect(mine.locator('.comment__body b')).toHaveCount(0);
  await expect(mine.locator('.comment__author')).toHaveText('Độc giả bình luận');

  // reply (details element, no script)
  await mine.locator('summary', { hasText: 'Trả lời' }).click();
  await mine.locator('textarea[name="body"]').fill(reply);
  await mine.getByRole('button', { name: 'Gửi trả lời' }).click();
  await expect(comments(page).locator('.comment--reply', { hasText: reply })).toBeVisible();
  await expect(comments(page).locator('h2 .comments__count')).toHaveText(/[2-9]|\d{2,}/);

  // a refusal keeps the typed text
  await page.locator('#comment-body').fill('a');
  await page.evaluate(() => (document.querySelector('#comment-body') as HTMLTextAreaElement).removeAttribute('minlength'));
  await page.getByRole('button', { name: 'Gửi bình luận' }).click();
  await expect(comments(page).locator('.comments__error')).toContainText('ít nhất 2 ký tự');

  // withdraw own comment
  await comments(page).locator('.comment', { hasText: String(stamp) }).first().locator('> .comment__actions').getByRole('button', { name: 'Xóa' }).click();
  await expect(comments(page).locator('.comments__notice')).toContainText('Đã xóa bình luận');
  await expect(comments(page).locator('.comment', { has: page.locator('.comment--reply', { hasText: reply }) }).locator('> .comment__removed')).toContainText('tác giả xóa'); // the reply keeps its thread
  expect(problems).toEqual([]);
});

test('an editor can take a comment down; a plain reader sees no such button', async ({ page, browser }) => {
  const slug = await middleArticle(page);
  await register(page, 'Người sẽ bị gỡ bình luận');
  await page.goto(`/tin/${slug}`);
  const text = `Nội dung cần gỡ ${Date.now()}`;
  await page.locator('#comment-body').fill(text);
  await page.getByRole('button', { name: 'Gửi bình luận' }).click();
  await expect(comments(page).locator('.comment', { hasText: text })).toBeVisible();
  await expect(comments(page).getByRole('button', { name: 'Gỡ bình luận' })).toHaveCount(0);

  const ctx = await browser.newContext();
  const editor = await ctx.newPage();
  await editor.goto('/tai-khoan/dang-nhap');
  await editor.locator('input[name="Email"]').fill('editor@vnews.local');
  await editor.locator('input[name="Password"]').fill('Demo@12345');
  await editor.locator('.reader-auth__submit').click();
  await editor.waitForURL((u) => !u.pathname.includes('dang-nhap'));
  await editor.goto(`/tin/${slug}`);
  await editor.locator('.comment', { hasText: text }).first().getByRole('button', { name: 'Gỡ bình luận' }).click();
  await expect(editor.locator('.comments__notice')).toContainText('Đã gỡ bình luận');
  await expect(editor.getByText(text)).toHaveCount(0);
  await ctx.close();
});

test('previous / next article links navigate within the category', async ({ page }) => {
  const slug = await middleArticle(page);
  await page.goto(`/tin/${slug}`);
  const prev = page.locator('.article-neighbors__link--prev');
  const next = page.locator('.article-neighbors__link--next');
  await expect(prev).toBeVisible();
  await expect(next).toBeVisible();
  const nextHref = await next.getAttribute('href');
  expect(nextHref).not.toContain(slug);
  await next.click();
  await expect(page).toHaveURL(new RegExp(nextHref!.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
  await expect(page.locator('h1.article-detail__title')).toBeVisible();
  await expect(page.locator('.article-neighbors__link--prev')).toBeVisible(); // and back again
});

for (const vp of VIEWPORTS) {
  test(`comment section layout @${vp.width}x${vp.height}: no horizontal scroll, form and thread inside the column`, async ({ page }) => {
    await page.setViewportSize(vp);
    await register(page, 'Độc giả bố cục');
    await page.goto(`/tin/${await middleArticle(page)}`);
    const marker = `bố-cục-${vp.width}-${Date.now()}`; // unique per run: wait for THIS comment, not for one a parallel test left on the page
    await page.locator('#comment-body').fill('Bình luận dài '.repeat(30) + marker + ' https://example.com/' + 'a'.repeat(80));
    await page.getByRole('button', { name: 'Gửi bình luận' }).click();
    await expect(comments(page).locator('.comment__body', { hasText: marker })).toBeVisible();
    const m = await page.evaluate(() => {
      const col = document.querySelector('.article-layout__main')!.getBoundingClientRect();
      const c = document.querySelector('#binh-luan')!.getBoundingClientRect();
      const overflowing = [...document.querySelectorAll('#binh-luan *')].filter((e) => e.getBoundingClientRect().right > document.documentElement.clientWidth + 1).length;
      return { colL: col.left, colR: col.right, l: c.left, r: c.right, doc: document.documentElement.scrollWidth - document.documentElement.clientWidth, overflowing };
    });
    expect(m.doc).toBeLessThanOrEqual(0);
    expect(m.overflowing).toBe(0);
    expect(m.l).toBeGreaterThanOrEqual(m.colL - 1);
    expect(m.r).toBeLessThanOrEqual(m.colR + 1);
  });
}

test('existing reading tools still work next to the new sections (save, share, font size)', async ({ page }) => {
  await register(page, 'Độc giả công cụ');
  await page.goto(`/tin/${await middleArticle(page)}`);
  const save = page.locator('[data-action="save"]');
  await save.click();
  await expect(save).toHaveAttribute('aria-pressed', 'true');
  await page.locator('[data-action="font-up"]').click();
  await expect(page.locator('#article-body')).toBeVisible();
});
