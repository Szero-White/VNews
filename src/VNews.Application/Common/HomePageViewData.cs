using VNews.Application.DTOs;

namespace VNews.Application.Common;

public class HomePageData
{
    public ArticleSummaryDto? Hero { get; init; }
    public IReadOnlyList<ArticleSummaryDto> SideFeatures { get; init; } = Array.Empty<ArticleSummaryDto>();
    public IReadOnlyList<ArticleSummaryDto> Latest { get; init; } = Array.Empty<ArticleSummaryDto>();
    public IReadOnlyList<CategoryPanel> CategoryPanels { get; init; } = Array.Empty<CategoryPanel>();
    public IReadOnlyList<ArticleSummaryDto> MostViewed { get; init; } = Array.Empty<ArticleSummaryDto>();
}

public class CategoryPanel
{
    public CategoryDto Category { get; init; } = null!;
    public IReadOnlyList<ArticleSummaryDto> Articles { get; init; } = Array.Empty<ArticleSummaryDto>();
}
