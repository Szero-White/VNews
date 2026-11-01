using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Web.Models;

namespace VNews.Web.Controllers;

public class CategoryController : Controller
{
    private const int PageSize = 12;

    private readonly IArticleQueryService _articleQueryService;
    private readonly ICategoryQueryService _categoryQueryService;

    public CategoryController(IArticleQueryService articleQueryService, ICategoryQueryService categoryQueryService)
    {
        _articleQueryService = articleQueryService;
        _categoryQueryService = categoryQueryService;
    }

    public async Task<IActionResult> Index(string slug, int page = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var category = await _categoryQueryService.GetBySlugAsync(slug, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        var articles = await _articleQueryService.GetByCategoryAsync(slug, page, PageSize, cancellationToken);

        // A page number past the end (stale link, hand-edited URL) shows the last real page, not an empty one.
        if (articles.Items.Count == 0 && articles.TotalCount > 0 && page > articles.TotalPages)
        {
            articles = await _articleQueryService.GetByCategoryAsync(slug, articles.TotalPages, PageSize, cancellationToken);
        }

        var mostViewed = await _articleQueryService.GetMostViewedAsync(5, null, cancellationToken);
        return View(new CategoryPageViewModel
        {
            Category = category,
            Articles = articles,
            MostViewed = mostViewed
        });
    }
}
