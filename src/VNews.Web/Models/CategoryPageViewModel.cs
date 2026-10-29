using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Web.Models;

public class CategoryPageViewModel
{
    public CategoryDto Category { get; set; } = null!;
    public PagedResult<ArticleSummaryDto> Articles { get; set; } = null!;
}
