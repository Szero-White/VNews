# VNews + Cloudinary: ảnh nằm trên Cloudinary, website chạy trên Somee

## 1. Cách hoạt động

- Database **không đổi**. Mọi ảnh vẫn được lưu là `/uploads/articles/<32 ký tự>.<jpg|png|webp>` (ảnh bìa, ảnh trong bài, bộ sưu tập, HTML bài viết).
- Khi có cấu hình `Cloudinary:CloudName`, website đổi địa chỉ đó thành địa chỉ Cloudinary khi hiển thị:
  `https://res.cloudinary.com/<cloud>/image/upload/f_auto,q_auto/vnews/articles/<32 ký tự>`.
  Tên 32 ký tự chính là tên ảnh trên Cloudinary (public id `vnews/articles/<32 ký tự>`), nên không cần bảng ánh xạ.
- Địa chỉ cũ ở những chỗ khác (xem trước trong CMS, danh sách bài đã lưu) được website **chuyển hướng** sang Cloudinary.
- Khi có thêm `ApiKey` và `ApiSecret`, ảnh tải lên từ CMS (ảnh bìa, ảnh dán, ảnh trong bài) được lưu thẳng lên Cloudinary thay vì lên đĩa của Somee, sau khi qua các kiểm tra cũ (kích thước, loại tệp, nội dung thật).
- Không có `CloudName` thì website chạy y như trước (ảnh trong thư mục `uploads` của máy chủ). Muốn quay lại chỉ cần bỏ cấu hình đó. Không có gì bị xóa hay đổi trong database.

## 2. Cấu hình an toàn (API Secret không vào Git)

1. Trong Cloudinary Console lấy 3 giá trị: **Cloud name**, **API Key**, **API Secret** (mục *Settings → API Keys*). Nên tạo một API key riêng cho VNews để có thể thu hồi riêng.
2. Trên **máy chủ Somee**, tạo file `appsettings.Production.json` cạnh `VNews.Web.dll`, theo mẫu [appsettings.Production.example.json](appsettings.Production.example.json). File thật đã bị `.gitignore` chặn.
3. Chỉ điền API Secret trong file trên máy chủ. Không gửi cho tôi, không dán vào chat, không đưa vào Git, README hay ảnh chụp màn hình.
4. Website không đọc biến `CLOUDINARY_URL`. Biến đó chỉ dành cho công cụ chuyển ảnh ở mục 3, để một cửa sổ terminal giữ khóa của bạn không bao giờ làm test hay bản chạy local tải ảnh lên tài khoản thật.

Khi chạy local mà muốn thử Cloudinary, dùng user-secrets của .NET:
`dotnet user-secrets set "Cloudinary:ApiSecret" "<giá trị>" --project src/VNews.Web` (cũng không đi vào Git).

## 3. Chuyển 260 ảnh cũ lên Cloudinary (làm trên máy của bạn)

Công cụ nằm ở `tools/VNews.ImageMigrator`. Nó chỉ **đọc** thư mục ảnh gốc và database, không sửa, không xóa, không ghi đè ảnh nào, và không nằm trong solution nên build/test của website không đụng tới nó.

Mở PowerShell tại thư mục dự án:

```powershell
cd D:\VNews-Optimized-Claude-Code-Kit
$env:VNEWS_DB_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=VNewsDemo;Trusted_Connection=True;TrustServerCertificate=True;ApplicationIntent=ReadOnly'
```

**Bước 1: lập kế hoạch (chưa cần tài khoản Cloudinary, không gửi gì đi).**

```powershell
dotnet run --project tools\VNews.ImageMigrator -- plan --uploads artifacts\demo-uploads --report artifacts\cloudinary-migration\report
```

Kết quả mong đợi (đã chạy trên dữ liệu hiện tại): 260 ảnh, 244,8 MB, ảnh lớn nhất 2,94 MB, thiếu trên đĩa 0, sai đuôi tệp 0. Nếu có "Missing" khác 0 thì dừng lại. `plan.csv` liệt kê từng ảnh kèm SHA-256 và nơi dùng (bìa, bộ sưu tập, trong bài). `problems.txt` liệt kê ảnh trên đĩa mà database không dùng (không được chuyển).

**Bước 2: nhập khóa Cloudinary vào cửa sổ terminal của chính bạn (không hiện ra màn hình, không lưu vào lịch sử lệnh).** Lấy chuỗi *API environment variable* trong Console (dạng `cloudinary://KEY:SECRET@CLOUD`):

```powershell
$s = Read-Host 'CLOUDINARY_URL' -AsSecureString
$env:CLOUDINARY_URL = [System.Net.NetworkCredential]::new('', $s).Password
```

**Bước 3: thử 3 ảnh trước.**

