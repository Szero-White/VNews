namespace VNews.Application.DTOs.Cms;

/// <summary>One row of the moderation list: the comment, where it was written and its state.</summary>
public sealed class CommentAdminItemDto
{
    public int Id { get; init; }
    public int ArticleId { get; init; }
    public string ArticleTitle { get; init; } = string.Empty;
    public string ArticleSlug { get; init; } = string.Empty;
    public bool ArticleIsPublic { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool IsReply { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
