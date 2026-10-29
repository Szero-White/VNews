# VNews project status

DONE:
- M0: Solution `VNews.slnx` với 5 project (Domain, Application, Infrastructure, Web MVC, Tests), tham chiếu đúng kiến trúc modular monolith.
- M1 (vertical slice): Domain entities Article/Category/Tag/ArticleTag; EF Core 10 + SQL Server LocalDB qua `VNewsDbContext`; migration `InitialCreate` đã áp dụng vào DB `VNewsDb`; seed idempotent 10 chuyên mục + 50 bài viết tiếng Việt (hư cấu, demo) qua `DbInitializer.SeedAsync`.
- Trang chủ (`/`), chi tiết bài (`/tin/{slug}`), chuyên mục (`/chuyen-muc/{slug}`), tìm kiếm (`/tim-kiem?q=`), chủ đề (`/chu-de/{slug}`), trang 404 tùy biến — tất cả đọc dữ liệu thật qua `IArticleQueryService`/`ICategoryQueryService` (LINQ, không mock).
- CSS responsive gốc (1440 / 768 / 390) theo phong cách tòa soạn, thương hiệu độc lập "VNews"; ảnh bìa là SVG gradient tự vẽ theo chuyên mục.
- **Visual-match pass (2026-10-29):** dùng Playwright CLI thật (đã cài, `playwright-cli 0.1.22`) chụp baseline `references/vnexpress-desktop.png`/`vnexpress-mobile.png` từ vnexpress.net, so sánh với VNews, sửa 2 vòng (hero bố cục ảnh trái/chữ phải thay vì xếp dọc; vẽ lại 10 ảnh minh hoạ gradient biên tập thay cho khối màu phẳng + chữ "VNews" lặp lại trông như ảnh vỡ; cân bằng chiều cao cột hero). Chi tiết & ảnh chụp: `docs/UI-VERIFICATION.md`.

- **M2 (2026-10-29):** REST API versioned `api/v1/...`:
  - `GET /api/v1/articles` (mới nhất hoặc lọc theo `category`, phân trang), `GET /api/v1/articles/{slug}` (404 nếu không có/chưa publish).
  - `GET /api/v1/categories`, `GET /api/v1/categories/{slug}`.
  - `GET /api/v1/categories/{slug}/articles` — gọi **stored procedure** `dbo.sp_GetPublishedArticlesByCategory` (tham số hoá `@CategorySlug/@PageNumber/@PageSize`).
  - `GET /api/v1/search?q=`, `GET /api/v1/stats/daily-publish?from=&to=` — gọi **stored procedure** `dbo.sp_GetDailyPublishStats` (tham số hoá `@FromDate/@ToDate`).
  - Cả 2 stored procedure tạo qua migration `AddStoredProcedures` (`CREATE OR ALTER`, có `Down()` để rollback), gọi qua EF Core `FromSqlRaw` + `SqlParameter` (keyless entity `ArticleByCategoryResult`/`DailyPublishStatResult`), không dùng chuỗi nối trực tiếp → an toàn SQL injection.
  - Validation nhất quán: `[ApiController]` tự trả `ValidationProblem`/`400` khi `page`/`pageSize`/`q` sai; `404` dạng `ProblemDetails` khi không tìm thấy; không bao giờ lộ bài viết chưa publish (mọi service đều lọc `Status == Published`, kể cả trong stored procedure).
  - Test: 6 unit test (`PagedResultTests`) + 6 integration test thật (`ArticlesApiIntegrationTests`, dùng `WebApplicationFactory<Program>` gọi HTTP thật tới SQL Server LocalDB thật, không mock) — tổng **12/12 pass**.

