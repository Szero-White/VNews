/*
  CHỈ CHẠY TRÊN DATABASE CỦA HOST (bản đã khôi phục), TUYỆT ĐỐI KHÔNG chạy trên VNewsDb hay VNewsDemo trên máy bạn.

  Vì sao: VNewsDemo có các tài khoản mẫu dùng mật khẩu công khai "Demo@12345" (xem IdentitySeeder). Trên host công khai, ai biết mật khẩu
  đó đều vào được CMS. Script này, trên bản sao ở host:
    1) khóa các tài khoản mẫu (LockoutEnd = 9999) và đổi SecurityStamp để mọi phiên đang mở bị vô hiệu;
    2) gỡ vai trò của chúng (không xóa tài khoản, nên bình luận, nhật ký và dữ liệu liên quan giữ nguyên);
    3) nhờ đó, khi khởi động lần đầu với Bootstrap__AdminEmail / Bootstrap__AdminPassword, ứng dụng sẽ tạo quản trị viên THẬT
       (ứng dụng chỉ tạo khi chưa có tài khoản Admin nào).
  Tài khoản thật của bạn (ví dụ độc giả congtoan2k4@gmail.com) KHÔNG bị đụng tới.
  Script chạy trong một giao dịch và in kết quả; kiểm tra rồi mới COMMIT nếu bạn chạy từng đoạn.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @demo TABLE (Email nvarchar(256) NOT NULL PRIMARY KEY);
INSERT INTO @demo (Email) VALUES
    (N'admin@vnews.local'), (N'editor@vnews.local'), (N'author@vnews.local'),
    (N'demo.admin@demo.vnews.local'), (N'demo.editor@demo.vnews.local'),
    (N'demo.author@demo.vnews.local'), (N'demo.reader@demo.vnews.local');

BEGIN TRANSACTION;

DELETE ur
FROM AspNetUserRoles ur
JOIN AspNetUsers u ON u.Id = ur.UserId
JOIN @demo d ON d.Email = u.NormalizedEmail OR d.Email = u.Email;

UPDATE u
SET LockoutEnabled = 1,
    LockoutEnd = '9999-12-31T00:00:00+00:00',
    SecurityStamp = CONVERT(nvarchar(36), NEWID())
FROM AspNetUsers u
JOIN @demo d ON d.Email = u.Email;

COMMIT TRANSACTION;

-- Kết quả: các tài khoản mẫu phải bị khóa và không còn vai trò; không được còn Admin nào trước khi khởi động lần đầu.
SELECT u.Email, u.LockoutEnd, (SELECT COUNT(*) FROM AspNetUserRoles r WHERE r.UserId = u.Id) AS Roles
FROM AspNetUsers u JOIN @demo d ON d.Email = u.Email;

SELECT COUNT(*) AS AdminsLeft
FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name = N'Admin';
