using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.Controllers;

public class ArticleController : Controller
{
    private readonly IArticleQueryService _articleQueryService;

    public ArticleController(IArticleQueryService articleQueryService)
    {
        _articleQueryService = articleQueryService;
    }

    public async Task<IActionResult> Detail(string slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var article = await _articleQueryService.GetBySlugAsync(slug, cancellationToken);
        if (article is null)
        {
            return NotFound();
        }

        return View(article);
    }
}
