# VNews ↔ VnExpress — Parity Matrix

Khảo sát thực hiện 2026-10-29 bằng Playwright (playwright-cli tương tác + script Node dùng `@playwright/test` để trích xuất DOM/pixel). Ảnh chụp lưu tại `artifacts/survey/`. Không sửa code trong lượt khảo sát này.

**Phạm vi đã khảo sát:**
- VnExpress: trang chủ (full-page, desktop 1440px), trang chuyên mục (`/thoi-su`), trang chi tiết bài viết (full-page), trang tìm kiếm (overlay + kết quả).
- VNews: trang chủ (full-page, desktop 1440px **và** mobile 390px), trang chuyên mục (`/chuyen-muc/thoi-su`), trang chi tiết bài viết (full-page), trang tìm kiếm (`/tim-kiem`), trang chủ đề (`/chu-de/{slug}`).
- Loại trừ khỏi so sánh: quảng cáo (banner trên cùng VnExpress, quảng cáo sidebar trang tìm kiếm), nội dung động/cá nhân hoá (thời tiết, số bình luận thời gian thực, bài viết trending theo thời điểm).

---

## 1. Toàn bộ section trang chủ VnExpress, theo thứ tự từ trên xuống dưới

Đo bằng trích xuất DOM (`getBoundingClientRect`) trên trang chủ thật, full-page 1440×10550px.

| # | Y (px) | Section | Nội dung |
|---|---|---|---|
| 1 | 0 | Utility bar (top-header) | Vị trí (TP HCM), thời tiết, ngày, "Mới nhất / Tin theo khu vực / International", Đăng nhập, chuông thông báo |
| 2 | 62 | Masthead + Main nav | Logo VnExpress, tagline, ô tìm kiếm (icon → overlay), danh mục ngang (VnE-GO, Thời sự, Thế giới, Kinh doanh, Khoa học công nghệ, Góc nhìn, Spotlight, Bất động sản, Sức khỏe, Giải trí, Thể thao, Pháp luật, Giáo dục, Đời sống, Xe, Du lịch, Ảnh, Infographic, Ý kiến, Tâm sự, Thư giãn, Tất cả) |
| 3 | 112 | Top story (hero) | 1 tin chính lớn (ảnh trái, tiêu đề/tóm tắt phải) + lưới "wrap-all-menu" tin liên quan |
| 4 | 710–3434 | Stream home (dòng tin tổng hợp) | Chuỗi dài các `article.item-news.full-thumb` xen kẽ nhiều chuyên mục khác nhau, mỗi tin ảnh lớn + tiêu đề — không phân theo chuyên mục rõ ràng, giống "feed" tổng hợp |
| 5 | 3434 | Khoa học công nghệ | Khối chuyên mục riêng: tiêu đề khối + danh sách bài |
| 6 | 4183 | Khối đặc biệt (không tên) | Widget/khối quảng cáo hoặc nội dung đối tác — chưa xác định rõ nội dung |
| 7 | 4577 | Podcasts | Khối giới thiệu podcast |
| 8 | 4847 | Thời sự | Khối chuyên mục Thời sự |
| 9 | 6036 | Xe | Khối chuyên mục Xe |
| 10 | 6552 | Du lịch | Khối chuyên mục Du lịch |
| 11 | 7108 | Tâm sự | Khối chuyên mục Tâm sự (lifestyle) |
| 12 | 7809 | Ảnh | Khối ảnh/photo story |
| 13 | 8410 | Spotlight | Khối nội dung nổi bật, dạng card lớn + swiper carousel |
| 14 | 10082 | Footer | Xem chi tiết mục 2 bên dưới |

**Chi tiết Masthead (mục 2):** `TP HCM | Mặc định | 33° | Thời tiết từng địa phương | Thứ năm, 29/10/2026 | Mới nhất | Tin theo khu vực | International | Đăng nhập`

**Chi tiết Footer (mục 14):** 4 cột link chuyên mục + cột "Mới nhất/Xem nhiều/Tin nổi bật/Lịch vạn niên/Rao vặt/Tải ứng dụng" + cột liên hệ (toà soạn, quảng cáo, điện thoại) + dòng pháp lý (giấy phép, tổng biên tập, địa chỉ, © bản quyền) + liên kết mạng xã hội + Điều khoản/Chính sách bảo mật/Cookies/RSS.

