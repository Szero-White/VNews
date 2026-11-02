using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

/// <summary>
/// Comment moderation for editors and admins: list and filter, take comments down (one or many), bring them back. Nothing is deleted.
/// comments-list scripts post with fetch and ask for JSON, so the page stays where it is and updates the rows itself.
/// </summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Editor)]
public sealed class CmsCommentsController : Controller
{
    private const int PageSize = 20;
    private const int MaxBulk = 100;
    private readonly ICommentModerationService _moderation;
    private readonly ICommentService _comments;
    private readonly UserManager<ApplicationUser> _userManager;

    public CmsCommentsController(ICommentModerationService moderation, ICommentService comments, UserManager<ApplicationUser> userManager)
    {
        _moderation = moderation;
        _comments = comments;
        _userManager = userManager;
    }

    private bool WantsJson => Request.Headers["X-Requested-With"] == "fetch";

    [HttpGet]
    public async Task<IActionResult> Index(string? status, string? q, [FromQuery(Name = "from")] DateTime? from, [FromQuery(Name = "to")] DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        if (from > to) (from, to) = (to, from); // days entered the wrong way round still give the range meant
        var vietnam = TimeSpan.FromHours(7); // the pickers are days in Vietnam time, comments are stored in UTC; "to" covers the whole last day
        ViewData["Status"] = status;
        ViewData["Query"] = q;
        ViewData["From"] = from?.Date;
        ViewData["To"] = to?.Date;
        ViewData["Counts"] = await _moderation.CountsAsync(cancellationToken);
        return View(await _moderation.ListAsync(status, q, page, PageSize, cancellationToken,
            from is DateTime f ? DateTime.SpecifyKind(f.Date - vietnam, DateTimeKind.Utc) : null,
            to is DateTime t ? DateTime.SpecifyKind(t.Date.AddDays(1) - vietnam, DateTimeKind.Utc) : null));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Hide(int id, string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
        => ChangeAsync(new[] { id }, hide: true, status, q, page, cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Restore(int id, string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
        => ChangeAsync(new[] { id }, hide: false, status, q, page, cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> HideMany(int[] ids, string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
        => ChangeAsync(ids, hide: true, status, q, page, cancellationToken);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreMany(int[] ids, string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
        => ChangeAsync(ids, hide: false, status, q, page, cancellationToken);

    private async Task<IActionResult> ChangeAsync(int[]? ids, bool hide, string? status, string? q, int page, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Forbid();

        var wanted = (ids ?? Array.Empty<int>()).Distinct().Take(MaxBulk).ToList();
        if (wanted.Count == 0) return Finish(false, "Bạn chưa chọn bình luận nào.", null, null, status, q, page);

        var done = new List<int>();
        string? firstProblem = null;
        foreach (var id in wanted)
        {
            var result = hide
                ? await _comments.HideAsync(id, user.Id, user.DisplayName, cancellationToken)
                : await _moderation.RestoreAsync(id, user.Id, user.DisplayName, cancellationToken);
            if (result.Success) done.Add(id); else firstProblem ??= result.Message;
        }

        if (done.Count == 0) return Finish(false, firstProblem ?? "Không thực hiện được.", null, null, status, q, page);

        var verb = hide ? "gỡ" : "khôi phục";
        var message = $"Đã {verb} {done.Count} bình luận." + (done.Count < wanted.Count ? $" {wanted.Count - done.Count} bình luận không thực hiện được." : string.Empty);
        var states = await _moderation.StatesAsync(done, cancellationToken);
        return Finish(true, message, states, await _moderation.CountsAsync(cancellationToken), status, q, page);
    }

    private IActionResult Finish(bool success, string message, IReadOnlyDictionary<int, string>? states, IReadOnlyDictionary<string, int>? counts, string? status, string? q, int page)
    {
        if (WantsJson)
        {
            return success
                ? Json(new { ok = true, message, states, counts })
                : BadRequest(new { ok = false, message });
        }

        TempData[success ? "Message" : "Error"] = message;
        return RedirectToAction(nameof(Index), new { status, q, page = page > 1 ? page : (int?)null });
    }
}
