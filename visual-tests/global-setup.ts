import { mkdirSync } from 'node:fs';
import path from 'node:path';

// The isolated server starts cold: the first request to each route pays JIT + Razor view compilation, which with ten
// parallel workers has pushed a navigation past the 5 s expect timeout (mega-menu keyboard, sidebar and sign-in specs).
// Request the public routes and, signed in as the seeded admin, the newsroom/account pages once before the specs run, so
// they measure behaviour rather than start-up cost. No timeout is raised and no assertion changes.
export default async function globalSetup(config: { projects: { use: { baseURL?: string } }[] }) {
  // Some specs write measurements and screenshots to artifacts/visual, which is git-ignored and so missing on a fresh clone.
  mkdirSync(path.resolve(__dirname, '..', 'artifacts', 'visual'), { recursive: true });

  const base = process.env.E2E_BASE_URL ?? config.projects[0]?.use.baseURL ?? 'http://localhost:5399';
  const publicPaths = ['/', '/tin-moi-nhat', '/chuyen-muc/cong-nghe', '/chuyen-muc/kinh-doanh', '/tim-kiem?q=a', '/tai-khoan/dang-nhap', '/tai-khoan/dang-ky'];
  await Promise.all(publicPaths.map((p) => fetch(base + p).then((r) => r.arrayBuffer()).catch(() => undefined)));

  // Only against the isolated server: it has the seeded admin; an external E2E_BASE_URL is left to GET-only warm-up above.
  if (process.env.E2E_BASE_URL) return;
  try {
    const loginPage = await fetch(base + '/tai-khoan/dang-nhap');
    const html = await loginPage.text();
    const token = /name="__RequestVerificationToken"[^>]*value="([^"]+)"/.exec(html)?.[1];
    const cookies = (loginPage.headers.getSetCookie?.() ?? []).map((c) => c.split(';')[0]);
    if (!token) return;
    const signIn = await fetch(base + '/tai-khoan/dang-nhap', {
      method: 'POST', redirect: 'manual',
      headers: { 'content-type': 'application/x-www-form-urlencoded', cookie: cookies.join('; ') },
      body: new URLSearchParams({ Email: 'admin@vnews.local', Password: 'Demo@12345', __RequestVerificationToken: token }).toString(),
    });
    const session = [...cookies, ...(signIn.headers.getSetCookie?.() ?? []).map((c) => c.split(';')[0])].join('; ');
    const authed = ['/cms/CmsOverview', '/cms', '/cms?status=PendingReview', '/cms/CmsArticles/Create', '/cms/CmsUsers', '/cms/CmsCategories', '/cms/CmsCategories/Create', '/cms/CmsAudit', '/tai-khoan/ho-so', '/tai-khoan/bai-da-luu'];
    await Promise.all(authed.map((p) => fetch(base + p, { headers: { cookie: session } }).then((r) => r.arrayBuffer()).catch(() => undefined)));
  } catch { /* warm-up only: never fail the run */ }
}