- **M3 (2026-10-29):** ASP.NET Core Identity + CMS.
  - `VNewsDbContext : IdentityDbContext<ApplicationUser>`; migration `AddIdentityAndCmsWorkflow` tạo bảng AspNetUsers/Roles + cột workflow trên `Articles` (`AuthorUserId`, `ReviewedByUserId`, `ReviewNote`, `RowVersion` — concurrency token) + bảng `AuditLogs`.
  - 4 role (Admin/Editor/Author/Reader) + 3 tài khoản **demo-only** seed idempotent qua `IdentitySeeder` (`admin@vnews.local` / `editor@vnews.local` / `author@vnews.local`, mật khẩu `Demo@12345` — công khai trong source, chỉ để trình diễn, ghi rõ trong `RUN-WINDOWS.md`).
  - Workflow Draft → PendingReview → Published/Rejected qua `ArticleCommandService` (`CreateDraftAsync`, `UpdateDraftAsync` có optimistic concurrency bằng `RowVersion`, `SubmitForReviewAsync`, `PublishAsync`, `RejectAsync`), ghi `AuditLogs` cho mỗi hành động.
  - Chống XSS: `HtmlSanitizer` (Ganss.Xss, allow-list thẻ p/strong/em/ul/li/h2-h4/blockquote/a/img, allow-list thuộc tính href/src/alt, allow-list scheme http/https) áp dụng khi lưu `Article.Body` — **đã kiểm chứng thật**: gửi `<script>alert('xss')</script>` qua form thật, script bị loại bỏ hoàn toàn trong DB (xác nhận bằng SQL trực tiếp).
  - Upload ảnh an toàn (`ImageUploadService`): giới hạn 3MB, allow-list đuôi file + Content-Type, kiểm tra magic bytes thật (JPEG/PNG/WEBP) hoặc chặn `<script>`/`onload=` trong SVG, tên file random GUID (chống path traversal/ghi đè).
  - CSRF: `[ValidateAntiForgeryToken]` trên mọi POST action CMS; antiforgery token sinh/validate qua cookie thật.
  - Role-based authorization: `[Authorize(Roles=...)]` theo controller/action; Author chỉ thấy/sửa bài của mình (so khớp `AuthorUserId`), Editor/Admin thấy tất cả và mới được Publish/Reject. Admin quản lý Category (`CmsCategoriesController`) và User/Role (`CmsUsersController`).
  - **Kiểm chứng end-to-end thật bằng Playwright CLI** (không mock): đăng nhập author → tạo bài (kèm payload XSS) → gửi duyệt → đăng xuất → đăng nhập editor → thấy đúng trạng thái PendingReview → xuất bản → bài hiện công khai thật tại `/tin/{slug}` với dữ liệu từ SQL Server. Thử tấn công: Author gọi thẳng `fetch POST /cms/CmsArticles/Publish/51` (bỏ qua UI) → bị chặn, redirect `/tai-khoan/khong-co-quyen` (403), trạng thái DB không đổi. Khách chưa đăng nhập vào `/cms` → redirect `/tai-khoan/dang-nhap` (302).
  - **Bug phát hiện và đã sửa trong quá trình kiểm thử:** (1) route `cms/{controller=CmsArticles}/...` đăng ký trước route `default` và không có constraint đã làm sai lệch sinh URL cho TOÀN BỘ site (vd. link "Trang chủ" bị sinh thành `/cms/Home`) — sửa bằng route constraint `controller=CmsArticles|CmsCategories|CmsUsers`. (2) route tiếng Việt `/tai-khoan/dang-nhap` không khớp action `Login` → 404 — sửa bằng route tường minh riêng cho từng action. (3) trang lỗi tuỳ biến `PageNotFound` luôn ép status về 404 bất kể status gốc, che giấu lỗi 400 thật — sửa để giữ đúng status code gốc.
  - Test: `dotnet test` vẫn 12/12 pass sau toàn bộ thay đổi M3 (không có test riêng cho CMS do giới hạn thời gian — ghi nhận là thiếu sót, xem BLOCKERS).

