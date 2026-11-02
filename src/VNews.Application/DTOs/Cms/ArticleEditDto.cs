namespace VNews.Application.DTOs.Cms;

public class ArticleEditDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? CoverCaption { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public int CategoryId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public IReadOnlyList<string> GalleryImages { get; set; } = Array.Empty<string>();
}
