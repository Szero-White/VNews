using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Common;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

/// <summary>
/// Builds the home page. The sections are disjoint slices of the same newest-first order (hero 1 + side 2, stream 3-11, podcasts 10-13,
/// latest 12-21) plus rankings by views and a photo grid. Read-only: nothing here changes data.
/// </summary>
public sealed class HomePageQueryService : IHomePageQueryService
{
    private const int PanelArticleCount = 5;

    private readonly VNewsDbContext _context;

    public HomePageQueryService(VNewsDbContext context) => _context = context;

    private IQueryable<Article> PublishedArticles() =>
        _context.Articles.AsNoTracking().Where(a => a.Status == ArticleStatus.Published);

    private async Task<List<ArticleSummaryDto>> SliceAsync(IQueryable<Article> ordered, int skip, int take, CancellationToken cancellationToken) =>
        await ordered.Skip(skip).Take(take).Select(ArticleProjections.ToSummary).ToListAsync(cancellationToken);

    public async Task<HomePageData> GetHomePageDataAsync(CancellationToken cancellationToken = default)
    {
        var newest = PublishedArticles().OrderByDescending(a => a.PublishedAt);
        var byViews = PublishedArticles().OrderByDescending(a => a.ViewCount);

        var top = await SliceAsync(newest, 0, 6, cancellationToken);

        return new HomePageData
        {
            Hero = top.FirstOrDefault(),
            SideFeatures = top.Skip(1).Take(2).ToList(),
            // Keep homepage collections disjoint: hero and side features first, then the stream, then the latest list.
            StreamItems = await SliceAsync(newest, 3, 9, cancellationToken),                // "Stream tin tổng hợp": longer mixed-category feed
            Latest = await SliceAsync(newest, 12, 10, cancellationToken),
            PodcastDemoItems = await SliceAsync(newest, 10, 4, cancellationToken),         // illustrative section, labelled as such by the view
            PhotoHighlights = await SliceAsync(PublishedArticles().Where(a => a.CoverImageUrl != null).OrderByDescending(a => a.PublishedAt), 0, 8, cancellationToken),
            MostViewed = await SliceAsync(byViews, 0, 5, cancellationToken),
            Spotlight = await SliceAsync(byViews, 0, 3, cancellationToken),                // "Spotlight": large cards, highest view count
            CategoryPanels = await LoadCategoryPanelsAsync(cancellationToken),
        };
    }

    /// <summary>The newest stories of every category in ONE query (a correlated Take per category), not one query per category.</summary>
    private async Task<List<CategoryPanel>> LoadCategoryPanelsAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Categories.AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                Articles = c.Articles
                    .Where(a => a.Status == ArticleStatus.Published)
                    .OrderByDescending(a => a.PublishedAt)
                    .Take(PanelArticleCount)
                    .Select(a => new ArticleSummaryDto
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Slug = a.Slug,
                        Summary = a.Summary,
                        CoverImageUrl = a.CoverImageUrl,
                        AuthorName = a.AuthorName,
                        PublishedAt = a.PublishedAt,
                        CategoryName = c.Name,
                        CategorySlug = c.Slug
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Articles.Count > 0) // a category with nothing published gets no panel
            .Select(r => new CategoryPanel
            {
                Category = new CategoryDto { Id = r.Id, Name = r.Name, Slug = r.Slug, Description = r.Description },
                Articles = r.Articles,
            })
            .ToList();
    }
}
