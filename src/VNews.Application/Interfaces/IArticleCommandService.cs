using VNews.Application.Common;
using VNews.Application.DTOs.Cms;

namespace VNews.Application.Interfaces;

public interface IArticleCommandService
{
    Task<PagedResult<ArticleCmsListItemDto>> ListForCmsAsync(
        string? statusFilter, string? authorUserId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<ArticleEditDto?> GetForEditAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new article as Draft, owned by the given author. The article and its audit-log
    /// entry are committed in ONE transaction: if the audit write fails, no article is created.
    /// </summary>
    Task<int> CreateDraftAsync(ArticleSaveRequest request, string authorUserId, string authorDisplayName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing Draft or Rejected article.
    /// Throws <see cref="Common.ArticleConcurrencyException"/> on RowVersion mismatch and
    /// <see cref="Common.ArticleForbiddenException"/> when a non-privileged user does not own the article.
    /// </summary>
    Task UpdateDraftAsync(int articleId, ArticleSaveRequest request, byte[] rowVersion, string actingUserId, bool canManageAll = false, CancellationToken cancellationToken = default);

    Task SubmitForReviewAsync(int articleId, string actingUserId, bool canManageAll = false, CancellationToken cancellationToken = default);

    Task PublishAsync(int articleId, string editorUserId, string editorDisplayName, CancellationToken cancellationToken = default);

    Task RejectAsync(int articleId, string editorUserId, string editorDisplayName, string reason, CancellationToken cancellationToken = default);
}
