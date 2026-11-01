using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

/// <summary>Comment moderation for editors and admins: list and filter, take a comment down, bring it back. Nothing is deleted.</summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Editor)]
public sealed class CmsCommentsController : Controller
{
    private const int PageSize = 20;
    private readonly ICommentModerationService _moderation;
    private readonly ICommentService _comments;
    private readonly UserManager<ApplicationUser> _userManager;

    public CmsCommentsController(ICommentModerationService moderation, ICommentService comments, UserManager<ApplicationUser> userManager)
    {
        _moderation = moderation;
        _comments = comments;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Status"] = status;
        ViewData["Query"] = q;
        return View(await _moderation.ListAsync(status, q, page, PageSize, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(int id, string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();
        var result = await _comments.HideAsync(id, user.Id, user.DisplayName, cancellationToken);
        return Back(result.Success, "Đã gỡ bình luận. Bạn có thể khôi phục lại ở bộ lọc “Đã gỡ”.", result.Message, status, q, page);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();
        var result = await _moderation.RestoreAsync(id, user.Id, user.DisplayName, cancellationToken);
        return Back(result.Success, "Đã khôi phục bình luận.", result.Message, status, q, page);
    }

    private IActionResult Back(bool success, string message, string? error, string? status, string? q, int page)
    {
        TempData[success ? "Message" : "Error"] = success ? message : error ?? "Không thể thực hiện thao tác.";
        return RedirectToAction(nameof(Index), new { status, q, page = page > 1 ? page : (int?)null });
    }
}
