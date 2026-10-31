namespace VNews.Domain.Entities;

/// <summary>A reader's bookmark for a published article. Composite key prevents duplicates.</summary>
public sealed class SavedArticle
{
    public string UserId { get; set; } = string.Empty;
    public int ArticleId { get; set; }
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    public Article Article { get; set; } = null!;
}
