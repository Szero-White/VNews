namespace VNews.Application.DTOs.Cms;

public class ArticleCmsListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>Why an editor sent the article back (only set for Rejected articles).</summary>
    public string? ReviewNote { get; set; }

    /// <summary>The cover image, so lists can show whether an article has one.</summary>
    public string? CoverImageUrl { get; set; }
}
