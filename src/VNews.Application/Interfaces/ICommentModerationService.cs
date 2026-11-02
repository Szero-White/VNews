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
    /// <param name="fromUtc">Only comments written at or after this instant.</param>
    /// <param name="toUtcExclusive">Only comments written before this instant.</param>
    Task<PagedResult<CommentAdminItemDto>> ListAsync(string? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default,
        DateTime? fromUtc = null, DateTime? toUtcExclusive = null);

    /// <summary>How many comments are in each state (Visible, Hidden, Deleted), for the figures and tabs of the moderation page.</summary>
    Task<IReadOnlyDictionary<string, int>> CountsAsync(CancellationToken cancellationToken = default);

    /// <summary>The current state of the given comments, so the page shows what really happened (a comment its author deleted never becomes visible).</summary>
    Task<IReadOnlyDictionary<int, string>> StatesAsync(IReadOnlyCollection<int> commentIds, CancellationToken cancellationToken = default);

    /// <summary>Hidden -> Visible. Comments the author deleted themselves stay deleted.</summary>
    Task<CommentResult> RestoreAsync(int commentId, string moderatorId, string moderatorName, CancellationToken cancellationToken = default);
}
