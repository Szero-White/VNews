# Chạy VNews trên Windows

## Yêu cầu
- .NET 10 SDK (`dotnet --version` → 10.0.x)
- SQL Server LocalDB (`sqllocaldb info` → thấy instance `MSSQLLocalDB`). Nếu chưa có, cài kèm Visual Studio hoặc SQL Server Express LocalDB.
- Công cụ `dotnet-ef` (cài một lần): `dotnet tool install --global dotnet-ef`

## Build
```powershell
cd D:\VNews-Optimized-Claude-Code-Kit
dotnet build VNews.slnx
```

## Chuỗi kết nối
Cấu hình tại `src/VNews.Web/appsettings.json` → `ConnectionStrings:DefaultConnection`:
```
Server=(localdb)\MSSQLLocalDB;Database=VNewsDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```
Không hardcode secrets — chuỗi này chỉ dùng LocalDB cục bộ, không có mật khẩu.

## Chạy ứng dụng (tự động tạo DB + seed dữ liệu)
```powershell
cd src\VNews.Web
dotnet run
```
Khi khởi động, `Program.cs` gọi `DbInitializer.SeedAsync` sẽ:
1. Áp dụng migration EF Core (`Database.MigrateAsync()`) — tự tạo database `VNewsDb` nếu chưa có.
2. Seed idempotent 10 chuyên mục + 52 bài viết demo tiếng Việt (chỉ seed nếu bảng đang trống; riêng chuyên mục Công nghệ có 7 bài nội dung/ảnh biên soạn riêng, không dùng công thức lặp).

Mặc định ứng dụng lắng nghe tại `https://localhost:xxxx` / `http://localhost:xxxx` (xem log console). Mở trình duyệt vào URL đó.

**Lưu ý nếu chạy với cổng tùy chỉnh (`dotnet run --no-launch-profile --urls http://localhost:xxxx`):** PHẢI set `ASPNETCORE_ENVIRONMENT=Development` thủ công (ví dụ: `set ASPNETCORE_ENVIRONMENT=Development && dotnet run ...` trên cmd, hoặc `$env:ASPNETCORE_ENVIRONMENT="Development"` trên PowerShell). Cờ `--no-launch-profile` bỏ qua `launchSettings.json` (nơi set biến này mặc định), khiến môi trường rơi về `Production` → tính năng nén tài nguyên tĩnh (CSS/JS) của `MapStaticAssets()` trả về response rỗng cho mọi trình duyệt thật (gửi `Accept-Encoding: gzip` mặc định), làm vỡ toàn bộ giao diện dù server vẫn chạy bình thường.

## Kiểm tra dữ liệu SQL trực tiếp (tùy chọn)
```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d VNewsDb -Q "SELECT COUNT(*) FROM Categories; SELECT COUNT(*) FROM Articles;"
```

## Quản lý migration
```powershell
dotnet ef migrations add <Tên> --project src\VNews.Infrastructure\VNews.Infrastructure.csproj --startup-project src\VNews.Web\VNews.Web.csproj --output-dir Persistence/Migrations
dotnet ef database update --project src\VNews.Infrastructure\VNews.Infrastructure.csproj --startup-project src\VNews.Web\VNews.Web.csproj
```

## Test
```powershell
dotnet test VNews.slnx
```

## Các route public (M1)
- `/` — trang chủ
- `/chuyen-muc/{slug}` — danh sách theo chuyên mục (ví dụ `/chuyen-muc/cong-nghe`)
- `/tin/{slug}` — chi tiết bài viết
- `/tim-kiem?q=...` — tìm kiếm
- `/chu-de/{slug}` — theo tag (hiện chưa có dữ liệu tag mẫu, sẽ hiển thị trạng thái rỗng)

## REST API (M2) — versioned `api/v1`
- `GET /api/v1/articles?category=&page=&pageSize=`, `GET /api/v1/articles/{slug}`
- `GET /api/v1/categories`, `GET /api/v1/categories/{slug}`
- `GET /api/v1/categories/{slug}/articles` — qua stored procedure `dbo.sp_GetPublishedArticlesByCategory`
- `GET /api/v1/search?q=&page=&pageSize=`
- `GET /api/v1/stats/daily-publish?from=&to=` — qua stored procedure `dbo.sp_GetDailyPublishStats`

