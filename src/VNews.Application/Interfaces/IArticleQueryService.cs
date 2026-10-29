using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

public interface IArticleQueryService
{
    Task<HomePageData> GetHomePageDataAsync(CancellationToken cancellationToken = default);

    Task<ArticleDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<PagedResult<ArticleSummaryDto>> GetByCategoryAsync(
        string categorySlug, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<ArticleSummaryDto>> SearchAsync(
        string query, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<ArticleSummaryDto>> GetByTagAsync(
        string tagSlug, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<ArticleSummaryDto>> GetLatestAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);
}
