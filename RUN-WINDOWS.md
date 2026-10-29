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
2. Seed idempotent 10 chuyên mục + 50 bài viết demo tiếng Việt (chỉ seed nếu bảng đang trống).

Mặc định ứng dụng lắng nghe tại `https://localhost:xxxx` / `http://localhost:xxxx` (xem log console). Mở trình duyệt vào URL đó.

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

## Lưu ý
- Không dùng mock/in-memory DB; toàn bộ nội dung public đọc từ SQL Server LocalDB qua EF Core.
- Nếu muốn reset dữ liệu, xóa database `VNewsDb` bằng `sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DROP DATABASE VNewsDb"` rồi chạy lại `dotnet run` (sẽ tự tạo và seed lại). **Chỉ thực hiện khi bạn chắc chắn muốn xóa dữ liệu.**