**Ghi chú:** Mục 4 ("stream home") là phần chiếm nhiều diện tích nhất (710→3434px, ~2700px) nhưng không có cấu trúc rõ ràng theo chuyên mục — cần quan sát thêm nếu muốn clone chính xác (có thể là "Tin mới nhất" liên chuyên mục dạng lớn, khác với layout list nhỏ của VNews).

---

## 2. Toàn bộ section trang chủ VNews hiện tại, theo thứ tự từ trên xuống dưới

Full-page 1440×5152px (`artifacts/survey/vnews-home-desktop-full.png`).

| # | Section | Nội dung | Khớp mẫu VnExpress? |
|---|---|---|---|
| 1 | Masthead | Logo + motto, ngày, "Tin mới nhất", Đăng nhập, tìm kiếm (icon) | Gần khớp mục 1+2 VnExpress đã gộp làm 1 hàng |
| 2 | Nav | 10 chuyên mục ngang | Khớp cấu trúc, ít chuyên mục hơn (10 vs ~20) |
| 3 | Hero + 3 tin phụ | 1 tin chính (ảnh trái/chữ phải) + 3 cột tin phụ bên dưới (2 có ảnh, 1 dạng "Được quan tâm" không ảnh) | Khớp bố cục hero; VnExpress không có khối 3 cột ngay dưới hero theo cách này |
| 4 | Tin mới nhất + Đọc nhiều | List 2 cột: danh sách 10 bài (trái) + top 5 đọc nhiều dạng số thứ tự (phải) | Không có tương đương trực tiếp ở VnExpress (gần giống phần "stream home" nhưng nhỏ hơn nhiều) |
| 5 | 4 khối chuyên mục | "Thời sự", "Kinh doanh", "Thế giới", "Công nghệ" — mỗi khối: tiêu đề + "Xem thêm" + lưới 3 card (ảnh + tiêu đề) | Khớp đúng mẫu khối chuyên mục VnExpress (mục 5, 8, 9, 10 reference) |
| 6 | Footer | 2 dòng text (disclaimer + copyright) | Rất sơ sài so với footer 4-cột đầy đủ của VnExpress |

**Tổng: 6 nhóm section lớn ở VNews so với 14 ở VnExpress.**

---

## 3. Bảng Parity Matrix chi tiết

