# VNews — Hướng dẫn chạy & kịch bản trình diễn khi phỏng vấn

Tài liệu này tóm tắt những gì cần biết để **chạy VNews và trình diễn trực tiếp** khi phỏng vấn. Chi tiết kỹ thuật đầy đủ (route, API, migration) xem `RUN-WINDOWS.md`.

## 1. Chạy nhanh

Yêu cầu: .NET 10 SDK, SQL Server LocalDB (`sqllocaldb info` thấy `MSSQLLocalDB`).

```powershell
cd D:\VNews-Optimized-Claude-Code-Kit
dotnet build VNews.slnx
cd src\VNews.Web
dotnet run
```

Mở URL hiện trong log console (`https://localhost:xxxx`). Lần chạy đầu tiên trên máy mới sẽ **tự tạo database `VNewsDb` và seed dữ liệu mẫu** — không cần thao tác gì thêm, không cần file SQL rời.

**Nếu chạy với `--urls` tùy chỉnh** (ví dụ cổng cố định để demo), phải set `ASPNETCORE_ENVIRONMENT=Development` thủ công, nếu không CSS/JS sẽ trả về rỗng:
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --no-launch-profile --urls http://localhost:5299
```

## 2. Database & dữ liệu mẫu

- SQL Server LocalDB, EF Core 10, migration tự áp dụng khi khởi động (`DbInitializer.SeedAsync` gọi `Database.MigrateAsync()`).
- Seed **chỉ chạy nếu bảng `Articles` đang trống** (idempotent — không tạo trùng lặp khi chạy lại).
- Trên database mới: 10 chuyên mục, 52 bài viết Published. Riêng **chuyên mục Công nghệ có 7 bài được biên soạn nội dung và chọn ảnh riêng từng bài** (không dùng công thức mô tả lặp lại như 9 chuyên mục còn lại) — đây là chuyên mục nên ưu tiên trình diễn.
- Cơ chế seed nằm hoàn toàn trong code (`src/VNews.Infrastructure/Seed/ArticleSeedData.cs`), **không phụ thuộc file `.sql` rời** — đã kiểm chứng bằng cách trỏ ứng dụng vào một database LocalDB trống hoàn toàn và xác nhận seed tái tạo đúng nội dung.
- Không reset database khi không cần thiết. Muốn làm lại từ đầu: xem ghi chú cuối `RUN-WINDOWS.md` (`DROP DATABASE VNewsDb` rồi chạy lại).

## 3. Tài khoản demo

| Email | Mật khẩu | Vai trò |
|---|---|---|
| `admin@vnews.local` | `Demo@12345` | Admin — toàn quyền, quản lý chuyên mục/người dùng |
| `editor@vnews.local` | `Demo@12345` | Editor — duyệt/xuất bản/từ chối bài |
| `author@vnews.local` | `Demo@12345` | Author — tạo/sửa bài nháp của chính mình |

Đây là tài khoản demo công khai trong mã nguồn, không phải thông tin thật.

## 4. Chức năng đã hoàn thành (đã kiểm chứng bằng thao tác thực tế)

- **Trang công khai**: trang chủ, danh sách theo chuyên mục (có bài nổi bật + phân trang thật), trang chi tiết bài, trang tìm kiếm, trang "Đã lưu" — toàn bộ đọc từ SQL Server qua EF Core, không có mảng dữ liệu giả trong Razor.
- **Tìm kiếm**: khớp theo từ khóa có dấu, trả kết quả thật từ DB (đã kiểm: tìm "an ninh mạng" ra đúng 1 bài). *Giới hạn đã biết*: chưa chuẩn hóa bỏ dấu, tìm "an ninh mang" (không dấu) sẽ không ra kết quả.
- **Công cụ đọc bài** (đã bấm thử, không chỉ kiểm tra API tồn tại):
  - *Lưu bài*: ghi thật vào `localStorage`, có toast xác nhận, trạng thái nút đổi và đồng bộ với trang "Đã lưu".
  - *Chia sẻ*: gọi `navigator.share()` thật (đã xác minh payload title+url đúng); trình duyệt không hỗ trợ Web Share sẽ tự chuyển sang copy link vào clipboard.
  - *Nghe tóm tắt*: dùng Web Speech API thật (`SpeechSynthesisUtterance`), đã xác minh nội dung đọc đúng = tiêu đề + tóm tắt bài, nút chuyển trạng thái "Dừng đọc" khi đang phát.
  - *Tăng/giảm cỡ chữ*: đổi `font-size` thật trên DOM, giới hạn 15–25px.
- **CMS** (`/cms`): luồng **Author tạo Nháp → Gửi duyệt → Editor Xuất bản → hiển thị công khai** đã chạy thử toàn bộ bằng thao tác trình duyệt thật (đăng nhập, điền form, click nút) trong phiên này, không phải chỉ test tự động. Phân quyền đúng: Author không tự xuất bản được.
- **Toolbar đọc bài trên mobile (390px)**: đã sửa lỗi tràn/xuống dòng, 5 nút nằm gọn một hàng.

## 5. Kịch bản trình diễn đề xuất (~5 phút)

1. Mở trang chủ → lướt xuống chuyên mục **Công nghệ** trên trang chủ, bấm "Xem thêm".
2. Trên trang chuyên mục Công nghệ: chỉ ra bài nổi bật + ảnh đúng chủ đề + phân trang, nhấn vào một bài.
3. Trên trang đọc bài: bấm **Lưu bài** (chỉ trạng thái đổi + toast) → vào menu **Đã lưu** chỉ bài vừa lưu còn đó.
4. Quay lại bài, bấm **Nghe tóm tắt** (nếu demo có loa/trình duyệt hỗ trợ) và **A+/A−** để chỉnh cỡ chữ.
5. Mở `/cms`, đăng nhập `author@vnews.local`, tạo một bài nháp mới → Gửi duyệt.
6. Đăng xuất, đăng nhập `editor@vnews.local`, vào `/cms`, bấm **Xuất bản** cho bài vừa gửi.
7. Quay lại trang chuyên mục Công nghệ → chỉ ra bài vừa xuất bản xuất hiện công khai ngay lập tức.
8. (Tùy chọn) Resize cửa sổ trình duyệt xuống ~390px để minh họa giao diện mobile.

## 6. Vấn đề/giới hạn còn tồn tại (minh bạch)

- **9 chuyên mục còn lại** (Thời sự, Kinh doanh, Thể thao...) vẫn dùng mô tả bài theo công thức lặp và ảnh gán theo số thứ tự toàn cục — chưa được biên soạn riêng như Công nghệ. Nên né các chuyên mục này khi trình diễn, hoặc nói rõ đây là phạm vi mở rộng tiếp theo.
- **Dữ liệu rác trong CMS**: các bài integration test (`dotnet test`) tạo ra (`Bai test tu dong...`, `Bai cho duyet...`) tồn tại ở trạng thái Draft/PendingReview trong `/cms` — **không hiển thị công khai** nên không ảnh hưởng demo trang chủ/chuyên mục, nhưng làm danh sách `/cms` trông lộn xộn nếu mở ra. Nguyên nhân: test tích hợp hiện chạy thẳng vào `VNewsDb` (không có DB test riêng) — đây là hạn chế kiến trúc có sẵn từ trước, chưa sửa trong lượt này vì ngoài phạm vi yêu cầu (không thêm tính năng/không mở rộng nhiệm vụ). Nếu cần dọn trước khi phỏng vấn, chạy:
  ```sql
  DELETE FROM Articles WHERE Title LIKE 'Bai test tu dong%' OR Title LIKE 'Bai cho duyet%';
  ```
- **Tìm kiếm không chuẩn hóa dấu tiếng Việt** (xem mục 4) — hành vi có từ trước, không phải lỗi mới.
- Ảnh minh họa lấy từ Unsplash (giấy phép dùng tự do, không cần ghi công bắt buộc) — đã ghi chú ở footer trang web.
