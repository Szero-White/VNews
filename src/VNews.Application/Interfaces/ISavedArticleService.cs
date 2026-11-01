using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

/// <summary>A signed-in reader's saved articles. Every call is scoped to the given user id; nobody can read or change another user's list.</summary>
public interface ISavedArticleService
{
    Task<PagedResult<SavedArticleDto>> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedArticleDto>> RecentAsync(string userId, int count, CancellationToken cancellationToken = default);

    Task<int> CountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Saves a PUBLISHED article; saving twice is harmless. Returns false when the article does not exist or is not published.</summary>
    Task<bool> SaveAsync(string userId, int articleId, CancellationToken cancellationToken = default);

    /// <summary>Returns true when something was removed.</summary>
    Task<bool> RemoveAsync(string userId, int articleId, CancellationToken cancellationToken = default);
}
