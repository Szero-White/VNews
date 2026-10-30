using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public class ArticleQueryService : IArticleQueryService
{
    private readonly VNewsDbContext _context;

    public ArticleQueryService(VNewsDbContext context)
    {
        _context = context;
    }

    private static readonly Expression<Func<Article, ArticleSummaryDto>> ToSummary = a => new ArticleSummaryDto
    {
        Id = a.Id,
        Title = a.Title,
        Slug = a.Slug,
        Summary = a.Summary,
        CoverImageUrl = a.CoverImageUrl,
        AuthorName = a.AuthorName,
        PublishedAt = a.PublishedAt,
        CategoryName = a.Category.Name,
        CategorySlug = a.Category.Slug
    };

    private IQueryable<Article> PublishedArticles() =>
        _context.Articles
            .Where(a => a.Status == ArticleStatus.Published)
            .Include(a => a.Category);

    public async Task<HomePageData> GetHomePageDataAsync(CancellationToken cancellationToken = default)
    {
        var latestOrdered = PublishedArticles()
            .OrderByDescending(a => a.PublishedAt);

        var topArticles = await latestOrdered
            .Take(6)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        var hero = topArticles.FirstOrDefault();
        var sideFeatures = topArticles.Skip(1).Take(2).ToList();

        // Keep homepage collections disjoint: top 3 for hero, next 9 for the news stream.
        var latest = await latestOrdered
            .Skip(12)
            .Take(10)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        // "Stream tin tổng hợp" — longer mixed-category feed of large cards.
        var streamItems = await latestOrdered
            .Skip(3)
            .Take(9)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        // "Ảnh" — photo-forward grid, most recently published articles with a cover image.
        var photoHighlights = await PublishedArticles()
            .Where(a => a.CoverImageUrl != null)
            .OrderByDescending(a => a.PublishedAt)
            .Take(8)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        var mostViewed = await PublishedArticles()
            .OrderByDescending(a => a.ViewCount)
            .Take(5)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        // "Spotlight" — large feature cards, highest view count.
        var spotlight = await PublishedArticles()
            .OrderByDescending(a => a.ViewCount)
            .Take(3)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        // "Podcasts" demo section — reuses a slice of recent articles, clearly labeled
        // as illustrative (no real audio) by the view, not the data.
        var podcastDemoItems = await latestOrdered
            .Skip(10)
            .Take(4)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

        var categories = await _context.Categories
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        var panels = new List<CategoryPanel>();
        foreach (var category in categories)
        {
            var articles = await PublishedArticles()
                .Where(a => a.CategoryId == category.Id)
                .OrderByDescending(a => a.PublishedAt)
                .Take(5)
                .Select(ToSummary)
                .ToListAsync(cancellationToken);

            if (articles.Count == 0)
            {
                continue;
            }

            panels.Add(new CategoryPanel
            {
                Category = new CategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug,
                    Description = category.Description
                },
                Articles = articles
            });
        }

        return new HomePageData
        {
            Hero = hero,
            SideFeatures = sideFeatures,
            Latest = latest,
            CategoryPanels = panels,
            MostViewed = mostViewed,
            StreamItems = streamItems,
            PhotoHighlights = photoHighlights,
            Spotlight = spotlight,
            PodcastDemoItems = podcastDemoItems
        };
    }

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
                AuthorName = a.AuthorName,
                PublishedAt = a.PublishedAt,
                ViewCount = a.ViewCount,
                CategoryName = a.Category.Name,
                CategorySlug = a.Category.Slug
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (article is not null)
        {
            await _context.Articles
                .Where(a => a.Id == article.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.ViewCount, a => a.ViewCount + 1), cancellationToken);
        }

        return article;
    }

    public async Task<PagedResult<ArticleSummaryDto>> GetLatestAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = PublishedArticles()
            .OrderByDescending(a => a.PublishedAt);

        return await ToPagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<ArticleSummaryDto>> GetByCategoryAsync(
        string categorySlug, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = PublishedArticles()
            .Where(a => a.Category.Slug == categorySlug)
            .OrderByDescending(a => a.PublishedAt);

        return await ToPagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<ArticleSummaryDto>> SearchAsync(
        string query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new PagedResult<ArticleSummaryDto>
            {
                Items = Array.Empty<ArticleSummaryDto>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        var pattern = $"%{query.Trim()}%";

        var filtered = PublishedArticles()
            .Where(a => EF.Functions.Like(a.Title, pattern) || EF.Functions.Like(a.Summary, pattern))
            .OrderByDescending(a => a.PublishedAt);

        return await ToPagedResultAsync(filtered, page, pageSize, cancellationToken);
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
