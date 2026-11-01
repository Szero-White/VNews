using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

/// <summary>Soft delete of articles: move to the trash and restore. Nothing is ever removed permanently.</summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Editor + "," + Roles.Author)]
public class CmsTrashController : Controller
{
    private readonly IArticleTrashService _trash;
    private readonly UserManager<ApplicationUser> _userManager;

    public CmsTrashController(IArticleTrashService trash, UserManager<ApplicationUser> userManager)
    {
        _trash = trash;
        _userManager = userManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Move(int id, string? rowVersionBase64, CancellationToken cancellationToken)
        => RunAsync(id, rowVersionBase64, "Đã chuyển bài viết vào thùng rác. Bạn có thể khôi phục lại bất cứ lúc nào.", cancellationToken,
            (rowVersion, user, name, all) => _trash.MoveToTrashAsync(id, rowVersion, user, name, all, cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Restore(int id, string? rowVersionBase64, CancellationToken cancellationToken)
        => RunAsync(id, rowVersionBase64, "Đã khôi phục bài viết.", cancellationToken,
            (rowVersion, user, name, all) => _trash.RestoreAsync(id, rowVersion, user, name, all, cancellationToken));

    private async Task<IActionResult> RunAsync(int id, string? rowVersionBase64, string success, CancellationToken cancellationToken,
        Func<byte[]?, string, string, bool, Task> action)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Forbid();
        }

        try
        {
            await action(DecodeRowVersion(rowVersionBase64), user.Id, user.DisplayName, User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor));
            TempData["Message"] = success;
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (ArticleForbiddenException)
        {
            return Forbid();
        }
        catch (Exception ex) when (ex is ArticleWorkflowException or ArticleConcurrencyException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Index", "CmsArticles");
    }

    private static byte[]? DecodeRowVersion(string? base64)
    {
        try { return string.IsNullOrWhiteSpace(base64) ? null : Convert.FromBase64String(base64); }
        catch (FormatException) { return null; }
    }
}
