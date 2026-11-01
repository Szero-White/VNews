using VNews.Application.DTOs.Cms;

namespace VNews.Application.Interfaces;

/// <summary>Everything that changes an article: create, edit, the review workflow. Reading for the CMS is <see cref="IArticleCmsQueryService"/>.</summary>
public interface IArticleCommandService
{
    /// <summary>
    /// Creates a new article as Draft, owned by the given author. The article and its audit-log
    /// entry are committed in ONE transaction: if the audit write fails, no article is created.
    /// </summary>
    Task<int> CreateDraftAsync(ArticleSaveRequest request, string authorUserId, string authorDisplayName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an article: a Draft or Rejected one by its author (or an editor/admin), a Published one by an editor/admin only.
    /// Throws <see cref="Common.ArticleConcurrencyException"/> on RowVersion mismatch and
    /// <see cref="Common.ArticleForbiddenException"/> when a non-privileged user does not own the article.
    /// </summary>
    Task UpdateAsync(int articleId, ArticleSaveRequest request, byte[] rowVersion, string actingUserId, bool canManageAll = false, CancellationToken cancellationToken = default, string? actingUserName = null);

    Task SubmitForReviewAsync(int articleId, string actingUserId, bool canManageAll = false, CancellationToken cancellationToken = default);

    Task PublishAsync(int articleId, string editorUserId, string editorDisplayName, CancellationToken cancellationToken = default);

    Task RejectAsync(int articleId, string editorUserId, string editorDisplayName, string reason, CancellationToken cancellationToken = default);
}
