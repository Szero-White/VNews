using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Web.Models;

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

        HttpContext.Items[VNews.Web.ViewComponents.CategoryNavViewComponent.ActiveCategoryItemKey] = article.CategorySlug;
        var related = await _articleQueryService.GetByCategoryAsync(article.CategorySlug, 1, 8, cancellationToken);
        var mostViewed = await _articleQueryService.GetMostViewedAsync(5, article.Id, cancellationToken);
        return View(new ArticleDetailPageViewModel
        {
            Article = article,
            RelatedArticles = related.Items.Where(item => item.Id != article.Id).Take(4).ToArray(),
            MostViewed = mostViewed
        });
    }
}
