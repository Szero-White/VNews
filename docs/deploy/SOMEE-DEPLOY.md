# Triển khai VNews lên Somee Free (Windows Server 2022 / IIS)

Gói triển khai đã dựng sẵn: `artifacts\somee-deploy\app\` (thư mục) và `artifacts\somee-deploy\VNews-somee-app.zip` (cùng nội dung). Gói **không chứa** mật khẩu, connection string hay API Secret. Chúng nằm trong một file bạn tự tạo ở bước 2.

## 1. Gói gồm những gì

- Bản **Release**, `net10.0`, phụ thuộc framework (cần runtime .NET 10 trên máy chủ, xem mục 5), chạy **in-process** trong IIS.
- `web.config` do SDK tạo: `AspNetCoreModuleV2`, `processPath="dotnet"`, `hostingModel="inprocess"`, `stdoutLogEnabled="false"`.
- Đã loại: `appsettings.Development.json`, thư mục ảnh thử `wwwroot\uploads`, thư viện Linux/macOS trong `runtimes\` (`linux-x64`, `osx-x64`, `osx-arm64`, `unix`).
- **Giữ lại** các thư mục native/managed cho Windows trong `runtimes\` (`win`, `win-x64`, `win-x86`, `win-arm64`): `Microsoft.Data.SqlClient.SNI.dll` và `Microsoft.Data.SqlClient.dll` nằm ở đó, thiếu chúng thì không kết nối được SQL Server.
- `VNews.Web.runtimeconfig.json` đã đặt `System.GC.Server=false` và `System.GC.Concurrent=false` (bộ gom rác nhẹ, hợp máy chủ dùng chung). Mặc định của SDK là Server GC.

## 2. Thông tin bí mật bạn nhập riêng (không gửi qua chat)

Tạo file `appsettings.Production.json` theo mẫu [appsettings.Production.example.json](appsettings.Production.example.json) (bản mẫu cũng nằm cạnh gói: `artifacts\somee-deploy\appsettings.Production.example.json`). Các giá trị cần điền:

| Khóa | Lấy ở đâu |
|---|---|
| `ConnectionStrings:DefaultConnection` | Panel Somee, mục database `vnewsdemo` (server, tên đăng nhập, mật khẩu). Giữ `MultipleActiveResultSets=True;TrustServerCertificate=True` |
| `Cloudinary:CloudName` | Cloudinary Console |
| `Cloudinary:ApiKey`, `Cloudinary:ApiSecret` | Cloudinary Console, *Settings → API Keys* |
| `AllowedHosts` | `vnews-szero.somee.com` |
| `DataProtection:KeysPath` | mặc định mẫu là `App_Data\keys` (tính từ thư mục website; IIS không phục vụ `App_Data`). Cần thư mục ghi được, xem mục 5 |

Cách an toàn: tạo file này **trực tiếp trên máy chủ** (File Manager của Somee) rồi dán giá trị vào, hoặc soạn trên máy bạn rồi tải lên và **xóa bản trên máy**. Đừng đưa file đó vào Git, ZIP chia sẻ hay ảnh chụp màn hình. Nếu file thiếu hoặc sai, ứng dụng không dùng được database nào khác: chuỗi mặc định trỏ về LocalDB, nơi không tồn tại trên Somee, nên ứng dụng báo lỗi chứ không ghi nhầm dữ liệu.

## 3. Thay trang `Default.asp` mặc định của Somee

IIS xử lý tài liệu mặc định trước ứng dụng: nếu `Default.asp` còn ở thư mục gốc, địa chỉ `/` sẽ hiện trang đó thay vì VNews.

1. Vào File Manager hoặc FTP của Somee, mở thư mục gốc website (thường là thư mục chứa `Default.asp`; tên thư mục phụ thuộc Somee, thường là `www.root`).
2. **Sao lưu** các file mặc định về máy (`Default.asp` và các file khác Somee tạo sẵn), rồi xóa chúng khỏi thư mục gốc (kể cả `index.html`, `default.aspx` nếu có).
3. Tải toàn bộ nội dung `artifacts\somee-deploy\app\` vào thư mục gốc (hoặc tải ZIP rồi giải nén tại đó): `web.config` và `VNews.Web.dll` phải nằm ngay thư mục gốc, không nằm trong thư mục con.
4. Tạo `appsettings.Production.json` (mục 2).
5. Trong panel Somee, bảo đảm website dùng ASP.NET Core / .NET 10 (mục 5). Khởi động lại ứng dụng bằng cách tải lên (hoặc sửa nhẹ rồi lưu) `web.config`.
6. Mở `https://vnews-szero.somee.com`.

Cách quay lại: xóa nội dung vừa tải lên và đặt lại các file đã sao lưu ở bước 2.

## 4. Kiểm tra sau khi lên

