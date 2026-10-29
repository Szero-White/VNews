using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Web.Models;

public class SearchPageViewModel
{
    public string Query { get; set; } = string.Empty;
    public PagedResult<ArticleSummaryDto> Articles { get; set; } = null!;
}
