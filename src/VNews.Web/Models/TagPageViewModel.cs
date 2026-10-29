using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Web.Models;

public class TagPageViewModel
{
    public string TagSlug { get; set; } = string.Empty;
    public PagedResult<ArticleSummaryDto> Articles { get; set; } = null!;
}
