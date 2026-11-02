using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Web.Models;

namespace VNews.Web.Controllers;

public class SearchController : Controller
{
    private const int PageSize = 12;
    private readonly IArticleQueryService _articleQueryService;
    private readonly ICategoryQueryService _categories;

    public SearchController(IArticleQueryService articleQueryService, ICategoryQueryService categories)
    {
        _articleQueryService = articleQueryService;
        _categories = categories;
    }

    /// <param name="cat">category slug</param>
    /// <param name="time">day | week | month | year (empty = any time)</param>
    /// <param name="kind">original | external (empty = both)</param>
    /// <param name="sort">latest (default) | relevance</param>
    public async Task<IActionResult> Index(string? q, string? cat, string? time, string? kind, string? sort, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = q ?? string.Empty;
        var tree = await _categories.GetTreeAsync(cancellationToken);
        var knownSlugs = tree.SelectMany(c => c.Children.Select(x => x.Slug).Append(c.Slug)).ToHashSet(StringComparer.Ordinal);

        var filter = new SearchFilter(
            cat is not null && knownSlugs.Contains(cat) ? cat : null,
            time switch { "day" => SearchPeriod.Day, "week" => SearchPeriod.Week, "month" => SearchPeriod.Month, "year" => SearchPeriod.Year, _ => SearchPeriod.All },
            kind switch { "original" => SearchKind.Original, "external" => SearchKind.External, _ => SearchKind.All },
            sort == "relevance" ? SearchSort.Relevance : SearchSort.Latest);

        var articles = string.IsNullOrWhiteSpace(query)
            ? new PagedResult<ArticleSummaryDto> { Items = Array.Empty<ArticleSummaryDto>(), Page = page, PageSize = PageSize, TotalCount = 0 }
            : await _articleQueryService.SearchAsync(query, page, PageSize, cancellationToken, filter);

        // A page number past the end (stale link, hand-edited URL) shows the last real page, not "no results".
        if (articles.Items.Count == 0 && articles.TotalCount > 0 && articles.TotalPages > 0 && page > articles.TotalPages)
        {
            articles = await _articleQueryService.SearchAsync(query, articles.TotalPages, PageSize, cancellationToken, filter);
        }

        return View(new SearchPageViewModel
        {
            Query = query,
            Articles = articles,
            Filter = filter,
            Categories = tree,
        });
    }
}
