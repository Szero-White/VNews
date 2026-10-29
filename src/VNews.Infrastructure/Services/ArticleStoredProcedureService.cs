using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Infrastructure.Persistence;
using VNews.Infrastructure.Persistence.ProcedureResults;

namespace VNews.Infrastructure.Services;

public class ArticleStoredProcedureService : IArticleStoredProcedureService
{
    private readonly VNewsDbContext _context;

    public ArticleStoredProcedureService(VNewsDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ArticleSummaryDto>> GetPublishedArticlesByCategoryAsync(
        string categorySlug, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 50 ? 10 : pageSize;

        var categorySlugParam = new SqlParameter("@CategorySlug", categorySlug);
        var pageParam = new SqlParameter("@PageNumber", page);
        var pageSizeParam = new SqlParameter("@PageSize", pageSize);

        var rows = await _context.ArticleByCategoryResults
            .FromSqlRaw(
                "EXEC dbo.sp_GetPublishedArticlesByCategory @CategorySlug, @PageNumber, @PageSize",
                categorySlugParam, pageParam, pageSizeParam)
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new ArticleSummaryDto
        {
            Id = r.ArticleId,
            Title = r.Title,
            Slug = r.Slug,
            Summary = r.Summary,
            CoverImageUrl = r.CoverImageUrl,
            AuthorName = r.AuthorName,
            PublishedAt = r.PublishedAt,
            CategoryName = r.CategoryName,
            CategorySlug = r.CategorySlug
        }).ToList();

        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new PagedResult<ArticleSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<IReadOnlyList<DailyPublishStatDto>> GetDailyPublishStatsAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var fromParam = new SqlParameter("@FromDate", fromDate.ToDateTime(TimeOnly.MinValue));
        var toParam = new SqlParameter("@ToDate", toDate.ToDateTime(TimeOnly.MinValue));

        var rows = await _context.DailyPublishStatResults
            .FromSqlRaw("EXEC dbo.sp_GetDailyPublishStats @FromDate, @ToDate", fromParam, toParam)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new DailyPublishStatDto
            {
                PublishDate = DateOnly.FromDateTime(r.PublishDate),
                PublishedCount = r.PublishedCount
            })
            .ToList();
    }
}
