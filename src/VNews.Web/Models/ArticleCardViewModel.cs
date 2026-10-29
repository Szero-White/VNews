using VNews.Application.DTOs;

namespace VNews.Web.Models;

/// <summary>
/// Shared view model for the reusable _ArticleCard partial, used across the homepage,
/// category pages, and search results so card markup is written exactly once.
/// </summary>
public class ArticleCardViewModel
{
    public ArticleSummaryDto Article { get; set; } = null!;

    /// <summary>image-top | image-left | image-top-large | photo-grid | spotlight | podcast-demo | text-only</summary>
    public string Layout { get; set; } = "image-top";

    /// <summary>Optional small label above the title (e.g. "Được quan tâm").</summary>
    public string? Eyebrow { get; set; }
}
