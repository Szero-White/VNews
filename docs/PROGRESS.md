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

CURRENT: M1 + M2 + M3 (core workflow) hoàn tất và đã kiểm chứng thật qua Playwright + SQL. Tiếp tục: test tự động cho CMS, và **theo yêu cầu người dùng — thay ảnh gradient M1 bằng ảnh chất lượng báo chí hơn** trước khi công bố v1.0.

NEXT: (1) Thay 10 ảnh gradient placeholder bằng ảnh biên tập chất lượng cao hơn, có quyền sử dụng hợp lệ. (2) M4 — unit/integration test cho CmsArticlesController và ArticleCommandService, GitHub Actions CI, SEO metadata, sitemap, `docs/FEATURE-MAP.md`, security review tổng thể.

BLOCKERS: Gói `System.Security.Cryptography.Pkcs` (transitive qua SqlClient) có cảnh báo lỗ hổng NU1902/NU1903 — theo dõi bản vá. **Thiếu sót đã biết (chưa khắc phục, ghi nhận trung thực):** chưa có test tự động riêng cho luồng CMS (chỉ kiểm chứng thủ công qua Playwright như trên); `ImageUploadService` chưa được test end-to-end qua UI thật (chỉ review code); chưa có chức năng xoá bài viết/chuyên mục; chưa viết GitHub Actions CI; ảnh minh hoạ vẫn là gradient (đang xử lý theo yêu cầu mới nhất của người dùng).

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
