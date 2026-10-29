using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.ViewComponents;

public class CategoryNavViewComponent : ViewComponent
{
    private readonly ICategoryQueryService _categoryQueryService;

    public CategoryNavViewComponent(ICategoryQueryService categoryQueryService)
    {
        _categoryQueryService = categoryQueryService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var categories = await _categoryQueryService.GetAllAsync();
        return View(categories);
    }
}
