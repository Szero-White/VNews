using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

/// <summary>Admin-only: pull the latest headlines from the publisher's public RSS feeds (summary + link back, never full text).</summary>
[Authorize(Roles = Roles.Admin)]
public sealed class CmsImportController : Controller
{
    private readonly INewsImportService _import;
    private readonly UserManager<ApplicationUser> _userManager;

    public CmsImportController(INewsImportService import, UserManager<ApplicationUser> userManager)
    {
        _import = import;
        _userManager = userManager;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        var result = await _import.ImportAsync(user.Id, user.DisplayName, cancellationToken);
        if (result.HasProblems) TempData["Error"] = string.Join(" ", result.Problems);
        TempData["Message"] = $"Đã nạp tin: thêm {result.Added} bài mới, bỏ qua {result.Skipped} bài đã có.";
        return RedirectToAction("Index", "CmsArticles");
    }
}
