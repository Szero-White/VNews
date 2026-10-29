# VNews project status

DONE:
- M0: Solution `VNews.slnx` với 5 project (Domain, Application, Infrastructure, Web MVC, Tests), tham chiếu đúng kiến trúc modular monolith.
- M1 (vertical slice): Domain entities Article/Category/Tag/ArticleTag; EF Core 10 + SQL Server LocalDB qua `VNewsDbContext`; migration `InitialCreate` đã áp dụng vào DB `VNewsDb`; seed idempotent 10 chuyên mục + 50 bài viết tiếng Việt (hư cấu, demo) qua `DbInitializer.SeedAsync`.
- Trang chủ (`/`), chi tiết bài (`/tin/{slug}`), chuyên mục (`/chuyen-muc/{slug}`), tìm kiếm (`/tim-kiem?q=`), chủ đề (`/chu-de/{slug}`), trang 404 tùy biến — tất cả đọc dữ liệu thật qua `IArticleQueryService`/`ICategoryQueryService` (LINQ, không mock).
- CSS responsive gốc (1440 / 768 / 390) theo phong cách tòa soạn, thương hiệu độc lập "VNews"; ảnh bìa là SVG placeholder tự vẽ theo chuyên mục.

CURRENT: M1 hoàn tất vertical slice chính; chưa làm phân trang kiểm thử kỹ UI thật trên trình duyệt (không có browser tool trong môi trường này).

NEXT: M2 — REST API versioned cho public articles/categories/search, stored procedures tham số hóa (phân trang theo chuyên mục, thống kê publish theo ngày).

BLOCKERS: Không có browser/screenshot tool trong môi trường hiện tại → chưa thực hiện kiểm tra hình ảnh trực quan thực tế (chỉ xác nhận HTTP 200 + cấu trúc HTML). Gói `System.Security.Cryptography.Pkcs` (transitive qua SqlClient) có cảnh báo lỗ hổng NU1902/NU1903 — theo dõi khi có bản vá EF Core/SqlClient mới.

VERIFICATION (đã chạy thật, không bịa kết quả):
- `dotnet build VNews.slnx` → Build succeeded.
- `dotnet ef migrations add InitialCreate` → tạo migration thành công, áp dụng khi chạy app (`Database.MigrateAsync()`).
- `dotnet run` (VNews.Web, http://localhost:5299) → log EF Core cho thấy đã tạo bảng và seed dữ liệu thật vào SQL Server LocalDB.
- `sqlcmd -S "(localdb)\MSSQLLocalDB" -d VNewsDb -Q "SELECT COUNT(*) FROM Categories; SELECT COUNT(*) FROM Articles"` → 10 Categories, 50 Articles (Published).
- HTTP smoke test: `/` → 200, `/chuyen-muc/cong-nghe` → 200, `/tin/{slug-thật-từ-db}` → 200 (hiển thị đúng tiêu đề bài viết), `/tim-kiem?q=the%20thao` → 200, `/tin/khong-ton-tai` → 404.
- `dotnet test VNews.slnx` → 1/1 test pass (scaffold xUnit, chưa có test nghiệp vụ — sẽ bổ sung ở M4).
