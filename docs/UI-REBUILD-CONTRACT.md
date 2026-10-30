# UI Rebuild Contract — Header, Navigation, Hero, Tin phụ

Phạm vi: CHỈ 4 khu vực đầu trang chủ — masthead/header, navigation ngang danh mục, hero (top story) và các tin phụ đi kèm hero. Không đụng các section khác.

Phương pháp: đo DOM/CSS computed **thật** trên `https://vnexpress.net/` bằng `playwright-cli run-code` (JS eval trong trang thật, không suy đoán), ở 2 viewport 1440×900 và 390×844, ngày 2026-10-30. Ảnh chụp: `artifacts/visual/vnexpress-header-hero-desktop.png`, `vnexpress-header-hero-mobile.png`.

## 1. Masthead (desktop 1440px)

| Đo | VnExpress (thật) | VNews |
|---|---|---|
| Vùng masthead | `header.top-header`: y=270 (sau banner quảng cáo, loại trừ), h=62px, 1 hàng duy nhất (logo + vị trí/thời tiết + ngày + "Mới nhất" + Đăng nhập) | `.masthead`: h=73px, 1 hàng (logo+motto + ngày + utility link + Đăng nhập + tìm kiếm) |
| Logo | 173×51px | thương hiệu text "VNews" riêng, không dùng logo ảnh của VnExpress (đúng yêu cầu không sao chép) |
| Đăng nhập | text link góc phải, x=1125 | text link góc phải, đã có |

**Kết luận:** VnExpress hiện KHÔNG còn utility-bar tách riêng phía trên masthead (đã gộp 1 hàng) — khớp đúng cấu trúc VNews đã làm từ pass trước (gỡ `.utility-strip` riêng). Không cần sửa thêm.

## 2. Navigation ngang (desktop)

| Đo | VnExpress | VNews |
|---|---|---|
| Vùng nav | `section.wrap-main-nav` y=332 h=50px, ngay dưới masthead | `.primary-nav` y=73 h=48px, ngay dưới masthead |
| Số mục | 21 chuyên mục (có cuộn ngang `scroll-nav` + nút prev/next) | 10 chuyên mục (giới hạn bởi 10 category thật trong DB — không hardcode mục giả) |

**Giới hạn đã biết, không sửa:** số lượng category do dữ liệu backend quyết định (`CategoryQueryService`), không tạo category rỗng/giả chỉ để khớp số lượng — đúng rule "không hardcode khi không có dữ liệu thật".

## 3. Hero (top story)

| Đo | VnExpress (thật, `article-topstory`) | VNews (`hero-lead`) |
|---|---|---|
| Container | `.col-left-top` 780px trong container 1130px | `.front-page` 832px |
| Ảnh hero | float left, 520×312px = **780×66.7%**, tỉ lệ ảnh **5:3** (natural 680×408) | `.hero-lead__image-link img` 552×331px = **832×66.3%**, `aspect-ratio: 5/3` |
| Chữ | nằm bên phải ảnh (text wrap), không phải dưới ảnh | `.hero-lead__content` bên phải ảnh (CSS grid 2 cột) |

**Kết luận:** đã khớp đúng cấu trúc ảnh-trái/chữ-phải, tỉ lệ ảnh 5:3 và % chiều rộng ảnh gần như chính xác (66.3% vs 66.7%, lệch <1%). Không cần sửa.

## 4. Tin phụ đi kèm hero

| Đo | VnExpress (thật, `.sub-news-top` → `.list-sub-feature`) | VNews (`.hero-side`) trước/sau sửa |
|---|---|---|
| Vị trí | NGAY DƯỚI hero, trong cùng cột trái (không phải bên cạnh hero) | Giống — `.hero-side` nằm dưới `.hero-lead`, trong `.front-page__main` |
| Số cột | 3 cột, mỗi cột ~250px, gap **20px** (440−170−250=20) | 3 cột `grid-template-columns: repeat(3,1fr)`, gap **20px** — khớp chính xác |
| Cột 1–2 | ảnh 250×150 = tỉ lệ **5:3** | TRƯỚC: `aspect-ratio:16/9` (sai, 1.778 vs 1.667 thật) → **ĐÃ SỬA** thành `5/3` |
| Cột 3 ("Góc nhìn") | có ảnh nhỏ 72×72 bên cạnh tiêu đề (không chỉ text) | TRƯỚC: không có ảnh (chỉ text) → **ĐÃ SỬA**: thêm ảnh nhỏ 56×56 float-left (`.hero-side__opinion-image`) |
| Mobile (390px) | gộp vào dòng tin chung, không còn là khối 3-cột riêng biệt | VNews giữ khối riêng nhưng ẩn ảnh/mô tả, chỉ còn danh sách tiêu đề (đơn giản hoá tương đương, không sai lệch nội dung) |

