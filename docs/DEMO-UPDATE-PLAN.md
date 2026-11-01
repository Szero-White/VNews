# Phương án cập nhật Demo (localhost:5300) — CHỜ DUYỆT, CHƯA THỰC HIỆN

## Hiện trạng
- localhost:5300 đang chạy **bản publish cũ** (`artifacts/demo-server`, `VNews.Web.dll` tạo 2026-11-01 17:08). DLL này không chứa `CmsTrash`, `CmsComments`, và vẫn dùng `app-ui.css` cũ. Vì vậy trang `/cms` chỉ có nút "Xem" (bản cũ: bài Published chưa có "Sửa", chưa có "Xóa"/"Thùng rác").
- Mã nguồn hiện tại (chưa commit) đã có nút Sửa/Xóa/Khôi phục; muốn thấy trên 5300 phải publish bản mới và khởi động lại 5300.
- Process 5300 dùng database `VNewsDemo` qua biến môi trường `ConnectionStrings__DefaultConnection`. `appsettings.json` trong thư mục publish trỏ mặc định tới **VNewsDb**.

## Migration (đều chỉ THÊM, không sửa/xóa dữ liệu cũ)
| Migration | Up | Ghi chú |
|---|---|---|
| `AddComments` | tạo bảng `Comments` + 3 chỉ mục, FK tới Articles/AspNetUsers (cascade), FK tự tham chiếu (restrict) | bảng mới, rỗng |
| `AddCategoryParent` | thêm cột `ParentId` nullable + chỉ mục + FK restrict | mọi chuyên mục hiện có = gốc |
| `AddArticleSoftDelete` | thêm 3 cột nullable `DeletedAt`, `DeletedByUserId`, `StatusBeforeDeletion` | bài hiện có không đổi |

`Down()` của cả ba đối xứng. `DbInitializer.SeedAsync` gọi `MigrateAsync()` khi ứng dụng khởi động → **chạy bản mới với bất kỳ database nào là áp migration cho database đó**. Seed idempotent, không ghi đè nội dung đã có.

## Các bước đề xuất (mỗi bước cần bạn đồng ý)
1. **Chỉ đọc**: kiểm tra `__EFMigrationsHistory` của `VNewsDemo` để biết migration cuối đã áp (SELECT, không ghi).
2. **Sao lưu**: `BACKUP DATABASE VNewsDemo ... WITH COPY_ONLY`, rồi `RESTORE VERIFYONLY` để xác nhận file dùng được.
3. **Diễn tập trên bản sao**: restore bản sao thành `VNewsDemoRehearsal`, chạy bản mới ở cổng riêng (ví dụ 5310, `DemoAuth` bật, tên DB có tiền tố `VNewsDemo`), kiểm tra dữ liệu cũ còn đủ (số bài, chuyên mục, người dùng, bình luận, bài đã lưu) và `/cms` hiển thị nút mới. Xong thì dừng 5310 và xóa bản sao.
4. **Publish bản mới sang thư mục MỚI** (`artifacts/demo-server-next`), **không ghi đè** `demo-server`. Luôn đặt `ConnectionStrings__DefaultConnection` = `VNewsDemo` trước khi chạy (không đặt = trỏ VNewsDb).
5. **Chuyển đổi**: dừng đúng PID đang giữ 5300, chạy bản mới trên 5300 với cùng biến môi trường. Kiểm tra nhanh đăng nhập và `/cms`.
6. **Hoàn tác** nếu lỗi: dừng bản mới, chạy lại `demo-server` (giữ nguyên); nếu cần đưa schema về trước thì RESTORE từ file sao lưu ở bước 2 (Down của migration chỉ là phương án phụ).

## Không làm
Không chạy bản mới trên `VNewsDb`; không đụng localhost:5299; không publish đè `demo-server`.
