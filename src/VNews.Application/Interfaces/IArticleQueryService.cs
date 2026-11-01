using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

public interface IArticleQueryService
{
    Task<ArticleDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Published articles of a category (a parent includes its sub-categories), newest first. <paramref name="excludeIds"/> removes stories shown elsewhere on the page, before paging, so no page repeats another.</summary>
    Task<PagedResult<ArticleSummaryDto>> GetByCategoryAsync(
        string categorySlug, int page, int pageSize, CancellationToken cancellationToken = default, IReadOnlyCollection<int>? excludeIds = null);

    /// <summary>Newest published articles per sub-category of a parent category, skipping the given ids (so a story is not shown twice on one page).</summary>
    Task<IReadOnlyList<CategoryGroupDto>> GetSubCategoryGroupsAsync(
        string parentSlug, int perGroup, IReadOnlyCollection<int> excludeIds, CancellationToken cancellationToken = default);

    Task<PagedResult<ArticleSummaryDto>> SearchAsync(
        string query, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<ArticleSummaryDto>> GetByTagAsync(
        string tagSlug, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Published articles ordered by the persisted ViewCount (incremented on each article read).</summary>
    Task<IReadOnlyList<ArticleSummaryDto>> GetMostViewedAsync(
        int count, int? excludeArticleId = null, CancellationToken cancellationToken = default);

    /// <summary>The published articles right before (older) and after (newer) this one in its category, by publish time.</summary>
    Task<ArticleNeighborsDto> GetNeighborsAsync(int articleId, CancellationToken cancellationToken = default);

    Task<PagedResult<ArticleSummaryDto>> GetLatestAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);
}
