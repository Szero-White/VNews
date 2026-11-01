using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public sealed class SavedArticleService : ISavedArticleService
{
    // An expression (not a method call) so EF translates it into the SQL projection.
    private static readonly Expression<Func<SavedArticle, SavedArticleDto>> ToDto =
        x => new SavedArticleDto(x.ArticleId, x.Article.Slug, x.Article.Title, x.Article.CoverImageUrl, x.Article.Category.Name, x.Article.PublishedAt, x.SavedAtUtc);

    private readonly VNewsDbContext _context;

    public SavedArticleService(VNewsDbContext context) => _context = context;

    private IQueryable<SavedArticle> PublishedOf(string userId) =>
        _context.SavedArticles.AsNoTracking().Where(x => x.UserId == userId && x.Article.Status == ArticleStatus.Published);

    public async Task<PagedResult<SavedArticleDto>> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await PublishedOf(userId).CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, lastPage);

        var rows = await PublishedOf(userId).OrderByDescending(x => x.SavedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToListAsync(cancellationToken);
        return new PagedResult<SavedArticleDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<IReadOnlyList<SavedArticleDto>> RecentAsync(string userId, int count, CancellationToken cancellationToken = default) =>
        await PublishedOf(userId).OrderByDescending(x => x.SavedAtUtc).Take(Math.Clamp(count, 1, 100)).Select(ToDto).ToListAsync(cancellationToken);

    public async Task<int> CountAsync(string userId, CancellationToken cancellationToken = default) =>
        await PublishedOf(userId).CountAsync(cancellationToken);

    public async Task<bool> SaveAsync(string userId, int articleId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Articles.AsNoTracking().AnyAsync(a => a.Id == articleId && a.Status == ArticleStatus.Published, cancellationToken))
        {
            return false;
        }

        if (await _context.SavedArticles.AnyAsync(x => x.UserId == userId && x.ArticleId == articleId, cancellationToken))
        {
            return true;
        }

        _context.SavedArticles.Add(new SavedArticle { UserId = userId, ArticleId = articleId });
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two tabs saving at once: the composite primary key allows one row, which is exactly what we want.
            if (!await _context.SavedArticles.AsNoTracking().AnyAsync(x => x.UserId == userId && x.ArticleId == articleId, cancellationToken)) throw;
        }
        return true;
    }

    public async Task<bool> RemoveAsync(string userId, int articleId, CancellationToken cancellationToken = default) =>
        await _context.SavedArticles.Where(x => x.UserId == userId && x.ArticleId == articleId).ExecuteDeleteAsync(cancellationToken) > 0;
}
