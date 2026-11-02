import { test, expect, Page } from '@playwright/test';
import { demoLogin } from './support/auth';

// Pasting formatted content into the article editor: structure, pictures and captions survive; styles, scripts and unknown markup do not.
// Isolated VNewsE2E database.

const CLIPBOARD_HTML = `
<meta charset="utf-8">
<div class="article" style="color:red">
  <h1 class="title">Tiêu đề lớn</h1>
  <p class="Normal" style="font-size:30px">Đoạn <b>đậm</b> và <i>nghiêng</i> có <a href="https://example.test/bai" onclick="x()">liên kết</a> và <a href="javascript:alert(1)">liên kết xấu</a>.</p>
  <script>alert('x')</script><style>p{color:blue}</style>
  <figure class="fig"><div class="fig-picture"><img data-src="https://example.test/anh-1.jpg" src="data:image/gif;base64,R0lGOD" alt="Biểu đồ"></div>
    <figcaption><p class="Image">Giá bán lẻ điện. <em>Biểu đồ: Tác giả</em></p></figcaption></figure>
  <span>Chữ rời sau ảnh</span>
  <ul><li>Một</li><li>Hai</li></ul>
  <p>&nbsp;</p>
</div>`;

async function openNewArticle(page: Page) {
  await demoLogin(page, 'author');
  await page.goto('/cms/CmsArticles/Create');
  await expect(page.locator('.rte__area')).toBeVisible();
}

async function paste(page: Page, html: string, text = '') {
  await page.locator('.rte__area').click();
  await page.evaluate(({ html, text }) => {
    const data = new DataTransfer();
    data.setData('text/html', html);
    data.setData('text/plain', text);
    document.querySelector('.rte__area')!.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
  }, { html, text });
}

test('pasting a formatted page keeps paragraphs, emphasis, links, the picture and its caption, and drops the rest', async ({ page }) => {
  await openNewArticle(page);
  await paste(page, CLIPBOARD_HTML, 'ignored plain text');

  const saved = await page.locator('textarea[data-rte]').inputValue();
  expect(saved).toContain('<h2>Tiêu đề lớn</h2>'); // h1 becomes h2
  expect(saved).toContain('<strong>đậm</strong>');
  expect(saved).toContain('<em>nghiêng</em>');
  expect(saved).toContain('<a href="https://example.test/bai">liên kết</a>');
  expect(saved).not.toContain('javascript:');
  expect(saved).not.toMatch(/onclick|style=|class=|<script|<style|<div|<span/);
  expect(saved).toContain('<img src="https://example.test/anh-1.jpg" alt="Biểu đồ">'); // the real address, not the placeholder data: URL
  expect(saved).toMatch(/<figure><img[^>]*><figcaption>Giá bán lẻ điện\. <em>Biểu đồ: Tác giả<\/em><\/figcaption><\/figure>/);
  expect(saved).toContain('<p>Chữ rời sau ảnh</p>'); // loose text becomes a paragraph
  expect(saved).toMatch(/<ul><li>Một<\/li><li>Hai<\/li><\/ul>/);
  expect(saved).not.toContain('&nbsp;</p>');
  await expect(page.locator('.rte__area figure img')).toHaveCount(1);
  await expect(page.locator('.rte__area figcaption')).toContainText('Giá bán lẻ điện');
});

test('a clipboard without structure (plain text only) still pastes as paragraphs', async ({ page }) => {
  await openNewArticle(page);
  await page.locator('.rte__area').click();
  await page.evaluate(() => {
    const data = new DataTransfer();
    data.setData('text/plain', 'Đoạn một.\n\nĐoạn hai.');
    document.querySelector('.rte__area')!.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
  });
  const saved = await page.locator('textarea[data-rte]').inputValue();
  expect(saved).toContain('<p>Đoạn một.</p>');
  expect(saved).toContain('<p>Đoạn hai.</p>');
});

test('a pasted picture with a caption is saved with the article and shown on the public page', async ({ page }) => {
  await openNewArticle(page);
  const title = `Dán nội dung ${Date.now()}`;
  await page.locator('#Title').fill(title);
  await page.locator('#CategoryId').selectOption({ index: 1 });
  await page.locator('#Summary').fill('Tóm tắt bài thử dán nội dung có định dạng.');
  await paste(page, '<p>Đoạn mở đầu đủ dài để qua bước kiểm tra độ dài tối thiểu của bài.</p><figure><img src="https://example.test/a.jpg"><figcaption>Chú thích ảnh dán</figcaption></figure>');
  await page.getByRole('button', { name: 'Tạo bản nháp' }).click();
  await page.waitForURL(/\/cms\/CmsArticles\/Edit\//, { timeout: 15000 }).catch(() => {});
  await expect(page.locator('.rte__area figure figcaption')).toContainText('Chú thích ảnh dán');
  await expect(page.locator('.rte__area figure img')).toHaveAttribute('src', 'https://example.test/a.jpg');
});

test('pasted pictures are saved on this site: the link is replaced by the stored copy, the save button waits, a failure keeps the link', async ({ page }) => {
  await openNewArticle(page);
  const asked: string[] = [];
  await page.route('**/cms/CmsImages/Import', async (route) => {
    const sent = decodeURIComponent(route.request().postData() ?? '');
    asked.push(sent);
    await new Promise((r) => setTimeout(r, 400));
    if (sent.includes('hong.jpg')) { await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ error: 'Chỉ lưu được ảnh JPG, PNG hoặc WebP.' }) }); return; }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ url: '/uploads/articles/da-luu.png' }) });
  });

  await paste(page, '<p>Mở đầu.</p><figure><img src="https://example.test/tot.jpg"><figcaption>Chú thích một</figcaption></figure><figure><img src="https://example.test/hong.jpg"><figcaption>Chú thích hai</figcaption></figure>');

  await expect(page.getByRole('button', { name: 'Tạo bản nháp' })).toBeDisabled(); // while pictures are being saved
  await expect(page.locator('.rte__note, [data-rte-note]').first()).toBeVisible().catch(() => {});
  await expect(page.locator('.rte__area figure img').first()).toHaveAttribute('src', '/uploads/articles/da-luu.png', { timeout: 10000 });
  await expect(page.getByRole('button', { name: 'Tạo bản nháp' })).toBeEnabled({ timeout: 10000 });

  const saved = await page.locator('textarea[data-rte]').inputValue();
  expect(saved).toContain('<img src="/uploads/articles/da-luu.png">');
  expect(saved).toContain('https://example.test/hong.jpg'); // the one that could not be saved still points at its source
  expect(saved).toContain('Chú thích một');
  expect(saved).toContain('Chú thích hai');
  expect(asked.length).toBe(2);
});
