<#
  Làm mới gói chuyển lên host từ dữ liệu HIỆN TẠI của VNewsDemo. Chạy ngay trước khi chuyển, sau khi bạn đã chỉnh sửa xong.
  Chỉ ĐỌC từ VNewsDemo và thư mục ảnh; chỉ GHI vào thư mục gói. Không sửa, không xóa, không áp migration lên bất kỳ database nào.

    1) Sao lưu VNewsDemo bằng BACKUP ... COPY_ONLY, CHECKSUM, rồi RESTORE VERIFYONLY.
    2) Tìm mọi ảnh /uploads/... mà bài viết đang dùng (ảnh bìa, ảnh trong bộ sưu tập, ảnh trong nội dung) và chép đúng những ảnh đó.
    3) Báo ảnh nào được tham chiếu nhưng không có trên đĩa (phải là 0 trước khi chuyển).

  Cách dùng (PowerShell, tại thư mục dự án):
    .\docs\deploy\refresh-package.ps1
#>
param(
    [string] $Instance   = '(localdb)\MSSQLLocalDB',
    [string] $Database   = 'VNewsDemo',
    [string] $UploadsRoot = (Join-Path $PSScriptRoot '..\..\artifacts\demo-uploads'),
    [string] $PackageDir  = (Join-Path $PSScriptRoot '..\..\artifacts\host-package')
)
$ErrorActionPreference = 'Stop'
if ($Database -eq 'VNewsDb') { throw 'VNewsDb is not the content database; refusing.' }
$UploadsRoot = (Resolve-Path $UploadsRoot).Path
New-Item -ItemType Directory -Force (Join-Path $PackageDir 'db'), (Join-Path $PackageDir 'uploads') | Out-Null
$PackageDir = (Resolve-Path $PackageDir).Path

# 1) backup + verify
$bak = Join-Path $PackageDir 'db\VNewsDemo-final.bak'
sqlcmd -S $Instance -d master -I -b -Q "BACKUP DATABASE [$Database] TO DISK = N'$bak' WITH COPY_ONLY, CHECKSUM, INIT, NAME = N'$Database-final (copy only)'; RESTORE VERIFYONLY FROM DISK = N'$bak' WITH CHECKSUM;" | Select-Object -Last 2
'{0}: {1:N1} MB, {2:u}' -f $bak, ((Get-Item $bak).Length / 1MB), (Get-Date)

# 2) pictures the content uses
$sql = Join-Path ([IO.Path]::GetTempPath()) ("vnews-refs-" + [guid]::NewGuid().ToString('N') + '.sql')
"SET NOCOUNT ON; SELECT CoverImageUrl FROM Articles WHERE CoverImageUrl LIKE '/uploads/%' UNION SELECT Url FROM ArticleImages WHERE Url LIKE '/uploads/%' UNION SELECT Body FROM Articles WHERE Body LIKE '%/uploads/%';" | Set-Content $sql -Encoding UTF8
try   { $text = (sqlcmd -S $Instance -d $Database -I -f 65001 -y 0 -i $sql) -join "`n" }
finally { Remove-Item $sql -ErrorAction SilentlyContinue }
$refs = [regex]::Matches($text, '/uploads/[A-Za-z0-9_\-/\.]+') | ForEach-Object { $_.Value } | Sort-Object -Unique

$copied = 0; $missing = @(); $bytes = 0
foreach ($url in $refs) {
    $rel = ($url -replace '^/uploads/', '') -replace '/', '\'
    $from = Join-Path $UploadsRoot $rel
    if (-not (Test-Path -LiteralPath $from)) { $missing += $url; continue }
    $to = Join-Path (Join-Path $PackageDir 'uploads') $rel
    New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
    if (-not (Test-Path -LiteralPath $to) -or (Get-Item $to).Length -ne (Get-Item $from).Length) { Copy-Item -LiteralPath $from -Destination $to -Force }
    $copied++; $bytes += (Get-Item $from).Length
}
$refs | Set-Content (Join-Path $PackageDir 'uploads-manifest.txt') -Encoding UTF8

# 3) report
'{0} referenced pictures, {1:N1} MB in the package; missing on disk: {2}' -f $copied, ($bytes / 1MB), $missing.Count
$missing | ForEach-Object { "  MISSING: $_" }
if ($missing.Count -gt 0) { Write-Warning 'Some pictures used by the content are not on disk. Do not transfer before this is resolved.' }
