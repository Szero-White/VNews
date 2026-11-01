using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Web.Models;

namespace VNews.Web.ViewComponents;

/// <summary>Real categories from the database; marks the one being read (category page or an article in it).</summary>
public class CategoryNavViewComponent : ViewComponent
{
    /// <summary>Set by ArticleController so an article highlights its own category.</summary>
    public const string ActiveCategoryItemKey = "ActiveCategorySlug";

    private readonly ICategoryQueryService _categoryQueryService;

    public CategoryNavViewComponent(ICategoryQueryService categoryQueryService)
    {
        _categoryQueryService = categoryQueryService;
    }

    /// <param name="variant">"strip" (horizontal navigation), "drawer" (list inside the mobile menu) or "mega" (desktop menu).</param>
    public async Task<IViewComponentResult> InvokeAsync(string variant = "strip")
    {
        var categories = await _categoryQueryService.GetTreeAsync();
        var model = new CategoryNavViewModel { Categories = categories, ActiveSlug = ResolveActiveSlug() };
        return variant switch
        {
            "drawer" => View("Drawer", model),
            "mega" => View("Mega", model),
            _ => View(model)
        };
    }

    private string? ResolveActiveSlug()
    {
        if (HttpContext.Items[ActiveCategoryItemKey] is string fromItems && fromItems.Length > 0)
        {
            return fromItems;
        }

        var route = ViewContext.RouteData.Values;
        return string.Equals(route["controller"]?.ToString(), "Category", StringComparison.OrdinalIgnoreCase)
            ? route["slug"]?.ToString()
            : null;
    }
}
