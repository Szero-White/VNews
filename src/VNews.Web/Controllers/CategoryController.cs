using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Web.Models;

namespace VNews.Web.Controllers;

public class CategoryController : Controller
{
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

        var articles = await _articleQueryService.GetByCategoryAsync(slug, page, 12, cancellationToken);

        return View(new CategoryPageViewModel
        {
            Category = category,
            Articles = articles
        });
    }
}
