using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

/// <summary>Soft delete of articles: move to the trash and restore. Only an admin can empty items from the trash for good (PurgeMany).</summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Editor + "," + Roles.Author)]
public class CmsTrashController : Controller
{
    private const int MaxBulk = 100;
    private readonly IArticleTrashService _trash;
    private readonly IArticleCmsQueryService _cmsQuery;
    private readonly UserManager<ApplicationUser> _userManager;

    public CmsTrashController(IArticleTrashService trash, IArticleCmsQueryService cmsQuery, UserManager<ApplicationUser> userManager)
    {
        _trash = trash;
        _cmsQuery = cmsQuery;
        _userManager = userManager;
    }

    /// <summary>list-actions.js posts with fetch and asks for JSON: the list stays on screen and removes the row itself.</summary>
    private bool WantsJson => Request.Headers["X-Requested-With"] == "fetch";

    private async Task<IActionResult> DoneAsync(string message, IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var stats = await _cmsQuery.GetCmsStatsAsync(User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor) ? null : _userManager.GetUserId(User), cancellationToken);
        return Json(new { ok = true, message, ids, stats = new { draft = stats.Draft, pendingReview = stats.PendingReview, rejected = stats.Rejected, published = stats.Published, trashed = stats.Trashed } });
    }

    private IActionResult Refused(string message) => BadRequest(new { ok = false, message });

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

    /// <summary>Restores every ticked article. Each one is its own transaction with its own role/ownership check, so one refusal never blocks the rest.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreMany(int[] ids, CancellationToken cancellationToken)
        => BulkAsync(ids, "Trashed", "khôi phục", "Bạn chưa chọn bài nào để khôi phục.", cancellationToken,
            (id, user, name, all) => _trash.RestoreAsync(id, null, user, name, all, cancellationToken));

    /// <summary>Moves every ticked article to the trash, with the same per-article checks as the single delete (an author only their own drafts).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MoveMany(int[] ids, CancellationToken cancellationToken)
        => BulkAsync(ids, null, "chuyển vào thùng rác", "Bạn chưa chọn bài nào để xóa.", cancellationToken,
            (id, user, name, all) => _trash.MoveToTrashAsync(id, null, user, name, all, cancellationToken));

    /// <summary>Permanently deletes the ticked articles from the database. Admin only, and only articles already in the trash.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public Task<IActionResult> PurgeMany(int[] ids, CancellationToken cancellationToken)
        => BulkAsync(ids, "Trashed", "xóa vĩnh viễn", "Bạn chưa chọn bài nào để xóa vĩnh viễn.", cancellationToken,
            (id, user, name, all) => _trash.PurgeAsync(id, user, name, User.IsInRole(Roles.Admin), cancellationToken));

    private async Task<IActionResult> BulkAsync(int[]? ids, string? returnStatus, string verb, string nothingTicked, CancellationToken cancellationToken,
        Func<int, string, string, bool, Task> action)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        var distinct = (ids ?? Array.Empty<int>()).Distinct().Take(MaxBulk).ToList();
        if (distinct.Count == 0)
        {
            if (WantsJson) return Refused(nothingTicked);
            TempData["Error"] = nothingTicked;
            return RedirectToAction("Index", "CmsArticles", new { status = returnStatus });
        }

        var canManageAll = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor);
        var done = new List<int>();
        var refused = 0;
        foreach (var id in distinct)
        {
            try
            {
                await action(id, user.Id, user.DisplayName, canManageAll);
                done.Add(id);
            }
            catch (Exception ex) when (ex is EntityNotFoundException or ArticleForbiddenException or ArticleWorkflowException or ArticleConcurrencyException)
            {
                refused++;
            }
        }

        var summary = done.Count > 0
            ? $"Đã {verb} {done.Count} bài viết." + (refused > 0 ? $" {refused} bài không thực hiện được (không đủ quyền hoặc đã thay đổi)." : string.Empty)
            : $"Không {verb} được bài nào (không đủ quyền hoặc bài đã thay đổi).";

        if (WantsJson) return done.Count > 0 ? await DoneAsync(summary, done, cancellationToken) : Refused(summary);

        TempData[done.Count > 0 ? "Message" : "Error"] = summary;
        return RedirectToAction("Index", "CmsArticles", new { status = returnStatus });
    }

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
            if (WantsJson) return await DoneAsync(success, new[] { id }, cancellationToken);
            TempData["Message"] = success;
        }
        catch (EntityNotFoundException)
        {
            return WantsJson ? Refused("Không tìm thấy bài viết (có thể đã bị người khác xử lý).") : NotFound();
        }
        catch (ArticleForbiddenException ex)
        {
            return WantsJson ? Refused(ex.Message) : Forbid();
        }
        catch (Exception ex) when (ex is ArticleWorkflowException or ArticleConcurrencyException)
        {
            if (WantsJson) return Refused(ex.Message);
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
