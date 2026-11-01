namespace VNews.Domain.Entities;

public enum CommentStatus
{
    Visible = 0,

    /// <summary>Taken down by an editor or administrator.</summary>
    Hidden = 1,

    /// <summary>Withdrawn by its own author.</summary>
    Deleted = 2,
}

/// <summary>A reader comment on a published article. Replies are one level deep: a reply's parent is always a top-level comment.</summary>
public class Comment
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;

    /// <summary>The display name at the time of writing (a later rename or account removal does not rewrite history).</summary>
    public string AuthorName { get; set; } = string.Empty;

    public int? ParentId { get; set; }
    public Comment? Parent { get; set; }

    /// <summary>Plain text only; it is encoded when rendered, never treated as HTML.</summary>
    public string Body { get; set; } = string.Empty;

    public CommentStatus Status { get; set; } = CommentStatus.Visible;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
