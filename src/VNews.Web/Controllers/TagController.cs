using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Web.Models;

namespace VNews.Web.Controllers;

public class TagController : Controller
{
    private readonly IArticleQueryService _articleQueryService;

    public TagController(IArticleQueryService articleQueryService)
    {
        _articleQueryService = articleQueryService;
    }

    public async Task<IActionResult> Index(string slug, int page = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var articles = await _articleQueryService.GetByTagAsync(slug, page, 12, cancellationToken);

        return View(new TagPageViewModel
        {
            TagSlug = slug,
            Articles = articles
        });
    }
}
