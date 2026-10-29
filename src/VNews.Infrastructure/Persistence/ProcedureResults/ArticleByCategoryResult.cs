namespace VNews.Infrastructure.Persistence.ProcedureResults;

public class ArticleByCategoryResult
{
    public int ArticleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public int TotalCount { get; set; }
}
