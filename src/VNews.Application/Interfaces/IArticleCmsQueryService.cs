using VNews.Application.Common;
using VNews.Application.DTOs.Cms;

namespace VNews.Application.Interfaces;

/// <summary>Read side of the newsroom: lists, counts and the data of the edit form. Changes nothing.</summary>
public interface IArticleCmsQueryService
{
    /// <param name="search">Optional words matched (accent- and case-insensitively) against the title or the author name.</param>
    /// <param name="oldestFirst">Review queues work oldest first; everything else lists the most recently changed first.</param>
    Task<PagedResult<ArticleCmsListItemDto>> ListForCmsAsync(
        string? statusFilter, string? authorUserId, int page, int pageSize, CancellationToken cancellationToken = default,
        string? search = null, bool oldestFirst = false,
        int? categoryId = null, DateTime? updatedFrom = null, DateTime? updatedTo = null, string? authorName = null);

    /// <summary>The author names that appear on at least one article (not in the trash), for the author filter.</summary>
    Task<IReadOnlyList<string>> GetAuthorNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Counts per status; pass an author id to count only that author's articles.</summary>
    Task<CmsStatsDto> GetCmsStatsAsync(string? authorUserId, CancellationToken cancellationToken = default);

    Task<ArticleEditDto?> GetForEditAsync(int articleId, CancellationToken cancellationToken = default);
}
