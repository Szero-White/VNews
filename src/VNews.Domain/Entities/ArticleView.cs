namespace VNews.Domain.Entities;

/// <summary>
/// One real reading of a published article. Anonymous: the visitor is only a one-way hash that changes every day, so no IP address or
/// user agent is stored and a person cannot be followed across days.
/// </summary>
public class ArticleView
{
    public long Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public DateTime ViewedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Hash of the visitor for the day of the view; used to count unique readers and to ignore reloads.</summary>
    public string VisitorHash { get; set; } = string.Empty;

    public bool IsMobile { get; set; }
}
