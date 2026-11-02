namespace VNews.Domain.Entities;

/// <summary>A picture shown inside an article (below the text), in addition to its cover. Any number per article, in the editor's order.</summary>
public class ArticleImage
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    /// <summary>The stored picture, normally /uploads/articles/{32 hex}.{jpg|png|webp}.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>0-based position in the editor's list.</summary>
    public int SortOrder { get; set; }
}