| Thành phần | VnExpress có gì | VNews đã có gì | Còn thiếu gì | Ưu tiên | Cách nghiệm thu |
|---|---|---|---|---|---|
| **Utility bar** | Vị trí, thời tiết, ngày, link "Mới nhất/Tin theo khu vực/International", đăng nhập, chuông thông báo | Đã gộp vào masthead: ngày + "Tin mới nhất" + đăng nhập + tìm kiếm | Vị trí/thời tiết (phụ thuộc API ngoài, không cần thiết cho portfolio), chuông thông báo, "Tin theo khu vực"/"International" | Thấp | Xem bằng mắt, không cần API thời tiết thật |
| **Masthead/logo** | Logo + tagline, 1 hàng cố định | Logo + motto, 1 hàng | Đạt tương đương | — | Đã nghiệm thu ở pass trước (≤12px) |
| **Nav chuyên mục** | ~20 chuyên mục + "Tất cả", icon tròn "VnE-GO" đầu hàng, cuộn ngang | 10 chuyên mục, icon nhà (Home) đầu hàng | Thiếu các chuyên mục: Góc nhìn, Spotlight, Bất động sản, Đời sống, Ảnh, Infographic, Ý kiến, Tâm sự, Thư giãn, "Tất cả" | Trung bình | So khớp danh sách nav, kiểm tra cuộn ngang mobile không vỡ |
| **Hero trang chủ** | Ảnh trái/chữ phải, tin liên quan dạng lưới ngay dưới | Ảnh trái/chữ phải, 3 cột tin phụ dưới | Đã khớp bố cục cơ bản (đo pixel pass trước) | — | Đã nghiệm thu |
| **Khối "stream" tổng hợp** | Chuỗi dài tin ảnh lớn xen nhiều chuyên mục (~2700px) | Không có — thay bằng "Tin mới nhất" dạng list nhỏ 2 cột | Thiếu hẳn dạng hiển thị "ảnh lớn xen kẽ" này | Trung bình | So sánh trực quan 1 khối mẫu, không cần pixel-perfect |
| **Khối chuyên mục (Thời sự/Xe/Du lịch/...)** | Tiêu đề khối + danh sách bài, có khối xen podcast/ảnh/spotlight đặc biệt | 4 khối chuyên mục dạng lưới 3 card + "Xem thêm" | Số lượng khối ít hơn (4 vs 8+), thiếu các khối đặc biệt (Podcasts, Ảnh, Spotlight) | Thấp (khối đặc biệt), Trung bình (thêm khối chuyên mục) | Đếm số khối, kiểm tra "Xem thêm" dẫn đúng trang chuyên mục |
| **Trang chuyên mục** | Tiêu đề to + sub-tab chuyên mục con (Chính trị/Dân sinh/...), lead story lớn (ảnh trái/chữ phải) + lưới 3 cột bên dưới | List đơn giản 1 cột (ảnh trái nhỏ + tiêu đề + tóm tắt + tác giả/ngày), có phân trang (`_Pagination`, chưa thấy kích hoạt vì ít bài) | Thiếu: sub-category tab, lead story lớn đầu trang, lưới nhiều cột | Cao | Vào 1 trang chuyên mục, so cấu trúc: có lead story + grid dưới, không chỉ list phẳng |
| **Trang chi tiết bài viết** | Breadcrumb, tiêu đề, meta, ảnh+caption/credit, nội dung, box bài liên quan, nút "Trở lại/Lưu"+share, khung bình luận "Ý kiến" (input+danh sách), tags | Breadcrumb, tiêu đề, tóm tắt, meta (tác giả/ngày/lượt xem), ảnh bìa (không caption), nội dung | Thiếu: caption/credit ảnh, box bài liên quan, nút lưu/share, khung bình luận, tags hiển thị | Cao (liên quan+tags), Trung bình (bình luận — cần backend mới), Thấp (share buttons) | Vào 1 bài, kiểm tra từng phần tử theo danh sách trên |
| **Trang tìm kiếm** | Input tìm kiếm + bộ lọc (Thời gian/Chuyên mục/Dạng bài) + tab "Mới nhất/Liên quan", kết quả có ảnh+tóm tắt+số bình luận | Tiêu đề kết quả + list phẳng (ảnh+tiêu đề+tóm tắt+ngày+chuyên mục), tìm theo substring (đã biết hạn chế: không khớp nếu từ khóa không xuất hiện nguyên văn) | Thiếu: input tìm kiếm ngay trên trang kết quả, bộ lọc thời gian/chuyên mục, tab sắp xếp | Trung bình | Nhập từ khóa có trong tiêu đề thật → ra kết quả đúng; kiểm tra có filter chưa |
| **Trang chủ đề (tag)** | — (VnExpress dùng "Chủ đề" nội bộ khác, không khảo sát sâu) | Route `/chu-de/{slug}` hoạt động (200) nhưng luôn rỗng vì chưa seed `ArticleTags` | Dữ liệu tag mẫu | Thấp | Seed vài tag demo, kiểm tra trang hiển thị đúng bài gắn tag |
| **Footer** | 4 cột link đầy đủ (chuyên mục, mục khác, liên hệ), thông tin pháp lý, mạng xã hội, điều khoản/cookie/RSS | 2 dòng text (disclaimer + copyright) | Gần như toàn bộ: cột link chuyên mục, liên hệ, pháp lý, mạng xã hội | Trung bình | Đếm số cột/link, kiểm tra link nội bộ dẫn đúng trang |
| **Responsive mobile** | Hamburger menu, 1 cột | Hamburger menu hoạt động, 1 cột, không tràn ngang (đã kiểm chứng pass trước) | Đạt tương đương cơ bản | — | Đã nghiệm thu (test tự động `homepage.spec.ts`) |
| **Bình luận bài viết** | Có (input + hiển thị danh sách bình luận, yêu cầu đăng nhập) | Không có | Toàn bộ tính năng (cần entity Comment, CRUD, kiểm duyệt) | Thấp (tính năng lớn, không bắt buộc cho portfolio demo) | Tạo bình luận thật, hiển thị lại đúng, có thể ẩn/duyệt |
| **Lưu bài / chia sẻ mạng xã hội** | Nút "Lưu" (cần đăng nhập) + icon share (Facebook/X/...) | Không có | Toàn bộ | Thấp | Click nút, kiểm tra hành vi (lưu vào danh sách cá nhân hoặc share link) |
| **SEO (meta description, OG tags, sitemap)** | Có đầy đủ (ngoài phạm vi khảo sát UI trực tiếp nhưng đã biết qua M4 backlog) | Chưa có | Toàn bộ | Trung bình (đã ghi nhận ở `docs/PROGRESS.md` M4) | Xem `<head>` có meta description/OG, có `sitemap.xml` trả 200 |

