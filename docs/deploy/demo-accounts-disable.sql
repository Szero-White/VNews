-- VNews: KHOA LAI 4 tai khoan demo tren Somee (dung khi muon tat che do demo cong khai). CHUA CHAY, ban tu chay.
-- Nhớ dong thoi dat DemoAuth:Enabled = false trong appsettings.Production.json (hoac xoa muc DemoAuth).
SET QUOTED_IDENTIFIER ON;
BEGIN TRAN;
UPDATE AspNetUsers SET LockoutEnabled = 1, LockoutEnd = '9999-12-31T23:59:59+00:00'
WHERE NormalizedEmail IN (N'DEMO.ADMIN@DEMO.VNEWS.LOCAL', N'DEMO.EDITOR@DEMO.VNEWS.LOCAL', N'DEMO.AUTHOR@DEMO.VNEWS.LOCAL', N'DEMO.READER@DEMO.VNEWS.LOCAL');
SELECT Email, LockoutEnd FROM AspNetUsers WHERE NormalizedEmail LIKE N'DEMO.%@DEMO.VNEWS.LOCAL';
COMMIT;