## 5. Hero + tin phụ (mobile 390px)

| Đo | VnExpress | VNews |
|---|---|---|
| Ảnh hero | full-width 390×234 (tỉ lệ 5:3, xấp xỉ) | `aspect-ratio: 390/235` — khớp |
| Nav | hamburger (`.btn_control_menu` 46×46) mở drawer off-canvas | hamburger `.masthead__menu-toggle` mở `.primary-nav.is-open` — khớp hành vi |
| Không tràn ngang | — | `document.body.scrollWidth === window.innerWidth === 390` — xác nhận bằng Playwright |

## 6. Thay đổi code đã áp dụng

- `src/VNews.Web/wwwroot/css/editorial.css`: `.news-home--v3 .hero-side__image-link img` — `aspect-ratio: 16/9` → `5/3` (quy tắc có hiệu lực thật trên trang vì load sau `site.css`); thêm `.news-home--v3 .hero-side__opinion-image` (56×56, float left) + ẩn trên mobile.
- `src/VNews.Web/wwwroot/css/site.css`: đồng bộ `aspect-ratio` sang `5/3`; thêm ghi chú giải thích rule nào thắng (tránh nhầm lẫn khi sửa sau).
- `src/VNews.Web/Views/Shared/_ArticleCard.cshtml`: layout `TextOnly` (dùng riêng cho ô "Góc nhìn" trong tin phụ hero) — thêm `<img>` thật từ `CoverImageUrl` của bài viết (không phải ảnh giả), `aria-hidden`/`tabindex="-1"` vì đã có link tiêu đề riêng.

## 7. Kiểm chứng

- `dotnet build VNews.slnx` → Build succeeded (0 lỗi, chỉ cảnh báo NU1902/NU1903 đã biết từ trước).
- `dotnet test VNews.slnx` → **16/16 pass**.
- `npx playwright test` (visual-tests) → **12/12 pass** (bao gồm `homepage.spec.ts`, `home-parity.spec.ts`, `vnews-editorial-v2.spec.ts` — không có test nào riêng cho tỉ lệ ảnh hero-side, nên bổ sung đo đạc bằng `playwright-cli run-code` thủ công, xem mục 4).
- Đo trực tiếp qua `playwright-cli run-code`: `sideImg1` 265×159 = 5:3 (trước sửa là 16:9) → xác nhận fix có hiệu lực thật trên trang chạy; `opinionImg` 56×56 xuất hiện đúng vị trí; mobile 390px không tràn ngang; hamburger menu bấm thật → `.primary-nav` chuyển `is-open`, rộng 390px.
- Console lỗi JS: 0 (`playwright-cli console` → "Total messages: 0").

## 8. Khác biệt còn lại (không che giấu)

- Số mục nav (10 vs 21) — do giới hạn dữ liệu category thật, không sửa.
- VnExpress có "box-category" bên phải hero (320px, nội dung rỗng khi đo — có thể là vùng quảng cáo/động) — VNews không có khối tương đương, chủ động bỏ qua vì không rõ nội dung thật và không thuộc phạm vi "tin phụ".
- Mobile: khối tin phụ của VnExpress thật ra hoà vào dòng tin chung (không còn là khối 3-cột riêng) khi thu nhỏ; VNews chọn giữ khối riêng (chỉ còn danh sách tiêu đề) để không mất thông tin — một lựa chọn thiết kế hợp lý hơn là sai lệch, ghi nhận minh bạch.