---

## 4. Ghi chú kỹ thuật phát sinh trong lúc khảo sát (không phải lỗi, chỉ là quan sát)

- Ảnh trong `.category-panel` dùng `loading="lazy"`; khi chụp full-page ngay sau khi tải trang, một số ảnh ở cuối trang hiển thị trống do chưa kịp trigger lazy-load. Xác minh bằng `img.complete`/`naturalWidth`: sau khi cuộn qua hoặc chờ thêm ~1.5s, ảnh tải đúng 100%. **Không phải lỗi ảnh vỡ thật**, chỉ là đặc điểm cần lưu ý khi viết test tự động full-page (nên cuộn qua trang hoặc tắt lazy-load khi chụp kiểm thử).
- Trang tìm kiếm VNews hoạt động đúng với từ khóa khớp nguyên văn tiêu đề (đã test "trường đại học" → ra đúng 1 kết quả); từ khóa chỉ khớp tên chuyên mục (vd. "giáo dục") không ra kết quả vì search hiện tại chỉ quét `Title`/`Summary`, không quét tên chuyên mục — hành vi đúng theo thiết kế hiện tại, không phải lỗi.

---

## 5. Thứ tự triển khai đề xuất

**Nguyên tắc:** hoàn thiện trang chủ toàn phần trước (đúng yêu cầu), sau đó mới mở rộng sang loại trang khác; ưu tiên tạo **component tái sử dụng** (Razor partial/view component) dùng chung giữa trang chủ, chuyên mục, tìm kiếm thay vì lặp lại markup card bài viết.

### Giai đoạn A — Trang chủ toàn phần (ưu tiên cao nhất, đúng yêu cầu hiện tại)

**Cập nhật 2026-10-29 (sau khi bị phản hồi "còn bỏ sót section"):** kế hoạch ban đầu chỉ cộng thêm vài khối chuyên mục, thiếu hẳn 4 kiểu section chiếm nhiều diện tích nhất ở VnExpress (stream tổng hợp dài, Podcasts, Ảnh, Spotlight). Danh sách dưới đây bổ sung đầy đủ, map 1-1 với 14 section đã kiểm kê ở mục 1.

| Section VnExpress (mục 1) | Việc cần làm trên VNews | Component dùng lại |
|---|---|---|
| 1–2. Utility bar + Masthead/Nav | Đã đạt (pass trước) — không đổi | — |
| 3. Hero | Đã đạt (pass trước) — không đổi | `_ArticleCard layout="image-left"` (mới, cũng dùng cho hero) |
| 4. Stream tổng hợp dài | **Section mới**: "Dòng tin mới nhất" dạng card lớn (ảnh trên, tiêu đề dưới), lấy ~8-10 bài mới nhất xen nhiều chuyên mục khác nhau, xếp dọc 1 cột khổ rộng — thay thế/đặt cạnh "Tin mới nhất" list nhỏ hiện tại | `_ArticleCard layout="image-top-large"` |
| 5, 8, 9, 10. Khối chuyên mục (KHCN, Thời sự, Xe, Du lịch...) | Mở rộng từ 4 lên **toàn bộ 10 chuyên mục** có bài, mỗi khối: tiêu đề + "Xem thêm" + lưới 3 card | `_ArticleCard layout="image-top"` (đã có, tái cấu trúc thành partial) |
| 6. Khối đặc biệt (không tên) | Bỏ qua — nội dung quảng cáo/đối tác, không áp dụng cho VNews | — |
| 7. Podcasts | **Section mới, demo rõ ràng**: dải card "tập podcast" (ảnh + tiêu đề + nhãn thời lượng giả lập), gắn nhãn "Minh hoạ — chưa có audio thật" để không gây hiểu lầm có tính năng phát âm thanh thật | `_ArticleCard layout="podcast-demo"` |
| 11. Tâm sự | Gộp vào danh sách khối chuyên mục (mục trên) nếu có category tương ứng, hoặc bỏ qua nếu không có | — |
| 12. Ảnh | **Section mới**: lưới ảnh dạng mosaic (3-4 cột, ảnh lớn hơn, ít chữ) lấy từ cover image các bài, dẫn link vào bài | `_ArticleCard layout="photo-grid"` |
| 13. Spotlight | **Section mới**: 2-3 card lớn nổi bật (ảnh full-bleed, tiêu đề đè lên ảnh), chọn theo `ViewCount` cao nhất | `_ArticleCard layout="spotlight"` |
| 14. Footer | Viết lại `_SiteFooter` 3-4 cột link thật (chuyên mục, trang chủ/tìm kiếm, thông tin dự án) + dòng pháp lý/disclaimer | `_SiteFooter` (component mới) |

