using VNews.Application.DTOs;

namespace VNews.Web.Models;

/// <summary>Supported editorial card presentations; avoids unvalidated layout strings.</summary>
public enum ArticleCardLayout
{
    ImageTop,
    HeroLead,
    HeroSide,
    StoryRow,
    CategoryLead,
    SpotlightFeature,
    StreamCompact,
    ListItem,
    ListItemThumbnail,
    PhotoGrid,
    TextOnly
}

/// <summary>Model for a single reusable article-card partial.</summary>
public sealed class ArticleCardViewModel
{
    public required ArticleSummaryDto Article { get; init; }
    public ArticleCardLayout Layout { get; init; } = ArticleCardLayout.ImageTop;
    public string? Eyebrow { get; init; }
    public bool UsePageHeading { get; init; }
}
