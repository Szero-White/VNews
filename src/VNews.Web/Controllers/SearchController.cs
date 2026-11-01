using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Web.Models;

namespace VNews.Web.Controllers;

public class SearchController : Controller
{
    private readonly IArticleQueryService _articleQueryService;

    public SearchController(IArticleQueryService articleQueryService)
    {
        _articleQueryService = articleQueryService;
    }

    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = q ?? string.Empty;

        var articles = string.IsNullOrWhiteSpace(query)
            ? new PagedResult<ArticleSummaryDto> { Items = Array.Empty<ArticleSummaryDto>(), Page = page, PageSize = 12, TotalCount = 0 }
            : await _articleQueryService.SearchAsync(query, page, 12, cancellationToken);

        // A page number past the end (stale link, hand-edited URL) shows the last real page, not "no results".
        if (articles.Items.Count == 0 && articles.TotalCount > 0 && articles.TotalPages > 0 && page > articles.TotalPages)
        {
            articles = await _articleQueryService.SearchAsync(query, articles.TotalPages, 12, cancellationToken);
        }

        return View(new SearchPageViewModel
        {
            Query = query,
            Articles = articles
        });
    }
}