**Các bước triển khai cụ thể:**
1. Mở rộng `IArticleQueryService`/`HomePageData` (Application layer) để cung cấp đủ dữ liệu cho các section mới: `StreamItems` (8-10 bài mới nhất xen chuyên mục), `PhotoHighlights` (bài có ảnh, lấy gần đây), `Spotlight` (top theo `ViewCount`), `PodcastDemoItems` (có thể dùng lại tập con bài viết, không cần entity mới). Giữ nguyên API/CMS hiện có, chỉ thêm method/DTO mới, không phá vỡ hợp đồng cũ.
2. Tạo component `_ArticleCard.cshtml` (Razor partial, nhận `ArticleSummaryDto` + tham số `layout`) hỗ trợ đủ 5 biến thể ở bảng trên — thay toàn bộ markup lặp lại hiện có trong `Index.cshtml` (`hero-side__item`, `story-row`, `card`).
3. Viết `_SiteFooter.cshtml`, include vào `_Layout.cshtml` thay cho footer 2 dòng hiện tại.
4. Mở rộng khối chuyên mục trên trang chủ từ 4 lên toàn bộ 10 chuyên mục có bài published.
5. Thêm section Stream, Podcasts (demo), Ảnh, Spotlight theo đúng thứ tự tương đối trong trang (không bắt buộc trùng thứ tự Y chính xác như VnExpress, nhưng phải xuất hiện đầy đủ, hợp lý về mặt biên tập).
6. Sau mỗi section: chạy `npx playwright test` (full-page desktop 1440 + mobile 390), xem ảnh, tự sửa khác biệt quan trọng trước khi sang section tiếp theo.
7. `dotnet build` + `dotnet test` cuối cùng, commit local nếu đạt.

### Giai đoạn A — KẾT QUẢ TRIỂN KHAI THẬT (2026-10-29)

**Trạng thái: ĐÃ TRIỂN KHAI, đã kiểm chứng bằng Playwright + build/test thật.**

**Code đã thay đổi:**
- `src/VNews.Application/Common/HomePageViewData.cs` — thêm `StreamItems`, `PhotoHighlights`, `Spotlight`, `PodcastDemoItems` vào `HomePageData`.
- `src/VNews.Infrastructure/Services/ArticleQueryService.cs` — refactor dùng chung 1 `Expression<Func<Article, ArticleSummaryDto>>` (xoá lặp code 5 lần), thêm truy vấn cho 4 field mới; khối chuyên mục mở từ `Take(4)` categories sang **toàn bộ categories có bài** (10/10).
- `src/VNews.Web/Models/ArticleCardViewModel.cs` (mới) + `src/VNews.Web/Views/Shared/_ArticleCard.cshtml` (mới) — component Razor partial dùng chung, 7 biến thể layout (`image-left`, `hero-side-item`, `story-row`, `image-top-large`, `photo-grid`, `spotlight`, `podcast-demo`, `text-only`), thay thế toàn bộ markup card lặp lại trước đây trong `Index.cshtml`.
- `src/VNews.Web/Views/Shared/_SiteFooter.cshtml` (mới) — footer 3 cột (Chuyên mục/Khám phá/Về VNews) lấy danh mục thật qua `ICategoryQueryService`, thay cho footer 2 dòng cũ; include vào `_Layout.cshtml`.
- `src/VNews.Web/Views/Home/Index.cshtml` — viết lại hoàn toàn, dùng `_ArticleCard` cho mọi card, thêm 4 section mới theo đúng thứ tự: Hero → **Dòng tin mới nhất (Stream)** → Tin mới nhất/Đọc nhiều → 10 khối chuyên mục → **Podcasts (demo)** → **Ảnh** → **Spotlight**.
- `src/VNews.Web/wwwroot/css/site.css` — thêm CSS cho `.stream-*`, `.podcast-*`, `.photo-grid*`, `.spotlight-*`, `.site-footer__grid/col`, có responsive ở cả 3 breakpoint (900/768/480px).

