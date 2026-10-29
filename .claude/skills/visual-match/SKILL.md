---
name: visual-match
description: So sánh VNews ASP.NET MVC với ảnh tham chiếu VnExpress bằng Playwright CLI, sửa giao diện theo vòng lặp và tự kiểm chứng; dùng cho frontend VNews.
disable-model-invocation: true
---

# Visual-match: one reusable workflow for VNews

**Scope:** frontend visual fidelity only. Keep .NET 10, ASP.NET Core MVC, EF Core, SQL Server, existing database/migrations, routes and articles. Read `docs/UI-REFERENCE.md`, existing `CLAUDE.md`, `docs/PROGRESS.md`, the relevant Razor views/CSS. Do not recreate the solution or jump to M2.

## Phase 0 — prerequisite and checkpoints
- Check `git status`, running localhost URL and `playwright-cli --help`. If no browser available, set up Playwright CLI per README; do not claim visual verification without screenshots.
- Ensure one existing screenshot baseline `references/vnexpress-desktop.png` at 1440x900; capture source once from public site using `scripts/capture-visual.ps1 -CaptureReference`, or use user-provided screenshot carefully cropped to page viewport. Also establish `references/vnexpress-mobile.png` at 390x844 if available. Pin baselines, **never silently replace them** on subsequent loops. Live news changes; don't judge article text/photo mismatch as CSS failure.
- Current local VNews may be at `http://localhost:5299`; use actual listening URL. If unavailable, launch using `RUN-WINDOWS.md`; only terminate processes confirmed to belong to this repo, with appropriate permissions.
- Prefer `playwright-cli` for token-efficient navigation/screenshot; refer to its installed skill or `playwright-cli --help`. Use `eval`/DOM measurements only as needed; avoid dumping complete page source or huge snapshots.

## Phase 1 — measure and design
- Inspect target screenshot and, if reachable, source page in browser. Record main container width, x/y anchors, header and nav heights, hero columns and image ratio, teaser rows, gutters, typography hierarchy, tablet/mobile behavior.
- Write a short actionable difference table in `docs/UI-VERIFICATION.md`: reference vs local vs change. Prioritize geometry over exact words and colors. Treat ads, personalized modules and breaking-news data as dynamic.
- Make **up to three controlled passes** (not endless prompting): (A) header/nav + hero/layout, (B) editorial imagery + story cards/spacing, (C) typography/polish + responsive. One pass may include several related edits, then verify.

## Phase 2 — implement only current UI scope
- Work mainly in `src/VNews.Web/Views/Shared/_Layout.cshtml`, `src/VNews.Web/Views/Home/Index.cshtml`, relevant partials, `wwwroot/css/site.css` and locally served sample images.
- **Main hero desktop:** lead image at left with article heading/description immediately to its right, not underneath; smaller teaser cards beneath. Do not use one giant monochrome placeholder in the main hero. Use independent permitted sample images or internally generated original imagery; never copy VnExpress logos, article text or copyrighted photos.
- Retain data binding to live SQL Server; don't hardcode static fake page markup or bypass the app layer. Avoid altering seeded articles unless a specific mapping needs safe migration.
- Maintain semantic headings, links, accessible controls, and mobile layout. No cosmetic change should cause broken image, horizontal overflow, or unusable nav.

## Phase 3 — visual verification loop
- After each pass, capture `artifacts/visual/vnews-desktop.png` (1440x900) and `artifacts/visual/vnews-mobile.png` (390x844) using `scripts/capture-visual.ps1`, or CLI directly. If output exists, keep previous approved baseline separately before overwriting.
- Actually inspect images, compare with pinned reference: top utility bar, masthead, nav, container width, hero geometry, story card hierarchy, white space, font scale, colors, photo aspect ratios. Identify 3 largest remaining actionable differences.
- Inspect browser console, failed network requests and link navigation; ensure `site.css`, JS and sample images load, use computed styles as needed. Screenshots are required, HTTP 200 alone is insufficient.
- Use Playwright commands `resize`, `screenshot`, `snapshot`, `eval`, `requests`, `console` as needed, with concise outputs. If source page blocks access, work from saved reference images. If output images cannot be opened by the agent, stop and report the limitation instead of claiming visual accuracy.
- Do **not** use raw pixel-diff percentage between live VnExpress and VNews as a pass metric: content/ads/photos are dynamic; judge section geometry and hierarchy. For VNews vs an approved **VNews** screenshot on the same OS/browser, Playwright `toHaveScreenshot` regression is appropriate.

## Phase 4 — acceptance and Git
- Run `dotnet build VNews.slnx`, `dotnet test VNews.slnx`; check desktop/mobile screenshots, routes, CSS/JS/assets, and no new console errors. Existing scaffold test alone does not prove UI quality.
- Update `docs/UI-VERIFICATION.md` and `docs/PROGRESS.md` with actual screenshots and verified status, plus remaining discrepancies. Report unverified items honestly.
- If verified, create a **local** conventional Git commit containing only reviewed changes. Never push, rewrite history, delete DB, or commit secrets. If user approval of design is still pending, leave changes ready and ask for that acceptance instead of declaring pixel perfection.
- Respond in <=12 lines with what improved, actual tests/screenshots and outstanding discrepancies.
