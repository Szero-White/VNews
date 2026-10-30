# VNews editorial update — phạm vi và kiểm tra

## Thay đổi (source C# / Razor / CSS / JavaScript)
- Trang chủ có nhiều dạng section biên tập: tin chính, dòng tin, tin trong ngày, chuyên mục xen kẽ, góc ảnh, spotlight, đọc & nghe.
- Trang `tin-moi-nhat`, `da-luu` (lưu trên trình duyệt), chi tiết bài có đọc tóm tắt bằng Web Speech API khi trình duyệt hỗ trợ, chia sẻ và tăng giảm cỡ chữ.
- Danh sách cùng chuyên mục được truyền bằng `ArticleDetailPageViewModel` (không dùng ViewBag).
- Bỏ sáu dạng card không có view nào gọi; các layout còn lại dùng enum `ArticleCardLayout` (hạn chế typo).
- Bỏ CSS cũ chỉ dành cho card không còn sử dụng, chuyển phần giao diện mới sang `wwwroot/css/editorial.css` để dễ duy trì; không xóa CMS/API.
- Tin hero, stream và danh sách tin trong ngày được phân trang logic để tránh lặp bài ngay trên đầu trang.
- Nếu bài mới không có ảnh bìa, hiển thị `wwwroot/images/demo/fallback.svg` thay vì ảnh vỡ.

## Không thay thế sản phẩm gốc
VNews là một ứng dụng demo độc lập, tham khảo các mô-típ bố cục báo điện tử. Không sử dụng logo/nội dung gốc của VnExpress và không tuyên bố sao chép chính xác 100%.

## Kiểm tra bắt buộc trên Windows
1. Đóng phiên dev server do chính dự án mở để tránh lỗi khoá DLL.
2. `dotnet build VNews.slnx`
3. `dotnet test VNews.slnx`
4. Chạy `dotnet run --project .\src\VNews.Web\VNews.Web.csproj --urls http://localhost:5299` (PowerShell đặt `ASPNETCORE_ENVIRONMENT=Development` nếu cần).
5. Kiểm tra trang chủ, `/tin-moi-nhat`, `/da-luu`, `/tim-kiem?q=cong%20nghe`, trang bài viết bất kỳ, chuyển chuyên mục, đăng nhập `/cms` và luồng CMS.
6. Mở bài viết → Lưu bài → truy cập `/da-luu` → Bỏ lưu. Kiểm tra chia sẻ/tăng giảm cỡ chữ/giọng đọc với Chrome/Edge.
7. So sánh ảnh full-page desktop 1440px và mobile 390px bằng Playwright; không coi HTTP 200 là xác nhận đúng giao diện.

## Giới hạn phải xử lý trước production
- Phiên bản này chưa được build .NET/kiểm chứng SQL Server trong môi trường xử lý artifact; phải chạy hai lệnh trên máy Windows.
- Các test integration đang dùng SQL Server LocalDB và có thể để lại dữ liệu kiểm thử, cần test DB cô lập trước CI production.
- Warning NU1902/NU1903 từ System.Security.Cryptography.Pkcs cần cập nhật dependency/patched version, không bỏ qua khi phát hành.
- Tài khoản CMS demo dùng mật khẩu công khai chỉ phù hợp máy local, tuyệt đối không công khai ra Internet.
- Dữ liệu ảnh/bài demo chưa tạo trải nghiệm biên tập thực sự như một tòa soạn; cần seed nội dung đúng chủ đề và kiểm tra license hình ảnh.
- Việc lưu bài hiện chỉ dùng localStorage, không đồng bộ tài khoản; Web Speech API không bảo đảm giọng vi-VN trên mọi thiết bị.