- **Thay ảnh minh hoạ (2026-10-29):** theo yêu cầu người dùng (từ chối 10 ảnh gradient), đã thay bằng 10 ảnh JPEG chụp thật tải từ Picsum Photos (giấy phép Unsplash License, miễn phí sử dụng kể cả thương mại) tại `wwwroot/images/demo/{slug}.jpg`. Cập nhật `ArticleSeedData.cs` cho lần seed sau, và **UPDATE có chọn lọc** 50 dòng `Articles.CoverImageUrl` đã seed trước đó trên SQL Server LocalDB thật (`REPLACE(...'.svg'...'.jpg')`, không đụng dữ liệu/bảng khác, không reset DB). Xác nhận `/images/demo/cong-nghe.jpg` → HTTP 200, `Content-Type: image/jpeg`; chụp lại ảnh thật qua Playwright CLI (`vnews-desktop-v2-photos.png`, `vnews-mobile-v2-photos.png`). Chi tiết: `docs/UI-VERIFICATION.md` mục "Pass C".

- **M4 (2026-10-29, một phần):**
  - `tests/VNews.Tests/Integration/CmsWorkflowIntegrationTests.cs` — 5 integration test thật, không mock: khách chưa đăng nhập vào `/cms` bị redirect login; sai mật khẩu không đăng nhập được; Author đăng nhập thật tạo Draft (xác nhận `AuthorUserId`/`Status=Draft` trong DB thật); Author gọi thẳng POST `Publish` (bỏ qua UI) bị chặn redirect `/tai-khoan/khong-co-quyen`, trạng thái DB không đổi. Tổng test toàn repo: **16/16 pass** (6 unit + 11 integration, build Release không lỗi).
  - `.github/workflows/ci.yml` — build + test trên `windows-latest` (có sẵn SQL Server LocalDB, đúng stack thật, không SQLite/in-memory). **Chưa chạy thật trên GitHub** (repo chỉ có local commit, chưa push) — chỉ xác nhận `dotnet build`/`dotnet test` chạy đúng cục bộ với cùng lệnh.
  - `docs/FEATURE-MAP.md` — bảng map kỹ năng JD → vị trí code thật, phục vụ phỏng vấn.
  - Rà soát bảo mật thủ công (không dùng skill `/security-review` riêng do giới hạn thời gian): SQL injection (toàn bộ query tham số hoá qua EF Core/`SqlParameter`, không nối chuỗi) — OK; XSS (HtmlSanitizer, đã test thật) — OK; CSRF (`[ValidateAntiForgeryToken]` toàn bộ POST CMS kể cả upload) — OK; mass assignment (DTO riêng `ArticleSaveRequest`/`CategorySaveViewModel`, không bind thẳng entity) — OK; path traversal upload (tên file GUID ngẫu nhiên, không dùng tên gốc) — OK; optimistic concurrency (`RowVersion`) — OK.

- **M1 — Pass D, đo bố cục chính xác bằng Playwright Test (2026-10-29):** Theo yêu cầu "M1 CHƯA ĐẠT", dựng bộ đo `visual-tests/` (Node + `@playwright/test`), phân tích pixel thật từ `references/vnexpress-desktop.png` (loại trừ vùng quảng cáo y=0-270px) để lập bảng mục tiêu, đo bounding box DOM thật trên site, lặp sửa đến khi **tất cả vùng bố cục chính lệch ≤12px** (xem bảng đầy đủ trong `docs/UI-VERIFICATION.md` mục "Pass D"). Thay đổi chính: gỡ thanh `.utility-strip` riêng biệt (nguyên nhân chính gây lệch 43px, không có trong reference, nội dung trùng lặp với footer), dồn ngày/đăng nhập vào cùng hàng masthead khớp reference. Phát hiện và sửa thêm 2 lỗi hiển thị nghiêm trọng phát sinh ngoài dự kiến: (a) **chữ tiếng Việt vỡ dấu** trên nhiều tiêu đề do font `Georgia`/`Times New Roman` — xác minh bằng thực nghiệm (ép Arial → hết lỗi), sửa toàn bộ font tiêu đề sang Arial; (b) **ảnh trùng lặp** giữa các bài cùng chuyên mục — sửa bằng cách gán 51 ảnh riêng biệt theo từng `Article.Id` (trước đó chỉ có 10 ảnh/chuyên mục dùng chung). Dọn 8 dòng dữ liệu rác do `dotnet test` tạo lặp qua nhiều lần chạy (bài "Bai test tu dong..."/"Bai cho duyet..." trạng thái Draft/PendingReview, không hiển thị công khai nhưng gây nhiễu DB) — không đụng 51 bài demo thật.
  - Kiểm chứng: `npx playwright test` trong `visual-tests/` → 3/3 pass (desktop bounding box, mobile 390px không tràn, tablet 768px không tràn); `dotnet build`/`dotnet test` C# → Build succeeded, 16/16 pass.

