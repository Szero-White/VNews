namespace VNews.Application.DTOs;

public sealed class CommentDto
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>False when the comment was taken down or withdrawn: only a placeholder is shown (kept when replies remain).</summary>
    public bool IsVisible { get; init; } = true;

    public string? RemovedReason { get; init; }
    public IReadOnlyList<CommentDto> Replies { get; init; } = Array.Empty<CommentDto>();
}

public sealed class CommentThreadDto
{
    /// <summary>Visible comments and replies of the article (what the heading counts).</summary>
    public int TotalVisible { get; init; }

    /// <summary>Top-level threads (not comments) for paging.</summary>
    public int ThreadCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public IReadOnlyList<CommentDto> Threads { get; init; } = Array.Empty<CommentDto>();
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(ThreadCount / (double)PageSize);
}

public enum CommentFailure
{
    None,
    ArticleNotFound,
    ParentNotFound,
    EmptyBody,
    TooLong,
    RateLimited,
    Duplicate,
    NotFound,
    Forbidden,
}

public sealed record CommentResult(bool Success, CommentFailure Failure = CommentFailure.None, int CommentId = 0, string? Message = null)
{
    public static CommentResult Ok(int id) => new(true, CommentFailure.None, id);
    public static CommentResult Fail(CommentFailure failure, string message) => new(false, failure, 0, message);
}
