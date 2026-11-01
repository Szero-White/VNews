using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.ViewComponents;

/// <summary>
/// Public header panel. Displays published articles from the existing query service,
/// not fake push notifications or a second persistence model.
/// </summary>
public sealed class ReaderNewsPanelViewComponent : ViewComponent
{
    private readonly IArticleQueryService _articles;

    public ReaderNewsPanelViewComponent(IArticleQueryService articles) => _articles = articles;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var items = await _articles.GetLatestAsync(1, 5, HttpContext.RequestAborted);
        return View(items.Items);
    }
}
