using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Common;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public class ArticleQueryService : IArticleQueryService
{
    private readonly VNewsDbContext _context;

    public ArticleQueryService(VNewsDbContext context)
    {
        _context = context;
    }

    private static readonly Expression<Func<Article, ArticleSummaryDto>> ToSummary = ArticleProjections.ToSummary;

    private IQueryable<Article> PublishedArticles() =>
        _context.Articles
            .Where(a => a.Status == ArticleStatus.Published)
            .Include(a => a.Category);

    public async Task<ArticleDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var article = await PublishedArticles()
            .Where(a => a.Slug == slug)
            .Select(a => new ArticleDetailDto
            {
                Id = a.Id,
                Title = a.Title,
                Slug = a.Slug,
                Summary = a.Summary,
                Body = a.Body,
                CoverImageUrl = a.CoverImageUrl,
                CoverCaption = a.CoverCaption,
                AuthorName = a.AuthorName,
                PublishedAt = a.PublishedAt,
                ViewCount = a.ViewCount,
                SourceName = a.SourceName,
                SourceUrl = a.SourceUrl,
                CategoryName = a.Category.Name,
                CategorySlug = a.Category.Slug,
                Tags = a.ArticleTags
                    .OrderBy(at => at.Tag.Name)
                    .Select(at => new TagDto { Name = at.Tag.Name, Slug = at.Tag.Slug })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (article is not null)
        {
            article.GalleryImages = await _context.ArticleImages.AsNoTracking()
                .Where(i => i.ArticleId == article.Id).OrderBy(i => i.SortOrder).Select(i => i.Url).ToListAsync(cancellationToken);
            await _context.Articles
                .Where(a => a.Id == article.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.ViewCount, a => a.ViewCount + 1), cancellationToken);
        }

        return article;
    }

    public async Task<ArticleNeighborsDto> GetNeighborsAsync(int articleId, CancellationToken cancellationToken = default)
    {
        var current = await _context.Articles.AsNoTracking()
            .Where(a => a.Id == articleId && a.Status == ArticleStatus.Published)
            .Select(a => new { a.Id, a.CategoryId, a.PublishedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (current?.PublishedAt is not DateTime at) return new ArticleNeighborsDto();

        var sameCategory = PublishedArticles().AsNoTracking().Where(a => a.CategoryId == current.CategoryId && a.Id != current.Id && a.PublishedAt != null);
        // Ties on the publish time fall back to the id so the order is total and the two links never point at the same article.
        var previous = await sameCategory.Where(a => a.PublishedAt < at || (a.PublishedAt == at && a.Id < current.Id))
            .OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id).Select(ToSummary).FirstOrDefaultAsync(cancellationToken);
        var next = await sameCategory.Where(a => a.PublishedAt > at || (a.PublishedAt == at && a.Id > current.Id))
            .OrderBy(a => a.PublishedAt).ThenBy(a => a.Id).Select(ToSummary).FirstOrDefaultAsync(cancellationToken);
        return new ArticleNeighborsDto { Previous = previous, Next = next };
    }

    public async Task<IReadOnlyList<ArticleSummaryDto>> GetMostViewedAsync(
        int count, int? excludeArticleId = null, CancellationToken cancellationToken = default)
    {
        var query = PublishedArticles();
        if (excludeArticleId is int excluded)
        {
            query = query.Where(a => a.Id != excluded);
        }

        return await query
            .OrderByDescending(a => a.ViewCount)
            .ThenByDescending(a => a.PublishedAt)
            .Take(count)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ArticleSummaryDto>> GetLatestAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = PublishedArticles()
            .OrderByDescending(a => a.PublishedAt);

        return await ToPagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryGroupDto>> GetSubCategoryGroupsAsync(
        string parentSlug, int perGroup, IReadOnlyCollection<int> excludeIds, CancellationToken cancellationToken = default)
    {
        var children = await _context.Categories.AsNoTracking()
            .Where(c => c.Parent != null && c.Parent.Slug == parentSlug)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var groups = new List<CategoryGroupDto>();
        var alreadyShown = excludeIds.ToList();
        foreach (var child in children)
        {
            var articles = await PublishedArticles().AsNoTracking()
                .Where(a => a.CategoryId == child.Id && !alreadyShown.Contains(a.Id))
                .OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id)
                .Take(perGroup)
                .Select(ToSummary)
                .ToListAsync(cancellationToken);
            if (articles.Count == 0) continue; // a sub-category with nothing new gets no empty block

            alreadyShown.AddRange(articles.Select(a => a.Id)); // and no story appears in two groups
            groups.Add(new CategoryGroupDto
            {
                Category = new CategoryDto { Id = child.Id, Name = child.Name, Slug = child.Slug, ParentId = child.ParentId, ParentSlug = parentSlug },
                Articles = articles,
            });
        }
        return groups;
    }

    public async Task<PagedResult<ArticleSummaryDto>> GetByCategoryAsync(
        string categorySlug, int page, int pageSize, CancellationToken cancellationToken = default, IReadOnlyCollection<int>? excludeIds = null)
    {
        var excluded = excludeIds?.ToList() ?? new List<int>();
        var query = PublishedArticles()
            .Where(a => !excluded.Contains(a.Id))
            // A parent category lists its own articles and those of its sub-categories; a sub-category lists only its own.
            .Where(a => a.Category.Slug == categorySlug || (a.Category.Parent != null && a.Category.Parent.Slug == categorySlug))
            .OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id);

        return await ToPagedResultAsync(query, page, pageSize, cancellationToken);
    }

    // Accent- and case-insensitive collation: "vang" matches "vàng". It does not fold đ/Đ, so those are replaced
    // by d on the column side, as SearchText does on the keyword side.
    private const string SearchCollation = "Latin1_General_100_CI_AI";

    public async Task<PagedResult<ArticleSummaryDto>> SearchAsync(
        string query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var tokens = SearchText.Tokenize(query);
        if (tokens.Count == 0)
        {
            return new PagedResult<ArticleSummaryDto>
            {
                Items = Array.Empty<ArticleSummaryDto>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        // Every keyword must appear in the title or the summary. Keywords are letters/digits only and are sent as
        // parameters (never concatenated into SQL). The body is not searched: it is stored HTML (tag and attribute
        // noise) and scanning nvarchar(max) needs a full-text index, which would need a migration.
        var filtered = PublishedArticles();
        foreach (var token in tokens)
        {
            var pattern = $"%{token}%";
            filtered = filtered.Where(a =>
                EF.Functions.Like(EF.Functions.Collate(a.Title, SearchCollation).Replace("đ", "d"), pattern)
                || EF.Functions.Like(EF.Functions.Collate(a.Summary, SearchCollation).Replace("đ", "d"), pattern));
        }

        // Title hits on the whole phrase first, then newest; Id makes the order total so pages never repeat an article.
        var phrase = $"%{string.Join(' ', tokens)}%";
        var ordered = filtered
            .OrderByDescending(a => EF.Functions.Like(EF.Functions.Collate(a.Title, SearchCollation).Replace("đ", "d"), phrase))
            .ThenByDescending(a => a.PublishedAt)
            .ThenByDescending(a => a.Id);

        return await ToPagedResultAsync(ordered, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<ArticleSummaryDto>> GetByTagAsync(
        string tagSlug, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = PublishedArticles()
            .Where(a => a.ArticleTags.Any(at => at.Tag.Slug == tagSlug))
            .OrderByDescending(a => a.PublishedAt);

        return await ToPagedResultAsync(query, page, pageSize, cancellationToken);
    }

    private static async Task<PagedResult<ArticleSummaryDto>> ToPagedResultAsync(
        IOrderedQueryable<Article> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 50 ? 10 : pageSize;

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        return new PagedResult<ArticleSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
