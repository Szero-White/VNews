# VNews Visual Workflow — cài một lần, dùng nhiều lần

Bộ bổ sung này **không phải source code website**. Nó thêm lệnh `/visual-match` cho Claude Code, kèm tiêu chí thiết kế và script chụp màn hình. Không thay thế CLAUDE.md, SQL Server hay code đã có.

## Cài đặt

1. Giải nén ZIP ra một thư mục riêng.
2. Copy `.claude`, `docs`, `scripts` từ bộ bổ sung này vào **thư mục gốc** `D:\VNews-Optimized-Claude-Code-Kit` (chọn gộp thư mục; không xóa/tạo lại thư mục dự án). **Không ghi đè** file hiện có nếu bạn đã tự tạo cùng tên.
3. Mở VS Code Terminal ở thư mục dự án. Kiểm tra `node --version` (cần Node.js >=20), `npm --version`.
4. Cài Playwright CLI một lần:
   ```powershell
   npm install -g @playwright/cli@latest
   playwright-cli --help
   playwright-cli install --skills
   ```
   Nếu thiếu Chromium hoặc bị chặn khi mở browser, nhờ Claude kiểm tra lỗi và cài browser tương ứng theo hướng dẫn Playwright, không dùng Bypass permissions.
5. Mở Claude Code trong thư mục VNews, nếu `/visual-match` chưa xuất hiện, restart phiên Claude Code.
6. Đảm bảo ứng dụng đang chạy bằng `dotnet run` theo `RUN-WINDOWS.md`.
7. Chạy lần đầu để **lưu ảnh tham chiếu cố định**:
   ```powershell
   .\scripts\capture-visual.ps1 -CaptureReference
   ```
   Nếu trang gốc chặn automation hoặc ảnh khác mẫu mong muốn, hãy tự chụp bằng trình duyệt ở đúng viewport, đặt vào `references/vnexpress-desktop.png` và `references/vnexpress-mobile.png` khi có.
8. Trong Claude Code: `/visual-match`

## Sử dụng về sau
- **Không** nhập lại các prompt dài.
- Lệnh `/visual-match` áp dụng quy trình screenshot → đo bố cục → sửa Razor/CSS → screenshot lại → build/test → ghi nhận Git.
- Có thể xem screenshot tự động ở `artifacts/visual/`; không dùng ảnh website tin tức động để pixelmatch tuyệt đối.
- Khi M1 đã đạt, quay lại prompt M2 trong kit cũ; giữ toàn bộ backend .NET.

## Lưu ý
- Script được chuẩn bị theo tài liệu CLI hiện hành; cần chạy thử trên chính máy Windows của bạn. Nếu các lệnh CLI đổi cú pháp theo phiên bản cài, Claude có thể xem `playwright-cli --help` và cập nhật script. Chưa chạy kiểm chứng với server localhost trên máy bạn.
- Đừng upload những file cookie/credentials khi làm việc với browser. Không push GitHub khi chưa được yêu cầu.

Nguồn: https://playwright.dev/docs/getting-started-cli , https://code.claude.com/docs/en/skills
