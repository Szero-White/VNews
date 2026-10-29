using VNews.Application.DTOs;

namespace VNews.Application.Common;

public class HomePageData
{
    public ArticleSummaryDto? Hero { get; init; }
    public IReadOnlyList<ArticleSummaryDto> SideFeatures { get; init; } = Array.Empty<ArticleSummaryDto>();
    public IReadOnlyList<ArticleSummaryDto> Latest { get; init; } = Array.Empty<ArticleSummaryDto>();
    public IReadOnlyList<CategoryPanel> CategoryPanels { get; init; } = Array.Empty<CategoryPanel>();
    public IReadOnlyList<ArticleSummaryDto> MostViewed { get; init; } = Array.Empty<ArticleSummaryDto>();

    /// <summary>Long mixed-category feed ("stream tin tổng hợp"), large image-top cards.</summary>
    public IReadOnlyList<ArticleSummaryDto> StreamItems { get; init; } = Array.Empty<ArticleSummaryDto>();

    /// <summary>Photo-forward grid ("Ảnh") — reuses article cover images.</summary>
    public IReadOnlyList<ArticleSummaryDto> PhotoHighlights { get; init; } = Array.Empty<ArticleSummaryDto>();

    /// <summary>Large feature cards ("Spotlight") chosen by highest view count.</summary>
    public IReadOnlyList<ArticleSummaryDto> Spotlight { get; init; } = Array.Empty<ArticleSummaryDto>();

    /// <summary>Demo-only "Podcasts" style section; no real audio, clearly labeled as illustrative.</summary>
    public IReadOnlyList<ArticleSummaryDto> PodcastDemoItems { get; init; } = Array.Empty<ArticleSummaryDto>();
}

public class CategoryPanel
{
    public CategoryDto Category { get; init; } = null!;
    public IReadOnlyList<ArticleSummaryDto> Articles { get; init; } = Array.Empty<ArticleSummaryDto>();
}
