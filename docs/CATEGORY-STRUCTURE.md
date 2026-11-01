# Cấu trúc chuyên mục — hiện trạng và đề xuất (chỉ đề xuất, chưa triển khai)

Nguồn so sánh: thanh điều hướng và menu "Tất cả chuyên mục" của vnexpress.net (đo ngày 2026-11-01). Không có migration hay thay đổi dữ liệu nào được thực hiện.

## Hiện trạng thực tế
- Bảng `Categories`: `Id, Name, Slug, Description, DisplayOrder` — **không có `ParentId`**, nên chưa có chuyên mục con.
- 10 chuyên mục thật: Thời sự, Kinh doanh, Thế giới, Công nghệ, Thể thao, Giải trí, Sức khỏe, Giáo dục, Du lịch, Pháp luật. Route duy nhất: `/chuyen-muc/{slug}`. Bài viết gắn một `CategoryId`.
- CMS Admin (`/cms/CmsCategories`) **đã tạo/sửa được chuyên mục cấp cao** (tên, slug, mô tả, thứ tự) — thêm chuyên mục cấp cao **không cần migration**; chỉ cần có bài viết để trang không rỗng.
- Thanh nav, mega menu và drawer đọc trực tiếp bảng này nên chuyên mục mới tự xuất hiện, không cần sửa giao diện.

## A. Chuyên mục cấp cao có thể bổ sung (không cần migration)
| Tên | Route | Nguồn bài viết | Migration |
|---|---|---|---|
| Góc nhìn | `/chuyen-muc/goc-nhin` | Bài mới gắn `CategoryId` mới (phân tích/bình luận dài) | Không |
| Bất động sản | `/chuyen-muc/bat-dong-san` | Bài mới | Không |
| Đời sống | `/chuyen-muc/doi-song` | Bài mới | Không |
| Xe | `/chuyen-muc/xe` | Bài mới | Không |
| Ý kiến | `/chuyen-muc/y-kien` | Bài mới (ý kiến độc giả/chuyên gia) | Không |
| Tâm sự | `/chuyen-muc/tam-su` | Bài mới | Không |
| Thư giãn | `/chuyen-muc/thu-gian` | Bài mới | Không |
| (đổi tên tùy chọn) Công nghệ → Khoa học công nghệ | giữ `/chuyen-muc/cong-nghe` | Bài hiện có | Không (sửa tên trong CMS) |

Điều kiện: cần **dữ liệu bài viết thật** (tối thiểu ~25 bài/mục để thấy phân trang). Seed bài mới cần bạn duyệt riêng; không tạo mục rỗng hay liên kết giả để lấp thanh menu.

## B. Chuyên mục con (cần migration `ParentId`)
Đề xuất mô hình: `Categories.ParentId int? → Categories.Id` (self-reference), chỉ mục `(ParentId, Slug)` duy nhất; CMS thêm ô chọn mục cha; nav chỉ hiện cấp 1, mega menu và trang chuyên mục hiện cấp 2. Route: `/chuyen-muc/{cha}/{con}`; trang mục cha gộp bài của các mục con (`CategoryId IN (cha, con...)`); bài chỉ gắn vào mục con hoặc mục cha như hiện nay.

| Mục cha | Mục con đề xuất (tham chiếu vnexpress.net) |
|---|---|
| Thời sự | Chính trị, Dân sinh, Việc làm, Giao thông |
| Thế giới | Phân tích, Tư liệu, Quân sự |
| Kinh doanh | Quốc tế, Doanh nghiệp, Chứng khoán |
| Công nghệ | Chuyển đổi số, Đổi mới sáng tạo, AI, Thiết bị |
| Giải trí | Giới sao, Sách, Phim |
| Pháp luật | Hồ sơ phá án, Tư vấn |
| Sức khỏe | Tin tức, Các bệnh, Sống khỏe, Vaccine |
| Thể thao | Bóng đá, Lịch thi đấu, Marathon |
| Giáo dục | Tuyển sinh, Chân dung, Du học |
| Du lịch | Điểm đến, Ẩm thực, Dấu chân, Tư vấn |

Nguồn dữ liệu: `Articles.CategoryId` trỏ tới mục con. Migration: **có** (1 cột nullable + index + FK; dữ liệu cũ không đổi, mọi mục hiện có là cấp 1).

## C. Không phải chuyên mục (cần tính năng riêng, ngoài phạm vi này)
Video, Podcasts, Ảnh, Infographic, Spotlight, International, Tin theo khu vực, Rao vặt, Lịch vạn niên: mỗi mục cần mô hình dữ liệu/loại bài riêng (media, bản tin tiếng Anh, địa lý…) — không nên tạo link giả.

## Thứ tự gợi ý
1. Bổ sung nội dung thật cho 10 mục hiện có (A không có ý nghĩa nếu bài ít).
2. Thêm chuyên mục cấp cao ở nhóm A qua CMS khi đã có bài.
3. Migration `ParentId` + CMS + mega menu nhiều tầng (nhóm B).
4. Các loại nội dung đặc biệt (nhóm C) theo từng giai đoạn.
