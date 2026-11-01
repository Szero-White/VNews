namespace VNews.Domain.Entities;

public class Article
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int ViewCount { get; set; }

    public string? AuthorUserId { get; set; }
    public string? ReviewNote { get; set; }
    public string? ReviewedByUserId { get; set; }

    /// <summary>Set while <see cref="ArticleStatus.Trashed"/>: when, by whom, and the status to return to on restore.</summary>
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public ArticleStatus? StatusBeforeDeletion { get; set; }

    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ICollection<ArticleTag> ArticleTags { get; set; } = new List<ArticleTag>();
}
