# VNews — Triển khai lên hosting (giữ nguyên dữ liệu và ảnh của bạn)

Mục tiêu: trang trên host hiển thị **đúng** các bài, chuyên mục, ảnh và quan hệ dữ liệu bạn đã chỉnh sửa, **không tự tạo nội dung mẫu**, giữ HTTPS bắt buộc và cookie Secure.

## 1. Nguồn dữ liệu (đã xác minh chỉ-đọc)

| | VNewsDemo | VNewsDb |
|---|---|---|
| Hoạt động mới nhất (UTC) | 02/11/2026 ~08:10, và còn tiếp tục (có `UpdatePublished`, `Trash` do tài khoản demo admin thực hiện) | 01/11/2026 02:21, toàn bài test tự động |
| Nội dung | 262 bài (đang đăng + thùng rác), 14 chuyên mục, 4 chuyên mục con của Thời sự, ảnh bìa, bộ sưu tập, ảnh trong bài, 11 migration | 154 bài (50 bản nháp, 52 chờ duyệt, 52 đã đăng), 10 chuyên mục, 4 migration, thiếu bảng bình luận |
| Kết luận | **Đây là nguồn cần chuyển** | Cũ, không chứa chỉnh sửa gần đây |

Ảnh nằm ở `artifacts\demo-uploads\articles` (1.000+ file, ~215 MB). Chỉ ~170 ảnh đang được bài dùng (bìa, bộ sưu tập, trong nội dung); các file còn lại là ảnh đã thay/bỏ hoặc ảnh thử. **Mọi ảnh được dùng đều có trên đĩa, không thiếu file nào.** Gói triển khai chỉ chứa ảnh đang dùng.

> VNewsDemo còn thay đổi khi bạn tiếp tục chỉnh trên localhost:5300. Chạy `docs\deploy\refresh-package.ps1` **ngay trước khi chuyển**, khi bạn đã chỉnh sửa xong.

## 2. Gói triển khai: `artifacts\host-package\`

| Thư mục/file | Nội dung |
|---|---|
| `app\` | Bản Release (đã loại `appsettings.Development.json`, có `web.config` cho IIS) |
| `db\VNewsDemo-final.bak` | Sao lưu COPY_ONLY + CHECKSUM của VNewsDemo (đã `RESTORE VERIFYONLY` và khôi phục thử vào DB tạm: số liệu và checksum nội dung trùng khớp) |
| `uploads\articles\...` | Các ảnh đang được nội dung dùng, giữ nguyên tên file (đường dẫn trong DB là `/uploads/articles/<tên>`) |
| `uploads-manifest.txt` | Danh sách đường dẫn ảnh được tham chiếu |

## 3. Đã kiểm chứng

- Chạy đúng gói `app\` ở chế độ **Production, qua HTTPS**, trên bản khôi phục của VNewsDemo: trang chủ, chuyên mục (cha và con), tìm kiếm, bài viết, ảnh `/uploads/...` đều 200; `/cms` chuyển về trang đăng nhập khi chưa đăng nhập; đăng nhập thành công với cookie `secure; httponly; samesite=lax`; khóa Data Protection ghi ra thư mục cấu hình.
- Sau lần chạy đó, bản sao **không đổi** (cùng số bài, cùng checksum nội dung): ứng dụng không seed, không thêm, không sửa dữ liệu.
- Production trên DB trống: chỉ áp migration và tạo vai trò; **0 chuyên mục, 0 bài, 0 tài khoản** (test `ProductionStartupTests`).
- Mã khởi tạo: `Seed:SampleContent` mặc định chỉ bật ở Development. Để trống ở host.

## 4. Cấu hình trên host (biến môi trường, không đưa vào Git)

| Biến | Giá trị |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | chuỗi kết nối DB của host |
| `Uploads__RootPath` | thư mục ảnh **bền và ghi được**, ngoài thư mục ứng dụng (ví dụ `D:\home\vnews-data\uploads`) |
| `DataProtection__KeysPath` | thư mục **bền và ghi được** để giữ khóa đăng nhập/antiforgery (không có thì có thể bị đăng xuất mỗi lần khởi động lại) |
| `AllowedHosts` | tên miền thật của site (không để `*`) |
| `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` | **chỉ dùng cho lần khởi động đầu**, mật khẩu mạnh; xóa ngay sau đó |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` chỉ khi đứng sau proxy/CDN kết thúc TLS (Cloudflare, nginx...). Trên IIS không cần |
| `Seed__SampleContent` | **không đặt** (hoặc `false`). Không bao giờ đặt `true` trên host |