```powershell
dotnet run --project tools\VNews.ImageMigrator -- upload --uploads artifacts\demo-uploads --report artifacts\cloudinary-migration\report --yes --limit 3
```

Mở Media Library, thư mục `vnews/articles`, thấy 3 ảnh là đạt.

**Bước 4: chuyển hết.** Chạy lại cùng lệnh nhưng bỏ `--limit 3`. Công cụ hỏi Cloudinary xem ảnh nào đã có, chỉ gửi ảnh chưa có. Nếu bị gián đoạn (mất mạng, tắt máy) cứ chạy lại đúng lệnh đó, ảnh đã có không bị gửi lại và không bao giờ bị ghi đè. Ảnh có kích thước khác với bản trên đĩa được báo "CONFLICT" và để nguyên.

**Bước 5: kiểm tra.**

```powershell
dotnet run --project tools\VNews.ImageMigrator -- verify --uploads artifacts\demo-uploads --report artifacts\cloudinary-migration\report
```

Kết quả cần thấy: `Missing: 0`, `Size differs: 0`, `Delivery URLs that open as an image: 260 of 260`. Chi tiết trong `verify.txt`.

**Bước 6: xóa khóa khỏi cửa sổ terminal.**

```powershell
Remove-Item Env:CLOUDINARY_URL; Remove-Item Env:VNEWS_DB_CONNECTION
```

Nhật ký từng ảnh nằm ở `artifacts\cloudinary-migration\report\state.jsonl` (không chứa khóa).

## 4. Đưa lên Somee

1. Tạo bản phát hành và dọn những thứ không được đưa lên máy chủ:

   ```powershell
   dotnet publish src\VNews.Web -c Release -o artifacts\somee-app
   Remove-Item artifacts\somee-app\appsettings.Development.json -Force                          # cấu hình máy bạn
   Remove-Item artifacts\somee-app\wwwroot\uploads -Recurse -Force                              # ảnh thử trên máy bạn (ảnh thật nằm trên Cloudinary)
   foreach ($r in 'linux-x64','osx-x64','osx-arm64','unix') { Remove-Item "artifacts\somee-app\runtimes\$r" -Recurse -Force }   # thư viện Linux/macOS
   ```

   Bản publish đo được 102,5 MB; bỏ các thư mục `runtimes` của Linux/macOS (tổng khoảng 54 MB) còn khoảng 49 MB, vừa giới hạn 150 MB của Somee (con số sau khi xóa là tính từ kích thước các thư mục, chưa đo lại). Giữ lại các thư mục `win-*` và `win`.
2. Tải nội dung `artifacts\somee-app` lên website, rồi tạo `appsettings.Production.json` trên máy chủ (mục 2). `ConnectionStrings:DefaultConnection` là chuỗi của database `vnewsdemo` trên Somee.
3. Khi Somee không cho ghi vào thư mục `uploads`, website vẫn khởi động bình thường (ảnh không dùng thư mục đó).
4. `DataProtection:KeysPath` cần một thư mục ghi được, xem [DEPLOY-HOSTING.md](../DEPLOY-HOSTING.md). Cookie đăng nhập yêu cầu HTTPS.

Kiểm tra sau khi lên:
- Trang chủ và một bài có ảnh trong bài: mở DevTools → Network, ảnh phải đến từ `res.cloudinary.com`.
- Tải một ảnh bìa mới trong CMS: ảnh xuất hiện trong Media Library tại `vnews/articles`.

## 5. Ghi chú và giới hạn

- Chưa được kiểm thử với tài khoản Cloudinary thật. Phần website được kiểm thử bằng test tự động (không mạng, có kho ảnh giả); công cụ chuyển ảnh đã chạy thật ở chế độ `plan`.
- Công cụ "Ảnh tải lên" trong CMS (tìm ảnh bìa không dùng) chỉ nhìn thấy đĩa của máy chủ, không thấy Cloudinary. Xóa bài hoặc xóa vĩnh viễn trong CMS không xóa ảnh trên Cloudinary.
- Có 5 nhóm ảnh trùng nội dung nhưng khác tên (do cùng một ảnh được tải nhiều lần). Mỗi tên vẫn được gửi một lần vì database tham chiếu theo tên, nên chúng chiếm dung lượng Cloudinary hai lần.
- Địa chỉ giao hàng dùng `f_auto,q_auto`: Cloudinary tự chọn định dạng và chất lượng nhẹ nhất. Đổi hoặc bỏ bằng `Cloudinary:DeliveryTransform` (để trống = ảnh gốc). Hãy xem mục *Usage* trong Console để biết hạn mức gói Free còn lại.
- Nếu Cloudinary ngừng hoạt động hoặc hết hạn mức, ảnh sẽ không hiển thị; chữ và dữ liệu bài viết không bị ảnh hưởng.
