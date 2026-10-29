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

## Khác biệt còn tồn tại (báo cáo trung thực, chưa sửa)
- Masthead VNews đơn giản hơn tham chiếu (thiếu dropdown khu vực, biểu tượng thời tiết, mục "Đăng nhập"/chuông thông báo) — vì các chức năng đó (đăng nhập, thông báo) thuộc M3 (CMS/Identity), chưa làm giả UI cho chức năng chưa tồn tại.
- Thanh chuyên mục chưa có icon "VnE-GO" dạng pill hay nút hamburger cuối hàng như tham chiếu — mức độ ưu tiên thấp, không ảnh hưởng chức năng.
- Ảnh bài viết là đồ hoạ gradient gốc (không phải ảnh chụp thật) — đúng chủ đích vì không được phép dùng ảnh có bản quyền của VnExpress; đây là lựa chọn thiết kế, không phải lỗi.
- Chưa kiểm tra tablet 768px bằng screenshot thực tế (chỉ có CSS responsive khai báo breakpoint `max-width: 1024px`), nên chưa xác nhận trực quan ở độ phân giải này.
