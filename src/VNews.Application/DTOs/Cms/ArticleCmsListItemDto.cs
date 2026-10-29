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
}