- `/` trả trang VNews, 152 bài và 14 chuyên mục; mở DevTools → Network: ảnh đến từ `res.cloudinary.com`.
- Một bài có ảnh trong bài mở được; địa chỉ ảnh cũ `/uploads/articles/<id>.png` chuyển hướng sang Cloudinary.
- `/tai-khoan/dang-nhap` đăng nhập được bằng Admin cá nhân; `/cms` mở được sau khi đăng nhập.
- Sau khi đăng nhập, **tải ảnh bìa mới** trong CMS và xem ảnh xuất hiện trong Media Library tại `vnews/articles`.
- Đợi ứng dụng khởi động lại (hoặc khởi động lại thủ công) rồi thử đăng nhập lần nữa: nếu bị đăng xuất hoặc form báo lỗi, khóa đăng nhập chưa được lưu bền (xem mục 5).

## 5. Điều chưa xác minh được (không thể kiểm tra từ máy bạn)

1. **Runtime .NET 10 trên Somee.** Trang giới thiệu của Somee ghi `ASP.Net Core 3/5/6/7/8/9/10`, nhưng FAQ của họ ghi tới .NET 9. Nếu ứng dụng báo lỗi 500.31 / 500.30 / "framework not found", máy chủ chưa có runtime .NET 10. Phương án dự phòng: bản **self-contained** `win-x64` (đo thử được **145,3 MB**, sát giới hạn 150 MB và chỉ chạy nếu pool 64-bit). Tôi chưa dựng sẵn bản này; cần thì báo tôi.
2. **Pool 32-bit hay 64-bit** của Somee chưa biết. Gói phụ thuộc framework chứa cả `win-x86` và `win-x64`, nên chạy được ở cả hai, miễn là runtime .NET 10 cùng bitness có trên máy chủ.
3. **Thư mục ghi được cho DataProtection.** Nếu `App_Data\keys` không ghi được, đăng nhập và các form sẽ lỗi sau mỗi lần khởi động lại, và có thể lỗi ngay ở trang có form. Thử thư mục khác mà panel cho phép ghi và đặt đường dẫn tuyệt đối vào `DataProtection:KeysPath`.
4. **Bộ nhớ (RAM).** Diễn tập cục bộ (Production, HTTPS, Workstation GC) cho thấy tiến trình dùng khoảng 204 MB working set, 101 MB private sau khi mở trang chủ, 12 bài, tìm kiếm, API. Hạn mức RAM của Somee Free tôi chưa biết; nếu ứng dụng bị khởi động lại liên tục, bộ nhớ là nghi phạm đầu tiên.
5. **HSTS và HTTPS thật.** Cục bộ HSTS không gửi cho `localhost`. Chuyển hướng HTTP sang HTTPS và HSTS chỉ kiểm chứng được trên chính tên miền Somee.
6. **Kết nối database Somee.** Chuỗi kết nối cần `TrustServerCertificate=True` (đã có trong mẫu). Tôi không kết nối được tới Somee nên chưa thử thật.
7. **Upload ảnh mới lên Cloudinary bằng khóa thật** chưa được thử (chỉ có test với kho ảnh giả).
8. **Bật log để tìm lỗi khởi động** (khi cần): đổi `stdoutLogEnabled="true"` trong `web.config` và tạo thư mục `logs` ghi được; nhớ tắt lại sau khi xong.

## 6. Sau khi tải lên và chạy ổn

Tôi chưa commit hay push gì; các thay đổi (Cloudinary, đường dẫn tương đối của `DataProtection:KeysPath`, tài liệu) còn nằm trên nhánh `feature/cloudinary-images`.

## 7. Bốn nút đăng nhập nhanh trên Somee (DemoAuth)

Bản local có 4 nút (Quản trị viên, Biên tập viên, Tác giả, Độc giả). Trên Somee chúng chỉ bật khi thêm vào `appsettings.Production.json`:

```json
"DemoAuth": { "Enabled": true, "AllowPublicHost": true }
```

- `AllowPublicHost` mặc định tắt; bật thì cổng demo bỏ điều kiện "Development" và "yêu cầu đến từ localhost", nhưng **vẫn bắt buộc tên database bắt đầu bằng `VNewsDemo`** (database Somee `vnewsdemo` thỏa điều kiện; trỏ nhầm sang database khác thì nút tự biến mất).
- Lần đầu bấm mỗi nút, ứng dụng tự sửa đúng tài khoản `demo.*@demo.vnews.local` tương ứng: mở khóa và đặt lại đúng một vai trò. Không động tới tài khoản khác. Cần SQL thủ công thì dùng `demo-accounts-enable.sql` (chưa chạy); tắt demo thì dùng `demo-accounts-disable.sql` và đặt `Enabled` về `false`.
- **Rủi ro:** các nút dùng quyền thật. Khách bấm "Quản trị viên" có thể sửa/xóa bài, đổi chuyên mục, khóa hoặc đổi vai trò tài khoản khác (kể cả Admin cá nhân của bạn), chạy nhập RSS và tải ảnh lên Cloudinary của bạn. Hãy giữ bản sao lưu `.bak` và biết cách khôi phục (xem phần cập nhật an toàn trong báo cáo); cân nhắc tắt demo khi không cần.
