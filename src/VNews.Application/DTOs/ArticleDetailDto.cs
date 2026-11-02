namespace VNews.Application.DTOs;

public class ArticleDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? CoverCaption { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }
    /// <summary>Pictures shown below the text, in order.</summary>
    public IReadOnlyList<string> GalleryImages { get; set; } = Array.Empty<string>();
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;

    /// <summary>The parent of a sub-category, for the "Parent › Category" trail; null for a top-level category.</summary>
    public string? CategoryParentName { get; set; }
    public string? CategoryParentSlug { get; set; }
    public IReadOnlyList<TagDto> Tags { get; set; } = Array.Empty<TagDto>();
}

public class TagDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}
