using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Web.Models;

/// <summary>Everything the category page shows, already arranged by the controller so the view only renders.</summary>
public class CategoryPageViewModel
{
    public CategoryDto Category { get; set; } = null!;

    /// <summary>The paged feed (the list at the bottom). Its paging drives the pager; its items are never shown twice on the page or across pages.</summary>
    public PagedResult<ArticleSummaryDto> Articles { get; set; } = null!;

    /// <summary>Page 1 only: the lead story and up to three headline cards above the feed.</summary>
    public ArticleSummaryDto? Lead { get; set; }
    public IReadOnlyList<ArticleSummaryDto> Cards { get; set; } = Array.Empty<ArticleSummaryDto>();

    /// <summary>The stories of the feed on this page.</summary>
    public IReadOnlyList<ArticleSummaryDto> Rows { get; set; } = Array.Empty<ArticleSummaryDto>();

    /// <summary>Real most-viewed published articles for the side column (empty when there are none).</summary>
    public IReadOnlyList<ArticleSummaryDto> MostViewed { get; set; } = Array.Empty<ArticleSummaryDto>();

    /// <summary>The sub-category links shown under the title: the children of this category, or (on a sub-category) its siblings. Empty when the category has none.</summary>
    public IReadOnlyList<CategoryDto> SubCategoryLinks { get; set; } = Array.Empty<CategoryDto>();

    /// <summary>Sub-category blocks (page 1 of a parent category only).</summary>
    public IReadOnlyList<CategoryGroupDto> Groups { get; set; } = Array.Empty<CategoryGroupDto>();

    public bool IsEmpty => Lead is null && Cards.Count == 0 && Rows.Count == 0 && Groups.Count == 0;
}
