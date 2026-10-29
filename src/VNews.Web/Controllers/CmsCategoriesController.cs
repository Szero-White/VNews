using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class CmsCategoriesController : Controller
{
    private readonly ICategoryQueryService _categoryQueryService;
    private readonly ICategoryCommandService _categoryCommandService;

    public CmsCategoriesController(ICategoryQueryService categoryQueryService, ICategoryCommandService categoryCommandService)
    {
        _categoryQueryService = categoryQueryService;
        _categoryCommandService = categoryCommandService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await _categoryQueryService.GetAllAsync(cancellationToken);
        return View(categories);
    }

    [HttpGet]
    public IActionResult Create() => View(new CategorySaveViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategorySaveViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _categoryCommandService.CreateAsync(model.Name, model.Slug, model.Description, model.DisplayOrder, cancellationToken);
            TempData["Message"] = "Đã tạo chuyên mục.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArticleWorkflowException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var categories = await _categoryQueryService.GetAllAsync(cancellationToken);
        var category = categories.FirstOrDefault(c => c.Id == id);
        if (category is null)
        {
            return NotFound();
        }

        return View(new CategorySaveViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategorySaveViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _categoryCommandService.UpdateAsync(id, model.Name, model.Slug, model.Description, model.DisplayOrder, cancellationToken);
            TempData["Message"] = "Đã cập nhật chuyên mục.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArticleWorkflowException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }
}