**Kiểm chứng thật (không suy đoán):**
- `dotnet build VNews.slnx` → Build succeeded, 0 lỗi.
- Chạy app thật, cuộn toàn trang kích hoạt lazy-load, `playwright-cli console` → 0 lỗi/cảnh báo.
- Chụp full-page desktop 1440px (`vnews-home-desktop-stageA.png`, 1440×11783) và mobile 390px (`vnews-home-mobile-stageA.png`, 390×22344) — xem bằng mắt từng section riêng (crop `.stream-section`, `.podcast-section`, `.photo-section`, `.spotlight-section`, `.site-footer`) ở cả 2 viewport: dữ liệu demo thật, ảnh phân biệt theo từng bài (không lặp), không có section nào chỉ là "vài card kéo dài trang" — mỗi section đúng kiểu trình bày riêng (card lớn ảnh-trên cho Stream, card nhỏ ngang có icon play cho Podcasts, mosaic vuông cho Ảnh, card ảnh-phủ-chữ cho Spotlight).
- `npx playwright test` (bộ test đo bounding box từ pass trước) → **3/3 pass** — xác nhận hero geometry (masthead/nav/hero) không bị phá vỡ, không tràn ngang ở 390px và 768px sau khi thêm 4 section mới.
- `dotnet test VNews.slnx` → **16/16 pass** (API + CMS integration test không bị ảnh hưởng).
- Dọn dữ liệu rác do `dotnet test` tạo lặp (bài "Bai test tu dong.../Bai cho duyet...") sau mỗi lần chạy, không đụng 51 bài demo thật.

**Giới hạn còn lại trong phạm vi Giai đoạn A (báo cáo trung thực):**
- "Dòng tin tổng hợp" của VNews dùng layout ảnh-trên 1 cột, trong khi VnExpress có nhiều biến thể card xen kẽ trong cùng khối stream — chấp nhận được vì không yêu cầu pixel-perfect, chỉ yêu cầu đúng *kiểu* trình bày (ảnh lớn + tiêu đề + tóm tắt), đã đạt.
- Section "Khối đặc biệt" (mục 6 trong bảng khảo sát, không rõ nội dung — khả năng là quảng cáo/đối tác của VnExpress) chủ động bỏ qua, không áp dụng cho VNews (không có nội dung tương đương hợp lý).
- Podcasts là **demo rõ ràng, không có audio thật** — đã gắn nhãn minh bạch ngay trong UI để không gây hiểu lầm, đúng tinh thần "không dùng dữ liệu giả vờ là tính năng thật".
- Chưa đo lại bounding-box pixel chính xác cho 4 section mới so với VnExpress (chỉ so sánh bằng mắt + đúng kiểu trình bày) — nằm ngoài yêu cầu rõ ràng của lượt này (yêu cầu là "đúng kiểu trình bày và có dữ liệu demo phù hợp", không yêu cầu đo pixel cho từng section mới).

**Nghiệm thu pass 1: CHƯA ĐẠT** (phản hồi người dùng, 2026-10-29) — có đủ section nhưng mỗi section chỉ là biến thể của "ảnh + tiêu đề", lặp lại cùng 1 loại card, không đúng *kiểu trình bày* riêng của từng khối thật trên VnExpress.

### Pass 2 — Redesign đúng kiểu trình bày thật (2026-10-29, sau phản hồi "còn khác nhiều")

