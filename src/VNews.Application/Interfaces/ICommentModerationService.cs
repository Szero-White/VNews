using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.DTOs.Cms;

namespace VNews.Application.Interfaces;

/// <summary>
/// The CMS side of comments: list and filter every comment, and bring back one an editor took down. Taking a comment down is
/// <see cref="ICommentService.HideAsync"/>. Comments are never deleted from the database.
/// </summary>
public interface ICommentModerationService
{
    /// <param name="status">Visible, Hidden or Deleted (deleted by the author); null lists all.</param>
    Task<PagedResult<CommentAdminItemDto>> ListAsync(string? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Hidden -> Visible. Comments the author deleted themselves stay deleted.</summary>
    Task<CommentResult> RestoreAsync(int commentId, string moderatorId, string moderatorName, CancellationToken cancellationToken = default);
}