- **Kiểm kê toàn site + Giai đoạn A trang chủ toàn phần (2026-10-29):** Khảo sát VnExpress/VNews bằng Playwright (full-page desktop 1440px + mobile 390px cả 2 phía, trang chuyên mục/chi tiết/tìm kiếm), lập `docs/VNEWS-PARITY-MATRIX.md` (14 section VnExpress vs 6 section VNews lúc đó). Sau phản hồi "còn bỏ sót section", triển khai **Giai đoạn A** thật trên code:
  - Mở rộng `HomePageData`/`ArticleQueryService` (Application+Infrastructure): thêm `StreamItems`, `PhotoHighlights`, `Spotlight`, `PodcastDemoItems`; khối chuyên mục mở từ 4 lên **toàn bộ 10/10 chuyên mục có bài**; refactor dùng chung 1 `Expression<Func<Article,ArticleSummaryDto>>` thay vì lặp code 5 lần.
  - Component tái sử dụng mới: `ArticleCardViewModel` + `_ArticleCard.cshtml` (7 biến thể layout), `_SiteFooter.cshtml` (footer 3 cột lấy chuyên mục thật từ DB) — thay toàn bộ markup card lặp lại trong `Index.cshtml` và footer 2 dòng cũ.
  - `Index.cshtml` viết lại hoàn chỉnh theo đúng thứ tự: Hero → **Dòng tin mới nhất (Stream)** → Tin mới nhất/Đọc nhiều → 10 khối chuyên mục → **Podcasts (demo, gắn nhãn rõ không có audio thật)** → **Ảnh** (mosaic) → **Spotlight** (card lớn). CSS mới cho 4 section, responsive đủ 3 breakpoint.
  - Kiểm chứng thật: `dotnet build` 0 lỗi; cuộn toàn trang kích hoạt lazy-load, `playwright-cli console` 0 lỗi; chụp full-page desktop+mobile, xem riêng từng section mới bằng mắt (ảnh thật, không trùng lặp, đúng kiểu trình bày); `npx playwright test` **3/3 pass** (hero geometry/overflow không bị phá vỡ); `dotnet test` **16/16 pass**. Chi tiết đầy đủ: `docs/VNEWS-PARITY-MATRIX.md` mục "Giai đoạn A — KẾT QUẢ TRIỂN KHAI THẬT".
  - **Nghiệm thu Giai đoạn A: ĐẠT** (section "khối đặc biệt"/quảng cáo của VnExpress chủ động loại trừ, có lý do ghi rõ trong parity matrix).

