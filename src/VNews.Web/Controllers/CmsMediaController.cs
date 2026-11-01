using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Domain.Identity;
using VNews.Web.Services;

namespace VNews.Web.Controllers;

/// <summary>Administrators only: list the uploaded cover images no article uses, and quarantine them (never delete).</summary>
[Authorize(Roles = Roles.Admin)]
public sealed class CmsMediaController : Controller
{
    private readonly UploadCleanupService _cleanup;

    public CmsMediaController(UploadCleanupService cleanup) => _cleanup = cleanup;

    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _cleanup.ScanAsync(cancellationToken: cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quarantine(CancellationToken cancellationToken)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var name = User.FindFirstValue(Security.DisplayNameClaimsFactory.ClaimType) ?? User.Identity?.Name ?? "Admin";
        var result = await _cleanup.QuarantineUnusedAsync(id, name, cancellationToken: cancellationToken);
        TempData["Message"] = result.Moved == 0
            ? "Không có ảnh nào cần dọn."
            : $"Đã chuyển {result.Moved} ảnh không còn được dùng vào thư mục lưu tạm ({result.Bytes / 1024} KB). Ảnh có thể khôi phục.";
        return RedirectToAction(nameof(Index));
    }
}
