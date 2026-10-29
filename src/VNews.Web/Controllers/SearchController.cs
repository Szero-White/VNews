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

        return View(new SearchPageViewModel
        {
            Query = query,
            Articles = articles
        });
    }
}
