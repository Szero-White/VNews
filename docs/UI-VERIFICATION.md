# VNews — Visual verification log (visual-match)

Baselines: `references/vnexpress-desktop.png` (1440x900, captured 2026-10-29), `references/vnexpress-mobile.png` (390x844).
Local: `artifacts/visual/vnews-desktop.png`, `artifacts/visual/vnews-mobile.png`.

## Pass A findings (before fix) — desktop

| # | Reference | VNews (before) | Change |
|---|---|---|---|
| 1 | Hero: ảnh lớn bên trái, tiêu đề/tóm tắt nằm **ngay bên phải ảnh** (hàng ngang) | Ảnh chiếm full-width phía trên, tiêu đề/tóm tắt nằm **dưới ảnh** (xếp dọc) | Đổi `.hero-lead` sang flex row: ảnh trái ~48%, nội dung phải |
| 2 | Ảnh bài báo là ảnh biên tập thật (photo), đa dạng | Ảnh là khối màu phẳng + chữ "VNews" khổng lồ lặp lại ở mọi ảnh — trông như ảnh vỡ/placeholder lỗi | Vẽ lại 10 SVG: gradient + hoạ tiết hình học, bỏ chữ "VNews" lặp lại, chỉ còn nhãn chuyên mục nhỏ góc dưới |
| 3 | Masthead: logo + tagline, ô tìm kiếm, khu vực/ngày giờ, dải tiện ích trên cùng | Đã có dải tiện ích (ngày + tagline) + masthead (logo + tìm kiếm) — cấu trúc 2 tầng tương tự | Giữ nguyên (đã gần đúng), bổ sung tagline nhỏ dưới logo ở pass sau nếu còn thời gian |
| 4 | Nav chuyên mục: nền xám nhạt, 1 hàng ngang, viền dưới | Đã có nền xám nhạt `--vn-surface`, 1 hàng ngang, viền dưới | Giữ nguyên, ưu tiên thấp |
| 5 | Card chuyên mục bên dưới: ảnh + tiêu đề, lưới đều | Đã có cấu trúc tương tự nhưng dùng ảnh placeholder vỡ (cùng lỗi #2) | Sửa cùng lúc với #2 |

## Pass A actions
- `src/VNews.Web/Views/Home/Index.cshtml`: hero-lead đổi bố cục hàng ngang (ảnh trái, nội dung phải).
- `src/VNews.Web/wwwroot/css/site.css`: thêm `.hero-lead` flex layout, điều chỉnh responsive stack lại thành cột ở mobile.
- `src/VNews.Web/wwwroot/images/demo/*.svg`: vẽ lại toàn bộ 10 ảnh minh hoạ theo phong cách biên tập (gradient + hoạ tiết), bỏ chữ lặp, giữ nhãn chuyên mục nhỏ.

Kết quả sau pass A: `artifacts/visual/vnews-desktop-pass-a.png` — hero đã chuyển đúng bố cục ảnh trái/chữ phải, ảnh minh hoạ không còn trông vỡ, nhưng cột phải (2 tin phụ) cao hơn hẳn cột hero trái → để lại khoảng trắng.

## Pass B — cân bằng chiều cao cột hero
- `align-items: start` cho `.hero-grid` (tránh bị kéo giãn theo cột cao nhất).
- Giảm `aspect-ratio` ảnh `.hero-side__item img` từ 16:9 xuống 21:9 để rút ngắn cột phải, cân đối hơn với cột hero trái.
- Kết quả: `artifacts/visual/vnews-desktop-pass-b.png`, ảnh cuối cùng `artifacts/visual/vnews-desktop.png` — khoảng trắng giảm rõ rệt, bố cục cân đối hơn.

## Kết quả cuối (đã xác nhận bằng Playwright CLI thật)
- `artifacts/visual/vnews-desktop.png` (1440x900, trang chủ): hero ảnh trái + tiêu đề/tóm tắt phải, nhãn chuyên mục in hoa màu accent, 2 tin phụ bên phải cân đối hơn, card chuyên mục dùng ảnh gradient biên tập (không còn chữ "VNews" lặp lại vỡ hình).
- `artifacts/visual/vnews-mobile.png` (390x844, trang chủ): menu thu gọn thành icon hamburger, hero xếp dọc đúng, không tràn ngang, đọc được.
- `artifacts/visual/vnews-article-desktop.png` (1440x900, trang chi tiết bài): breadcrumb, tiêu đề, tóm tắt, meta (tác giả/ngày/lượt xem — lượt xem tăng thật khi tải lại, chứng tỏ dữ liệu từ SQL Server), ảnh bìa, nội dung — rõ ràng, chuyên nghiệp.
- `playwright-cli console` trên trang chủ: 0 lỗi, 0 cảnh báo. `playwright-cli requests`: không có request 404/lỗi.
- Baseline tham chiếu cố định: `references/vnexpress-desktop.png`, `references/vnexpress-mobile.png` (chụp 2026-10-29 bằng Playwright CLI thật từ vnexpress.net, không chỉnh sửa).

## Pass C (2026-10-29) — thay ảnh gradient bằng ảnh báo chí thật
Người dùng yêu cầu rõ: không chấp nhận 10 ảnh gradient placeholder thay cho ảnh chất lượng báo chí. Đã xử lý:
- Tải 10 ảnh chụp thật (JPEG, 800x600) từ **Picsum Photos** (`https://picsum.photos/seed/vnews-{category}/800/600`) — nguồn ảnh cấp phép theo **Unsplash License** (miễn phí sử dụng kể cả thương mại, không bắt buộc ghi nguồn, xem `https://picsum.photos` + `https://unsplash.com/license`), hợp lệ với yêu cầu "ảnh được phép sử dụng" trong CLAUDE.md. Dùng seed cố định theo từng slug chuyên mục để có thể tải lại y hệt (reproducible).
- Thay `src/VNews.Web/wwwroot/images/demo/{slug}.svg` (khối màu phẳng) bằng `{slug}.jpg` (ảnh thật) cho cả 10 chuyên mục; xoá các file `.svg` cũ.
- Cập nhật `ArticleSeedData.cs` để các lần seed mới trỏ `.jpg`.
- **Cập nhật có chọn lọc dữ liệu đã seed** bằng `UPDATE Articles SET CoverImageUrl = REPLACE(CoverImageUrl, '.svg', '.jpg') WHERE CoverImageUrl LIKE '/images/demo/%.svg'` trên SQL Server LocalDB thật (ảnh hưởng đúng 50 dòng đã seed từ trước, không đụng tới cấu trúc bảng, user, hay bài viết khác) — không drop/reset database.
- Xác nhận bằng HTTP thật: `/images/demo/cong-nghe.jpg` → 200, `Content-Type: image/jpeg`. Chụp lại `artifacts/visual/vnews-desktop-v2-photos.png` và `vnews-mobile-v2-photos.png` qua Playwright CLI — ảnh hiển thị là ảnh chụp thật (phong cảnh, nội thất tàu điện...), không còn khối màu phẳng, trông giống một trang báo thật hơn hẳn.
- **Giới hạn trung thực:** ảnh là ảnh stock ngẫu nhiên theo seed, KHÔNG minh hoạ đúng nội dung từng bài viết cụ thể (vd. bài về AI báo chí hiện ảnh phong cảnh núi) — chấp nhận được cho mục đích demo/portfolio, tương tự cách nhiều trang demo dùng ảnh stock, nhưng không phải ảnh biên tập thật sự khớp ngữ cảnh bài viết.

## Khác biệt còn tồn tại (báo cáo trung thực, chưa sửa)
- Masthead VNews đơn giản hơn tham chiếu (thiếu dropdown khu vực, biểu tượng thời tiết, chuông thông báo) — các mục đăng nhập/tài khoản nay đã có thật qua M3 (`/tai-khoan/dang-nhap`) nhưng chưa gắn link vào masthead công khai.
- Thanh chuyên mục chưa có icon "VnE-GO" dạng pill hay nút hamburger cuối hàng như tham chiếu — mức độ ưu tiên thấp, không ảnh hưởng chức năng.
- Ảnh minh hoạ là ảnh stock ngẫu nhiên theo chuyên mục, không khớp ngữ cảnh từng bài cụ thể (xem Pass C ở trên).
- Chưa kiểm tra tablet 768px bằng screenshot thực tế (chỉ có CSS responsive khai báo breakpoint `max-width: 1024px`), nên chưa xác nhận trực quan ở độ phân giải này.
