using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Web.Models;

public class SearchPageViewModel
{
    public string Query { get; set; } = string.Empty;
    public PagedResult<ArticleSummaryDto> Articles { get; set; } = null!;
    public SearchFilter Filter { get; set; } = new();
    public IReadOnlyList<CategoryDto> Categories { get; set; } = Array.Empty<CategoryDto>();

    /// <summary>The query-string values that rebuild the current search (used by the sort tabs and the pager). Empty values are left out.</summary>
    public Dictionary<string, object?> RouteValues(int? page = null, SearchSort? sort = null)
    {
        var values = new Dictionary<string, object?> { ["q"] = Query };
        if (Filter.CategorySlug is not null) values["cat"] = Filter.CategorySlug;
        if (Filter.Period != SearchPeriod.All) values["time"] = Filter.Period.ToString().ToLowerInvariant();
        if (Filter.Kind != SearchKind.All) values["kind"] = Filter.Kind.ToString().ToLowerInvariant();
        if ((sort ?? Filter.Sort) == SearchSort.Relevance) values["sort"] = "relevance";
        if (page is > 1) values["page"] = page;
        return values;
    }
}