## CMS (M3) — `/cms`, yêu cầu đăng nhập
- Đăng nhập: `/tai-khoan/dang-nhap` — **tài khoản demo (chỉ để trình diễn, không phải tài khoản thật):**
  - `admin@vnews.local` / `Demo@12345` — vai trò Admin (quản lý chuyên mục, người dùng, toàn quyền)
  - `editor@vnews.local` / `Demo@12345` — vai trò Editor (duyệt/xuất bản/từ chối bài)
  - `author@vnews.local` / `Demo@12345` — vai trò Author (tạo/sửa bài nháp của chính mình)
- `/cms` — danh sách bài viết (Author chỉ thấy bài của mình; Editor/Admin thấy tất cả)
- `/cms/CmsArticles/Create`, `/cms/CmsArticles/Edit/{id}` — tạo/sửa bài (nội dung HTML được lọc sạch chống XSS khi lưu)
- `/cms/CmsCategories`, `/cms/CmsUsers` — chỉ Admin
- Quy trình: Author tạo **Nháp** → Gửi duyệt (**PendingReview**) → Editor/Admin **Xuất bản** hoặc **Từ chối** (kèm lý do) → bài Published mới hiển thị công khai.

## Đăng nhập chung và Demo một cú nhấp
- Một trang đăng nhập duy nhất cho độc giả và nhân viên: `/tai-khoan/dang-nhap` (cùng ASP.NET Core Identity). `/cms/dang-nhap` chỉ còn là đường dẫn cũ, tự chuyển về trang chung kèm `returnUrl=/cms`. Sau khi đăng nhập: Reader → trang công khai, Author → `/cms`, Editor → hàng chờ duyệt, Admin → `/cms/CmsUsers`; `returnUrl` ngoài site luôn bị bỏ qua.
- **Demo một cú nhấp (mặc định TẮT).** Thẻ "Trải nghiệm sản phẩm" (Độc giả / Tác giả / Biên tập viên / Quản trị viên) chỉ xuất hiện và chỉ hoạt động khi **cả bốn** điều kiện đúng — server kiểm tra mọi request, không chỉ ẩn nút:
  1. `ASPNETCORE_ENVIRONMENT=Development` (Production luôn từ chối, kể cả khi cấu hình sai);
  2. `DemoAuth__Enabled=true` (không khai báo = tắt);
  3. database có tên bắt đầu bằng `VNewsDemo`, `VNewsE2E` hoặc `VNewsTests_` (**không bao giờ** `VNewsDb` — chỉnh bằng `DemoAuth:AllowedDatabasePrefixes`);
  4. request đến từ chính máy này (địa chỉ loopback, host `localhost`, không có header proxy `X-Forwarded-*`).
- Chạy bản demo trên database riêng (PowerShell, trong thư mục dự án):
  ```powershell
  $env:ASPNETCORE_ENVIRONMENT = 'Development'
  $env:DemoAuth__Enabled = 'true'
  $env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=VNewsDemo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True'
  dotnet run --project src/VNews.Web --urls http://localhost:5300
  ```
  Database `VNewsDemo` tự tạo và seed khi khởi động. Tắt demo: bỏ biến `DemoAuth__Enabled` (hoặc đặt `false`) rồi chạy lại.
- Tài khoản demo là tài khoản riêng `demo.<vai trò>@demo.vnews.local`, tạo/sửa lại tự động khi bấm nút, mật khẩu ngẫu nhiên không lưu ở đâu cả (không thể đăng nhập bằng mật khẩu). Trong khi dùng demo có nhãn "DEMO · vai trò" ở góc trái dưới và nút "Thoát demo" (đăng xuất bình thường).
- Kiểm thử tự động chạy trên database cô lập: `.NET` dùng `VNewsTests_<guid>`; Playwright tự khởi động server riêng với `VNewsE2E` (cổng 5399, đã bật demo). Nếu một server dev đang khóa `bin\Debug`, build bằng `dotnet build tests/VNews.Tests -c Verify` rồi chạy `E2E_BUILD_CONFIG=Verify npx playwright test` trong `visual-tests/`.

## Lưu ý
- Không dùng mock/in-memory DB; toàn bộ nội dung public đọc từ SQL Server LocalDB qua EF Core.
- Nếu muốn reset dữ liệu, xóa database `VNewsDb` bằng `sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DROP DATABASE VNewsDb"` rồi chạy lại `dotnet run` (sẽ tự tạo và seed lại). **Chỉ thực hiện khi bạn chắc chắn muốn xóa dữ liệu.**
