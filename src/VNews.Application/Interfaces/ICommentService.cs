using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

public interface ICommentService
{
    public const int MaxLength = 1000;
    public const int MinLength = 2;

    Task<CommentThreadDto> GetThreadsAsync(int articleId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The id of a PUBLISHED article by slug (does not count a view), or null.</summary>
    Task<int?> GetPublishedArticleIdAsync(string slug, CancellationToken cancellationToken = default);

    Task<int> CountVisibleAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>Adds a comment (or a reply when <paramref name="parentId"/> is set) to a PUBLISHED article.</summary>
    Task<CommentResult> AddAsync(int articleId, string userId, string authorName, string body, int? parentId, CancellationToken cancellationToken = default);

    /// <summary>An editor or administrator takes a comment down. Recorded in the audit log.</summary>
    Task<CommentResult> HideAsync(int commentId, string moderatorId, string moderatorName, CancellationToken cancellationToken = default);

    /// <summary>The author withdraws their own comment; nobody else's.</summary>
    Task<CommentResult> DeleteOwnAsync(int commentId, string userId, CancellationToken cancellationToken = default);

    /// <summary>The slug of the article a comment belongs to (for redirects), or null.</summary>
    Task<string?> GetArticleSlugAsync(int commentId, CancellationToken cancellationToken = default);
}
