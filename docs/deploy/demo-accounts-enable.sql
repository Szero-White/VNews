-- VNews: mo lai DUNG 4 tai khoan demo tren database Somee (CHUA CHAY, ban tu chay trong panel Somee > SQL).
-- TUY CHON: ung dung tu sua 4 tai khoan nay (mo khoa + dat lai vai tro) ngay lan dau bam nut. Script nay chi de ban chu dong lam truoc.
-- Chi dong vao 4 email @demo.vnews.local. KHONG dong vao admin@/editor@/author@vnews.local, tai khoan ca nhan hay bat ky du lieu nao khac.
-- Idempotent: chay nhieu lan khong sao. Tai khoan demo KHONG co mat khau dung duoc; chi vao duoc bang 4 nut.
SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON;
BEGIN TRAN;

DECLARE @map TABLE (Email nvarchar(256), RoleName nvarchar(256));
INSERT @map VALUES
  (N'demo.admin@demo.vnews.local',  N'Admin'),
  (N'demo.editor@demo.vnews.local', N'Editor'),
  (N'demo.author@demo.vnews.local', N'Author'),
  (N'demo.reader@demo.vnews.local', N'Reader');

-- 1. Mo khoa 4 tai khoan
UPDATE u SET LockoutEnd = NULL, AccessFailedCount = 0, LockoutEnabled = 1
FROM AspNetUsers u JOIN @map m ON u.NormalizedEmail = UPPER(m.Email);

-- 2. Mỗi tai khoan chi giu dung 1 vai tro cua no
DELETE ur FROM AspNetUserRoles ur
JOIN AspNetUsers u ON u.Id = ur.UserId JOIN @map m ON u.NormalizedEmail = UPPER(m.Email)
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE r.Name <> m.RoleName;

INSERT AspNetUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM @map m JOIN AspNetUsers u ON u.NormalizedEmail = UPPER(m.Email) JOIN AspNetRoles r ON r.Name = m.RoleName
WHERE NOT EXISTS (SELECT 1 FROM AspNetUserRoles x WHERE x.UserId = u.Id AND x.RoleId = r.Id);

-- Kiem tra: phai ra dung 4 dong, moi dong 1 vai tro, LockoutEnd = NULL
SELECT u.Email, u.LockoutEnd, r.Name AS RoleName
FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId = u.Id JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE u.NormalizedEmail LIKE N'DEMO.%@DEMO.VNEWS.LOCAL' ORDER BY u.Email;

COMMIT;   -- neu ket qua khong dung 4 dong, doi COMMIT thanh ROLLBACK