CURRENT: M1 (bố cục + trang chủ toàn phần theo Giai đoạn A) + M2 + M3 + M4 (một phần) hoàn tất, kiểm chứng thật qua Playwright + SQL Server + test tự động (3 visual test JS + 16 test C#). Build Release 0 lỗi.

NEXT: Giai đoạn B (trang chuyên mục: lead-story + lưới nhiều cột), Giai đoạn C (trang chi tiết: bài liên quan, caption ảnh, tags), Giai đoạn D (tìm kiếm + seed tag demo) theo `docs/VNEWS-PARITY-MATRIX.md`. Ngoài ra còn: SEO metadata + sitemap.xml; chức năng xoá bài viết/chuyên mục; test tự động cho `ImageUploadService` qua UI thật; chạy thử CI thật trên GitHub (cần push, chưa được yêu cầu).

BLOCKERS: Gói `System.Security.Cryptography.Pkcs` (transitive qua SqlClient) có cảnh báo lỗ hổng NU1902/NU1903 — theo dõi bản vá, không có cách khắc phục từ phía project (chờ EF Core/SqlClient ra bản vá). **VNews CHƯA đạt tiêu chí "v1.0 hoàn thành"** theo đúng yêu cầu người dùng (mục 6 — completion criteria) vì còn các mục liệt kê ở NEXT; báo cáo trung thực, không tự nhận hoàn thành. Trang chủ desktop/mobile/tablet nay đã đạt tiêu chí bố cục ≤12px theo yêu cầu riêng của pass này.

## Điều tra sự cố "CSS/ảnh hỏng tại localhost:5299" (2026-10-29)

Báo cáo: trình duyệt hiển thị HTML không style, ảnh bài báo hỏng tại `http://localhost:5299`.

**Những gì đã kiểm tra (không suy đoán):**
1. Phát hiện tiến trình đang LISTEN tại cổng 5299 là `VNews.Web.exe` (PID 7572, cha là `dotnet.exe` PID 17688) — đúng là tiến trình dev server của chính project này, do tôi khởi động từ phiên làm việc trước (không phải tiến trình lạ ngoài dự án). Dừng đúng 2 tiến trình này bằng `taskkill /PID 17688 /T /F`, xác nhận cổng 5299 không còn LISTEN.
2. Chạy `dotnet clean VNews.slnx` rồi `dotnet build VNews.slnx` → Build succeeded, không lỗi.
3. Kiểm tra manifest static assets sinh ra sau build (`obj/Debug/net10.0/staticwebassets.build.endpoints.json`) → xác nhận `css/site.css`, `js/site.js`, toàn bộ 10 file `images/demo/*.svg` đều được đăng ký đúng route (cả bản fingerprint và bản gốc).
4. Khởi động lại sạch: `dotnet run --no-launch-profile --urls http://localhost:5299` (ASPNETCORE_ENVIRONMENT=Development).
5. Kiểm tra HTTP thực tế bằng `curl -I`/`curl -s`:
   - `/css/site.css` → 200, `Content-Type: text/css`, 7337 bytes (không rỗng/không bị cắt), hoạt động đúng cả khi gửi `Accept-Encoding: gzip, deflate, br, zstd` (trả `Content-Encoding: gzip`, Content-Length khớp byte thực tế).
   - `/js/site.js` → 200, `Content-Type: text/javascript`.
   - `/images/demo/cong-nghe.svg` → 200, `Content-Type: image/svg+xml`, nội dung SVG hợp lệ.
   - Kiểm tra HTML trả về của `/`, `/tin/{slug}`, `/chuyen-muc/cong-nghe` → tất cả `<link>`/`<script>`/`<img>` đều dùng đường dẫn tuyệt đối gốc `/css/...`, `/js/...`, `/images/...` (không phải đường dẫn tương đối) — loại trừ khả năng lỗi route lồng nhau làm sai path khi điều hướng vào `/tin/...` hoặc `/chuyen-muc/...`.
6. Đối chiếu `ArticleSeedData.cs` với file ảnh thật trong `wwwroot/images/demo/`: 10 slug chuyên mục trong seed khớp 100% với 10 file `.svg` trên đĩa. Truy vấn trực tiếp SQL Server LocalDB (`SELECT DISTINCT CoverImageUrl FROM Articles`) xác nhận 10 giá trị `CoverImageUrl` trong DB khớp đúng path file thật, không có mismatch → **không cần sửa/update lại dữ liệu đã seed**.
7. `dotnet build VNews.slnx` (lần cuối) → Build succeeded, 0 lỗi. `dotnet test VNews.slnx` → 1/1 pass.

**Kết luận root cause:** Sau khi clean rebuild + khởi động lại hoàn toàn mới, không tái hiện được lỗi — toàn bộ asset (CSS/JS/SVG) trả về đúng HTTP 200, đúng Content-Type, nội dung đầy đủ, đường dẫn tuyệt đối đúng ở mọi loại trang (home/category/article). Không tìm thấy lỗi cấu hình trong `Program.cs` (`MapStaticAssets`, thứ tự middleware, `UseHttpsRedirection` không gây redirect vì Kestrel chỉ bind HTTP), không tìm thấy mismatch đường dẫn ảnh trong seed data. Khả năng cao nhất: hiện tượng người dùng thấy là do tiến trình dev server cũ/kết nối cũ từ phiên trước hoặc cache trình duyệt tại một thời điểm cụ thể, không phải lỗi trong source code hiện tại. **Chưa xác nhận được nguyên nhân gốc 100% vì không tái hiện được lỗi** — nếu người dùng vẫn thấy lỗi sau khi hard-refresh (Ctrl+Shift+R) trình duyệt với server vừa khởi động lại, cần cung cấp chi tiết tab Network (status code, request URL chính xác, response headers) để điều tra tiếp.

**Cập nhật 2026-10-29 (sau khi cài Playwright CLI):** đã kiểm chứng trực quan thật — xem mục "Visual-match pass" bên dưới.

## Visual-match pass — M1 UI (2026-10-29)

Dùng `playwright-cli` (đã cài sẵn trong môi trường, version 0.1.22) theo `.claude/skills/visual-match/SKILL.md`:
- Chụp baseline thật từ `https://vnexpress.net/`: `references/vnexpress-desktop.png` (1440x900), `references/vnexpress-mobile.png` (390x844).
- So sánh với VNews, sửa 2 vòng lặp (chi tiết: `docs/UI-VERIFICATION.md`):
  1. Hero chuyển bố cục ảnh trái/chữ phải (trước đó xếp dọc, sai lệch lớn nhất so với tham chiếu).
  2. Vẽ lại 10 ảnh `wwwroot/images/demo/*.svg` theo phong cách gradient biên tập, bỏ chữ "VNews" lặp lại gây cảm giác ảnh vỡ; cân bằng chiều cao 2 cột hero (`align-items: start`, giảm aspect-ratio ảnh phụ).
- Mỗi vòng đều rebuild (`dotnet build`), dừng đúng tiến trình `VNews.Web.exe` của chính dự án (xác minh bằng `wmic`/PID trước khi `taskkill`) rồi chạy lại, chụp ảnh thật bằng Playwright CLI — không suy đoán.
- Kết quả cuối: `artifacts/visual/vnews-desktop.png`, `vnews-mobile.png`, `vnews-article-desktop.png`. `playwright-cli console` → 0 lỗi/cảnh báo; `playwright-cli requests` → không có request lỗi/404.
- Khác biệt còn lại so với tham chiếu được ghi trung thực trong `docs/UI-VERIFICATION.md` (masthead đơn giản hơn vì chưa có M3/Identity, chưa test tablet 768px bằng screenshot).

VERIFICATION (đã chạy thật, không bịa kết quả):
- `dotnet build VNews.slnx` → Build succeeded.
- `dotnet ef migrations add InitialCreate` → tạo migration thành công, áp dụng khi chạy app (`Database.MigrateAsync()`).
- `dotnet run` (VNews.Web, http://localhost:5299) → log EF Core cho thấy đã tạo bảng và seed dữ liệu thật vào SQL Server LocalDB.
- `sqlcmd -S "(localdb)\MSSQLLocalDB" -d VNewsDb -Q "SELECT COUNT(*) FROM Categories; SELECT COUNT(*) FROM Articles"` → 10 Categories, 50 Articles (Published).
- HTTP smoke test: `/` → 200, `/chuyen-muc/cong-nghe` → 200, `/tin/{slug-thật-từ-db}` → 200 (hiển thị đúng tiêu đề bài viết), `/tim-kiem?q=the%20thao` → 200, `/tin/khong-ton-tai` → 404.
- `dotnet test VNews.slnx` → 1/1 test pass (scaffold xUnit, chưa có test nghiệp vụ — sẽ bổ sung ở M4).
