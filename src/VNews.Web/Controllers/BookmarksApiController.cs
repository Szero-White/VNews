using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.Controllers;

/// <summary>Authenticated bookmark storage used by the "Lưu bài" button. Only published articles can be bookmarked or listed.</summary>
[ApiController]
[Authorize]
[Route("api/me/bookmarks")]
public sealed class BookmarksApiController : ControllerBase
{
    private const int ListLimit = 100;
    private readonly ISavedArticleService _saved;

    public BookmarksApiController(ISavedArticleService saved) => _saved = saved;

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (UserId is not { } userId) return Unauthorized();

        var rows = await _saved.RecentAsync(userId, ListLimit, ct);
        return Ok(rows.Select(x => new { id = x.ArticleId, slug = x.Slug, title = x.Title, image = x.CoverImageUrl }));
    }

    [HttpPost("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int id, CancellationToken ct)
    {
        if (UserId is not { } userId) return Unauthorized();
        return await _saved.SaveAsync(userId, id, ct) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        if (UserId is not { } userId) return Unauthorized();
        await _saved.RemoveAsync(userId, id, ct);
        return NoContent();
    }
}
