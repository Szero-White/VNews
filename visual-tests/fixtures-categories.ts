import { APIRequestContext, Browser } from '@playwright/test';

// Real sub-categories with real published articles for the navigation and category-page specs. The isolated E2E database starts without
// any parent/child categories, so they are created ONCE through the real CMS forms (the same requests the pages make) and reused.
// Idempotent and resumable: a category is created only when its page is missing, and articles only when its page is still empty.

export const PARENT = { slug: 'e2e-khoa-hoc', name: 'E2E Khoa học', articles: 3 };
export const CHILDREN = [
  { slug: 'e2e-vu-tru', name: 'E2E Vũ trụ', articles: 6 },
  { slug: 'e2e-thiet-bi', name: 'E2E Thiết bị', articles: 6 },
  { slug: 'e2e-ai', name: 'E2E Trí tuệ nhân tạo', articles: 3 },
];

const decode = (html: string) => html.replace(/&#x([0-9A-Fa-f]+);/g, (_, h) => String.fromCodePoint(parseInt(h, 16)));
const escapeRe = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

async function token(request: APIRequestContext, path: string): Promise<string> {
  const html = await (await request.get(path)).text();
  const m = /name="__RequestVerificationToken"[^>]*value="([^"]+)"/.exec(html);
  if (!m) throw new Error('no antiforgery token on ' + path);
  return m[1];
}
async function post(request: APIRequestContext, path: string, tokenPage: string, form: Record<string, string>) {
  const res = await request.post(path, { form: { ...form, __RequestVerificationToken: await token(request, tokenPage) }, maxRedirects: 0 });
  if (res.status() >= 400) throw new Error(`${path} -> ${res.status()}`);
  return res;
}

export async function ensureSubCategories(browser: Browser, baseURL: string): Promise<void> {
  const ctx = await browser.newContext({ baseURL });
  try {
    const request = ctx.request;
    const pageOk = async (slug: string) => (await request.get(`/chuyen-muc/${slug}`)).ok();
    const pageEmpty = async (slug: string) => decode(await (await request.get(`/chuyen-muc/${slug}`)).text()).includes('Chưa có bài viết trong chuyên mục này');
    if ((await pageOk(CHILDREN[CHILDREN.length - 1].slug)) && !(await pageEmpty(CHILDREN[CHILDREN.length - 1].slug))) return;

    await post(request, '/tai-khoan/dang-nhap', '/tai-khoan/dang-nhap', { Email: 'admin@vnews.local', Password: 'Demo@12345' });

    const categoryId = async (label: string) => {
      const html = decode(await (await request.get('/cms/CmsArticles/Create')).text());
      const m = new RegExp(`<option value="(\\d+)"[^>]*>${escapeRe(label)}</option>`).exec(html);
      if (!m) throw new Error('category not found: ' + label);
      return m[1];
    };
    const ensureCategory = async (name: string, slug: string, order: number, parentId = '') => {
      if (!(await pageOk(slug))) await post(request, '/cms/CmsCategories/Create', '/cms/CmsCategories/Create', { Name: name, Slug: slug, DisplayOrder: String(order), ParentId: parentId });
    };
    const publish = async (title: string, catId: string) => {
      const res = await post(request, '/cms/CmsArticles/Create', '/cms/CmsArticles/Create', {
        Title: title, Summary: 'Tóm tắt của bài viết dùng để kiểm thử menu chuyên mục con.',
        Body: '<p>Nội dung bài viết kiểm thử menu chuyên mục con và bố cục trang chuyên mục, đủ dài để hợp lệ.</p>', CategoryId: catId, CoverImageUrl: '/images/demo/fallback.svg',
      });
      const id = /Edit\/(\d+)/.exec(res.headers()['location'] ?? '')![1];
      await post(request, `/cms/CmsArticles/SubmitForReview/${id}`, '/cms', {});
      await post(request, `/cms/CmsArticles/Publish/${id}`, '/cms', {});
    };

    await ensureCategory(PARENT.name, PARENT.slug, 5);
    const parentId = await categoryId(PARENT.name);
    if (await pageEmpty(PARENT.slug)) for (let i = 0; i < PARENT.articles; i++) await publish(`${PARENT.name} bài viết ${i + 1}`, parentId);

    for (const [i, c] of CHILDREN.entries()) {
      await ensureCategory(c.name, c.slug, i + 1, parentId);
      if (await pageEmpty(c.slug)) {
        const childId = await categoryId(`${PARENT.name} › ${c.name}`);
        for (let n = 0; n < c.articles; n++) await publish(`${c.name} bài viết ${n + 1}`, childId);
      }
    }
  } finally {
    await ctx.close();
  }
}
