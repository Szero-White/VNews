# Visual Contract — Trang chủ: "Dòng tin mới nhất" + Khối chuyên mục đầu tiên

Phạm vi: CHỈ 2 khu vực dưới đây. Không áp dụng cho phần còn lại của trang chủ.
Nguồn đo: DOM thật trên `https://vnexpress.net/` qua Playwright CLI (`playwright-cli eval`), viewport desktop **1440×900**, ngày 2026-10-29. Không suy đoán — mọi con số lấy từ `getBoundingClientRect()`/`getComputedStyle()` thật.

## Phát hiện cấu trúc chính (khác với giả định trước đây)

VnExpress **không** xếp "Dòng tin mới nhất" và khối chuyên mục đầu tiên theo chiều dọc (cái này dưới cái kia). Chúng chạy **song song 2 cột** bên trong cùng một `<section class="section_stream_home">`:

- **Cột trái** (x≈170–550, rộng **380px**): danh sách tin tổng hợp dạng thumb nhỏ, chạy dài liên tục (2700px+, ~16+ tin).
- **Cột phải** (x≈590–1270, rộng **680px**): các khối chuyên mục xếp chồng dọc (Kinh doanh → Bất động sản → Thể thao → ...). Khối chuyên mục **đầu tiên** là "Kinh doanh" (`id="kinhdoanh"`).
- Khoảng cách giữa 2 cột: **40px**.
- Tổng vùng nội dung: x≈170 → 1270 (rộng 1100px, khớp `width_common` đã dùng cho masthead/nav).
- Tỉ lệ cột trái : cột phải ≈ **380 : 680 ≈ 0.559**.
- Cả 2 cột bắt đầu cùng Y (≈730px tính từ đầu `section_stream_home`) — tức "chạy song song", không phải cái này nằm dưới cái kia.

VNews hiện tại (trước khi sửa): Stream là 1 cột full-width (832px) nằm **trên** toàn bộ, category panel đầu tiên nằm **dưới** stream — sai cấu trúc "song song 2 cột".

## Khu vực 1 — "Dòng tin mới nhất" (Stream, cột trái)

| Tiêu chí | Giá trị tham chiếu (VnExpress) | Dung sai chấp nhận |
|---|---|---|
| Vị trí | Cột trái, cạnh trái khối chuyên mục đầu tiên, cùng hàng (không nằm trên/dưới) | Bắt buộc — đo bằng `offsetY` lệch ≤ 24px giữa 2 cột |
| Độ rộng cột | 380px / tổng 1100px nội dung ≈ 34.5% | Tỉ lệ cột trái trong khoảng 28%–42% tổng vùng 2 cột |
| Cấu trúc 1 item | Ảnh nhỏ bên trái (cố định, không kéo giãn) + tiêu đề in đậm + tóm tắt bên phải | Phải có thumb cố định ở 1 bên (trái), không phải ảnh full-width-trên-tiêu đề |
| Tỉ lệ ảnh thumb | 145×87 ≈ **1.667:1** (16:9.6) | 1.4:1 – 1.9:1 |
| Độ rộng thumb so với item | 145/380 ≈ 38% | ≤ 45% độ rộng item (để còn chỗ cho tiêu đề/tóm tắt) |
| Font tiêu đề | 15px/700/line-height 24px | 14–16px, weight ≥ 600 |
| Font tóm tắt | 14px/400/line-height 19.6px, màu #4f4f4f | 13–15px, có hiển thị tóm tắt (không bắt buộc màu chính xác) |
| Khoảng cách giữa các item | 14–15px | 10–20px |
| Số lượng item hiển thị | Danh sách dài (16+ trong 2700px) | Tối thiểu 6 item (không yêu cầu dài bằng VnExpress — nội dung demo ít hơn) |

## Khu vực 2 — Khối chuyên mục đầu tiên (`#kinhdoanh`, cột phải)

| Tiêu chí | Giá trị tham chiếu (VnExpress) | Dung sai chấp nhận |
|---|---|---|
| Vị trí | Cột phải, cùng hàng với Stream (không dưới Stream) | Bắt buộc |
| Độ rộng cột | 680px / 1100px ≈ 61.8% | 58%–72% tổng vùng 2 cột |
| Tiêu đề khối | Tên chuyên mục in đậm 18px, có thể kèm link phụ (sub-category) cùng hàng | Có tiêu đề rõ ràng + link "Xem thêm" (đã có, giữ nguyên) |
| **Hàng 1** (dưới tiêu đề) | 2 phần tử nằm **ngang hàng**: (a) tin dẫn lớn — ảnh trái 225×135 (tỉ lệ 1.667:1, chiếm 49% chiều rộng lead) + tiêu đề/tóm tắt phải; (b) 1 tin phụ dạng chữ (không ảnh) bên phải, rộng 220px | Bắt buộc 2 phần tử cạnh nhau (không xếp dọc) ở ≥1024px; ảnh lead tỉ lệ 1.4–1.9:1, chiếm 40–60% chiều rộng khối lead |
| **Hàng 2** (dưới hàng 1) | 3 tin dạng chữ, không ảnh, xếp **ngang hàng 3 cột đều** (200/215/215px) | Bắt buộc ≥3 phần tử xếp ngang hàng (cùng `offsetY`, lệch ≤ 20px) ở desktop |
| Tổng chiều cao khối | ~308px (title 29 + hàng1 154 + hàng2 82 + spacing) | Không quy định cứng — chỉ cần đủ 2 hàng cấu trúc trên |

## Trách nhiệm & cách đo

- Đo bằng Playwright Test (`visual-tests/home-parity.spec.ts`), chạy trên `http://localhost:5299/` ở viewport **1440×900**.
- Test phải **FAIL** trên code hiện tại trước khi sửa (xác nhận bằng chạy thật, không suy đoán) — đã xác nhận: stream hiện là cột full-width nằm trên category panel, không phải 2 cột song song.
- Sau khi sửa code, lặp tối đa 3 vòng: build → chạy test → chụp ảnh từng khu vực → sửa tiếp nếu còn sai tiêu chí.
- **Không mở rộng phạm vi**: các khối chuyên mục thứ 2 trở đi, Podcasts, Ảnh, Spotlight, footer — giữ nguyên như hiện tại, không sửa trong lượt này.
- Không yêu cầu giống pixel-perfect màu sắc/nội dung — chỉ yêu cầu đúng *cấu trúc, vị trí tương đối, tỉ lệ kích thước, số cột/hàng* theo bảng trên.
