using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Web.Controllers;

/// <summary>Authenticated bookmark storage. Only published articles can be bookmarked or listed.</summary>
[ApiController]
[Authorize]
[Route("api/me/bookmarks")]
public sealed class BookmarksApiController : ControllerBase
{
    private readonly VNewsDbContext _db;

    public BookmarksApiController(VNewsDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var rows = await _db.SavedArticles.AsNoTracking()
            .Where(x => x.UserId == userId && x.Article.Status == ArticleStatus.Published)
            .OrderByDescending(x => x.SavedAtUtc)
            .Take(100)
            .Select(x => new { id = x.ArticleId, slug = x.Article.Slug, title = x.Article.Title, image = x.Article.CoverImageUrl })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        if (!await _db.Articles.AsNoTracking().AnyAsync(a => a.Id == id && a.Status == ArticleStatus.Published, ct))
            return NotFound();

        if (!await _db.SavedArticles.AnyAsync(x => x.UserId == userId && x.ArticleId == id, ct))
        {
            _db.SavedArticles.Add(new SavedArticle { UserId = userId, ArticleId = id });
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // If two tabs save simultaneously, the composite PK allows only one row.
                if (!await _db.SavedArticles.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ArticleId == id, ct))
                    throw;
            }
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        await _db.SavedArticles.Where(x => x.UserId == userId && x.ArticleId == id).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}
