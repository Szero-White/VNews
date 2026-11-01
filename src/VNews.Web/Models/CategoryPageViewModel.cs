using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Web.Models;

public class CategoryPageViewModel
{
    public CategoryDto Category { get; set; } = null!;
    public PagedResult<ArticleSummaryDto> Articles { get; set; } = null!;

    /// <summary>Real most-viewed published articles for the side column (empty when there are none).</summary>
    public IReadOnlyList<ArticleSummaryDto> MostViewed { get; set; } = Array.Empty<ArticleSummaryDto>();
}