**Phương pháp:** không khảo sát lại site, dùng ảnh full-page VnExpress đã lưu (`artifacts/survey/vnexpress-home-desktop-full.png`), viết script crop pixel chính xác từng khối (`visual-tests/crop-reference.js`) để xem đúng cấu trúc thật thay vì suy đoán. Phát hiện:
- Khối chuyên mục thật (KHCN, Kinh doanh...) = ô nền màu nhạt có viền, tiêu đề + tin dẫn lớn (ảnh trái/chữ phải) + danh sách 3 tin phụ dạng thumb nhỏ bên cạnh — **không phải lưới 3 card bằng nhau**.
- "Ảnh" thật = 1 ảnh lớn + panel tối bên phải liệt kê 2 tin chữ — **không phải lưới mosaic 8 ảnh**.
- "Spotlight" thật = 1 feature lớn duy nhất (ảnh trái/chữ phải) + danh sách tiêu đề bên dưới — **không phải lưới 3 card ảnh-phủ-chữ**.
- "Podcasts" thật = dải ảnh lớn ngang, icon play góc trên-trái, badge thời lượng góc dưới-phải — **không phải card nhỏ 72px + icon play giữa**.

**Sửa lại trên code (component `_ArticleCard.cshtml` + `Index.cshtml` + `site.css`):**
- Thêm 6 layout mới: `category-lead`, `list-item-thumb`, `photo-feature`, `photo-feature-aside`, `spotlight-feature`, `list-item` — mỗi section giờ dùng tổ hợp lead+list khác nhau thay vì 1 loại card lặp lại.
- `.category-panel`: đổi từ `category-panel__grid` (3 card đều) sang `category-panel__body` (grid 2 cột: lead trái 1.3fr + `<ul>` danh sách thumb nhỏ phải, có viền ngăn cách) + nền `#fcfaf5` viền nhạt giống ô tin tức thật.
- `.photo-section`: đổi từ `photo-grid` (4×2 ảnh vuông) sang `photo-feature-row` (ảnh lớn 1.5fr trái + panel nền tối `#1c1c1c` liệt kê 2 tin chữ phải).
- `.spotlight-section`: đổi từ `spotlight-grid` (3 card ảnh-phủ-chữ) sang 1 `spotlight-feature` lớn (ảnh trái 46%/chữ phải) + `spotlight-list` 2 cột danh sách tiêu đề bên dưới.
- `.podcast-grid`: ảnh phóng to (aspect 3:4, không còn 72×72px), icon play chuyển lên góc trên-trái, thêm badge thời lượng góc dưới-phải đè lên ảnh, tiêu đề đè gradient dưới ảnh — đúng dạng "dải tập phát" thay vì card ngang nhỏ.
- Responsive viết lại riêng cho từng section ở breakpoint 768px/480px (không dùng chung 1 rule `grid-template-columns: 1fr` cho tất cả) — ví dụ `category-panel__body` chuyển viền trái→viền trên khi xếp dọc, `photo-feature-row` ảnh bo góc trên thay vì góc trái khi xếp dọc.
- Sửa 2 chỗ lỡ dùng lại font `Georgia` (lỗi vỡ dấu tiếng Việt đã sửa ở pass trước) khi viết CSS mới — phát hiện bằng grep trước khi build, không để lọt lại.

**Kiểm chứng thật (không suy đoán):**
- `dotnet build` → Build succeeded, 0 lỗi. `dotnet test` → **16/16 pass**.
- Chạy app thật, cuộn toàn trang kích hoạt lazy-load, `playwright-cli console` → 0 lỗi.
- Crop riêng từng section desktop (`stageA2-category.png`, `stageA2-photo.png`, `stageA2-spotlight.png`, `stageA2-podcast.png`) và mobile (`stageA2-mobile-*.png`) — xem bằng mắt, xác nhận đúng cấu trúc lead+list/feature+list như bản tham chiếu, mobile xếp dọc hợp lý theo thứ tự biên tập (ảnh → tiêu đề/tóm tắt → danh sách phụ), không phải chỉ ép card desktop xuống 1 cột.
- `npx playwright test` → **3/3 pass** (hero geometry/overflow không bị phá vỡ bởi redesign).
- Kiểm tra tương tác thật: click "Xem thêm" ở khối "Thời sự" → điều hướng đúng `/chuyen-muc/thoi-su` (200); link ảnh trong Spotlight → điều hướng đúng trang bài viết thật (200).
- Ảnh full-page cuối cùng lưu tại `artifacts/visual/vnews-desktop.png`, `artifacts/visual/vnews-mobile.png` (ghi đè bản trước đó bằng bản đã redesign).
- Dọn ảnh trung gian pass 1 (`stageA-*.png`, không còn phản ánh code hiện tại), giữ ảnh crop pass 2 (`stageA2-*.png`) làm bằng chứng đối chiếu cuối cùng.