Không đặt `DemoAuth__Enabled` (đăng nhập nhanh chỉ chạy ở Development trên localhost).

## 5. HTTPS và cookie Secure

Cookie đăng nhập và antiforgery luôn `Secure`. Nếu request đến qua HTTP thường, mọi trang có form (kể cả trang chủ) trả lỗi 500. Vì vậy:

1. Bật HTTPS cho tên miền **trước khi** mở site cho người dùng (host miễn phí thường phải gửi ticket để bật).
2. Nếu có proxy/CDN: bảo đảm nó chuyển `X-Forwarded-Proto` và đặt `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.
3. Kiểm tra bằng `https://tên-miền/` trước, không nới lỏng cookie để "cho chạy tạm".

## 6. Các bước chuyển (chỉ làm khi bạn xác nhận nguồn dữ liệu)

1. **Chốt dữ liệu:** dừng chỉnh sửa trên 5300, chạy `.\docs\deploy\refresh-package.ps1`. Kết quả phải ghi `missing on disk: 0`.
2. **Sao lưu dự phòng** (giữ riêng): copy `db\VNewsDemo-final.bak` và thư mục `artifacts\demo-uploads` sang nơi an toàn. Đây là điểm khôi phục.
3. **Tạo DB trống** trên host và lấy chuỗi kết nối.
4. **Chuyển DB** (chọn theo khả năng của host):
   - **A. Khôi phục `.bak`** nếu host cho phép (`RESTORE DATABASE ... FROM DISK`). Cách này giữ nguyên mọi quan hệ, ID và slug.
   - **B. Chuyển dữ liệu qua SSMS** khi host không cho khôi phục `.bak`: chạy ứng dụng một lần trên DB trống để tạo bảng (không seed gì), rồi dùng *Tasks → Generate Scripts → "Types of data to script: Data only"* trên bản khôi phục cục bộ để nạp dữ liệu vào host (bật `IDENTITY_INSERT`, nạp theo thứ tự khóa ngoại, bao gồm `__EFMigrationsHistory` không cần nạp vì ứng dụng đã áp).
5. **Khóa tài khoản mẫu** trên DB của host (không phải trên máy bạn): chạy `docs\deploy\post-restore-secure-accounts.sql`. Tài khoản `admin@vnews.local`, `editor@...`, `author@...`, `demo.*@demo.vnews.local` dùng mật khẩu công khai `Demo@12345`. Script khóa và gỡ vai trò của chúng; tài khoản thật (ví dụ `congtoan2k4@gmail.com`) giữ nguyên. Script đã thử trên bản sao.
6. **Chép ảnh**: copy `uploads\articles\*` vào `Uploads__RootPath\articles\` trên host (FTP/SFTP).
7. **Chép ứng dụng**: copy `app\*` vào thư mục site, đặt biến cấu hình ở mục 4 (kèm `Bootstrap__*`).
8. **Khởi động lần đầu**: ứng dụng áp migration còn thiếu (không có gì nếu đã khôi phục), tạo quản trị viên từ `Bootstrap__*` (vì không còn Admin nào). Xóa hai biến `Bootstrap__*`, khởi động lại.
9. **Kiểm tra**: số bài và chuyên mục khớp, bài có ảnh hiển thị, đăng nhập Admin, thử tạo/sửa/xóa mềm một bài thử rồi khôi phục.

Người viết bài (Author/Editor) sau đó tự đăng ký tài khoản Reader; Admin đổi vai trò trong trang Người dùng.

## 7. Sao lưu và khôi phục

Host miễn phí thường không có sao lưu tự động. Định kỳ: `BACKUP DATABASE ... COPY_ONLY` (hoặc `refresh-package.ps1` nếu còn DB cục bộ) và tải thư mục ảnh về. Khôi phục: `RESTORE DATABASE` từ `.bak` và chép lại thư mục ảnh. Bản `.bak` hiện tại đã được xác minh khôi phục được.

## 8. Việc chưa thể kiểm chứng trước khi có host thật

- Khả năng của host: khôi phục `.bak`, thư mục ghi được ngoài thư mục ứng dụng, thời điểm bật HTTPS.
- Host có để ứng dụng "ngủ" khi vắng khách (lần mở đầu chậm).
- Giới hạn RAM của gói miễn phí (ứng dụng chạy khoảng 150–250 MB).
