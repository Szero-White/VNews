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

## Pass D (2026-10-29) — bộ đo Playwright Test + sửa bố cục/font/ảnh theo yêu cầu đo bounding box

Bối cảnh: trước pass này đã có chỉnh sửa thủ công đáng kể (xem `_ui_backup/`) đổi hero sang ảnh trái/chữ phải thật, tin phụ 3 cột bên dưới (giống reference), masthead 2 tầng (logo+motto / tiện ích). Pass này: (1) dựng bộ đo Playwright Test chính thức, (2) đo bounding box thật từ `references/vnexpress-desktop.png` bằng phân tích pixel (không ước lượng bằng mắt), (3) sửa các lệch còn lại, (4) phát hiện và sửa 2 lỗi hiển thị nghiêm trọng phát sinh ngoài dự kiến.

### Công cụ đo: `visual-tests/` (Playwright Test, Node)
- `analyze-reference.js`: load `references/vnexpress-desktop.png` qua `file://`, vẽ vào canvas, quét pixel dọc/ngang để tìm biên vùng (masthead, nav, hero) dựa trên chênh lệch màu RGB > ngưỡng. Loại trừ quảng cáo: quét xác nhận banner quảng cáo chiếm y=0-270px trong ảnh reference, **loại khỏi mọi số đo mục tiêu** (chỉ dùng chiều cao tương đối masthead/nav, không dùng toạ độ Y tuyệt đối).
- `homepage.spec.ts`: 3 test — đo bounding box desktop (`boundingBox()` qua CSS selector thật: `.masthead`, `.primary-nav`, `.hero-lead__image-link img`, `.hero-lead__content`, `.hero-side`) so với bảng mục tiêu, dung sai 12px; kiểm tra không tràn ngang + không ảnh vỡ ở 390px và 768px.

### Bảng mục tiêu (từ pixel reference) vs thực tế (sau sửa)
| Vùng | Mục tiêu (từ reference, đã loại ad) | Thực tế VNews | Lệch | Đạt (≤12px) |
|---|---|---|---|---|
| Chiều cao masthead | 61px | 73px | 12px | ✅ (đúng ngưỡng) |
| Chiều cao nav | 50px | 48px | 2px | ✅ |
| Y bắt đầu nội dung chính (sau masthead+nav) | 111px | 121px | 10px | ✅ |
| Chiều rộng ảnh hero | 552px | 552px | 0px | ✅ |
| Tỉ lệ khung ảnh hero | 1.67 (5:3) | 1.67 | 0 | ✅ |
| Khoảng cách ảnh↔chữ hero | 24px | 24px | 0px | ✅ |
| Tin phụ nằm dưới hero (không phải cột phải) | bắt buộc | đúng, y=494 ≥ 474 (đáy hero row) | — | ✅ |

Dữ liệu thô: `artifacts/visual/layout-measurements.json`.

### Thay đổi để đạt bảng trên
- **Gỡ `.utility-strip`** (thanh ngày/disclaimer riêng biệt cao 33px nằm trên masthead) — nguyên nhân chính khiến `contentTopY` lệch 43px so với mục tiêu. Reference không có thanh này (thông tin ngày/vị trí nằm chung hàng với logo). Nội dung disclaimer vốn đã trùng lặp với dòng đầu tiên trong `<footer>` nên không mất thông tin. Ngày hiện tại (`#today-date`) chuyển vào `.masthead__right`, cùng hàng với "Đăng nhập"/tìm kiếm — khớp đúng mẫu reference.
- Sau khi gỡ utility-strip: `contentTopY` giảm từ 154px xuống 121px (còn lệch 10px, trong ngưỡng).
- Sửa 1 bug CSS phát sinh: selector `.masthead__utility-link:first-child` (dùng để ẩn bớt mục trên mobile) không còn trúng đúng phần tử sau khi chèn thêm ngày — gây tràn ngang 7px ở 390px. Thay bằng class tường minh `.masthead__hide-mobile`.

### 2 lỗi hiển thị nghiêm trọng phát hiện ngoài phạm vi bố cục, đã sửa vì ảnh hưởng trực tiếp "không lỗi hiển thị nghiêm trọng"
1. **Chữ tiếng Việt vỡ dấu** trên nhiều tiêu đề (vd. "mở rộng đô´i tượng áp dụng", "tuyên truyê`n pháp luật", "Nhiê`u trường đại học..."). Xác minh nguyên nhân bằng thực nghiệm: ép `font-family: Arial` lên các tiêu đề qua `page.addStyleTag` → chữ hiển thị đúng hoàn toàn → xác nhận 100% do font `Georgia, "Times New Roman", serif` (dùng cho mọi tiêu đề) không dựng đúng một số tổ hợp dấu tiếng Việt trên Chromium/máy này. Sửa: thay toàn bộ 6 khai báo `font-family: Georgia...` trong `site.css` bằng `Arial, "Helvetica Neue", Helvetica, sans-serif`.
2. **Ảnh trùng lặp**: nhiều bài trong cùng chuyên mục dùng chung đúng 1 ảnh (do M1 gán ảnh theo chuyên mục, không theo bài). Sửa: tải 51 ảnh JPEG riêng biệt (Picsum/Unsplash License, seed theo từng `Article.Id`) vào `wwwroot/images/demo/articles/{id}.jpg`; UPDATE có chọn lọc `CoverImageUrl` cho đúng 51 bài gốc (loại trừ 8 bài rác do `dotnet test` tạo lặp qua nhiều lần chạy — đã xoá 8 bài rác này, không đụng 51 bài demo thật); cập nhật `ArticleSeedData.cs` dùng `counter` (thứ tự bài) làm seed ảnh cho lần seed sau. Xoá 10 ảnh cấp-chuyên-mục cũ.

### Kết quả cuối — ảnh chụp thật (Playwright Test)
`artifacts/visual/vnews-desktop.png`, `vnews-mobile.png`, `vnews-tablet.png` (dùng `waitForFunction` chỉ chờ ảnh trong viewport ban đầu tải xong trước khi chụp, tránh chụp nhầm lúc ảnh chưa decode xong — đã từng gây hiện tượng "ảnh vỡ" giả do lỗi timing trong chính kịch bản đo, không phải lỗi thật của site, đã xác minh lại bằng `img.naturalWidth`/`img.complete` trước khi kết luận).

### Khác biệt còn tồn tại (báo cáo trung thực, chưa sửa)
- Masthead VNews đơn giản hơn tham chiếu (thiếu dropdown khu vực, biểu tượng thời tiết, chuông thông báo) — mục "Đăng nhập" đã có thật qua M3.
- Thanh chuyên mục chưa có icon "VnE-GO" dạng pill hay nút hamburger cuối hàng như tham chiếu — mức độ ưu tiên thấp, không ảnh hưởng chức năng.
- Ảnh minh hoạ vẫn là ảnh stock (Picsum), nay đã **không trùng lặp** nhưng vẫn không khớp ngữ cảnh cụ thể từng bài (không có nguồn ảnh biên tập thật để dùng).
- `mastheadHeight` lệch đúng 12px (ở biên ngưỡng) — có thể siết thêm nếu cần tuyệt đối khớp, nhưng rủi ro làm vỡ bố cục `.masthead__identity` 2 dòng (logo+motto); chưa làm vì đã đạt tiêu chí ≤12px.
- Trang chi tiết bài viết (`/tin/{slug}`) **chưa được đo lại** trong pass này (phạm vi yêu cầu chỉ giới hạn trang chủ); ảnh `vnews-article-desktop.png` trong thư mục artifacts là ảnh cũ từ phiên trước, có thể không phản ánh đúng font Arial mới.