**Giới hạn còn lại (báo cáo trung thực, không pixel-perfect):**
- Khối chuyên mục thật của VnExpress còn có pill sub-tab (Chính trị/Dân sinh/...) và đôi khi widget đặc biệt (bảng giá, banner nội bộ) — VNews chưa có vì không có dữ liệu sub-category.
- "Dòng tin mới nhất" (Stream) của VNews vẫn là 1 cột card lớn tuần tự; bản thật VnExpress có cột trái là sidebar tin nhỏ chạy song song các khối chuyên mục — không tái tạo vì cần đổi toàn bộ bố cục 2-cột của cả trang, vượt phạm vi 1 lượt sửa, ghi nhận để cân nhắc ở lượt sau nếu được yêu cầu.
- Footer vẫn 3 cột đều (chưa đổi sang 5 cột lệch như bản thật) — mức ưu tiên thấp hơn 4 section trên theo đúng thứ tự "từ trên xuống dưới", chưa sửa trong lượt này.

**Nghiệm thu Giai đoạn A (pass 2): ĐẠT có điều kiện** — 4 section bị phản hồi "khác biệt lớn" (khối chuyên mục, Ảnh, Spotlight, Podcasts) đã redesign đúng *kiểu trình bày* riêng biệt theo bản tham chiếu, kiểm chứng bằng Playwright + dotnet test đều pass, không ảnh/tràn ngang/khoảng trắng bất thường, liên kết hoạt động đúng. Còn 3 giới hạn nêu trên (sub-tab, stream 2-cột, footer 5-cột) chưa đạt pixel-perfect nhưng không thuộc "khác biệt lớn nhất" theo đúng phạm vi phản hồi lần này.

### Giai đoạn B — Trang chuyên mục
6. Thêm lead-story lớn đầu trang chuyên mục (dùng lại `_ArticleCard layout="image-left"` — chính component đã làm hero trang chủ).
7. Đổi list phẳng còn lại sang lưới nhiều cột (dùng `_ArticleCard layout="image-top"` — cùng component card chuyên mục trên trang chủ).

### Giai đoạn C — Trang chi tiết bài viết
8. Thêm caption/credit ảnh (field có sẵn hay cần thêm cột DB tuỳ model hiện tại).
9. Box "bài liên quan" (lấy theo cùng `CategoryId`, dùng lại `_ArticleCard`).
10. Hiển thị tags thật (cần seed `ArticleTags` trước — liên quan mục Giai đoạn D).

### Giai đoạn D — Trang tìm kiếm + dữ liệu tag
11. Thêm input tìm kiếm ngay trên trang kết quả (dùng lại form tìm kiếm trong masthead).
12. Seed tag demo cho `ArticleTags`, kiểm tra `/chu-de/{slug}` hiển thị đúng.

### Giai đoạn E — Tính năng tương tác (ưu tiên thấp, backend mới)
13. Bình luận bài viết (entity mới, cần kiểm duyệt).
14. Lưu bài / chia sẻ mạng xã hội.
15. SEO: meta description/OG tags, `sitemap.xml`.

---

## 6. Tổng kết khảo sát

- File parity matrix: `docs/VNEWS-PARITY-MATRIX.md` (file này).
- Số section trang chủ VnExpress: **14** (không tính các sub-item bên trong từng khối).
- Số section trang chủ VNews hiện tại: **6**.
- Số loại trang đã khảo sát cả 2 phía: **4** (trang chủ, chuyên mục, chi tiết bài, tìm kiếm) + 1 phía VNews riêng (trang chủ đề, chưa có đối chiếu trực tiếp).
- Số dòng "còn thiếu" trong bảng mục 3: **13** hạng mục (không tính các dòng "đã nghiệm thu").
- Thứ tự triển khai: 5 giai đoạn (A→E), giai đoạn A (trang chủ) có 5 bước cụ thể, ưu tiên cao nhất theo đúng yêu cầu "hoàn thành trang chủ toàn phần trước".
- **Chưa sửa code nào trong lượt khảo sát này** — đúng yêu cầu.
