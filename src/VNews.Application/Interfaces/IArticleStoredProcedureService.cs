using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

public interface IArticleStoredProcedureService
{
    Task<PagedResult<ArticleSummaryDto>> GetPublishedArticlesByCategoryAsync(
        string categorySlug, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyPublishStatDto>> GetDailyPublishStatsAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);
}
