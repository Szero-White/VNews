using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.DTOs;
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

    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _categoryQueryService.GetAllAsync(cancellationToken));

    [HttpGet]
    public Task<IActionResult> Create(CancellationToken cancellationToken) => FormViewAsync(new CategorySaveViewModel(), cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategorySaveViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return await FormViewAsync(model, cancellationToken);

        try
        {
            await _categoryCommandService.CreateAsync(model.Name, model.Slug, model.Description, model.DisplayOrder, model.ParentId, cancellationToken);
            TempData["Message"] = "Đã tạo chuyên mục.";
            return RedirectToAction(nameof(Index));
        }
        catch (ArticleWorkflowException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await FormViewAsync(model, cancellationToken);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = (await _categoryQueryService.GetAllAsync(cancellationToken)).FirstOrDefault(c => c.Id == id);
        if (category is null) return NotFound();

        return await FormViewAsync(new CategorySaveViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            ParentId = category.ParentId,
        }, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CategorySaveViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid) return await FormViewAsync(model, cancellationToken);

        try
        {
            await _categoryCommandService.UpdateAsync(id, model.Name, model.Slug, model.Description, model.DisplayOrder, model.ParentId, cancellationToken);
            TempData["Message"] = "Đã cập nhật chuyên mục.";
            return RedirectToAction(nameof(Index));
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (ArticleWorkflowException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await FormViewAsync(model, cancellationToken);
        }
    }

    /// <summary>The create/edit form. Parent choices are the top-level categories other than this one; a category that already has children cannot be given a parent.</summary>
    private async Task<IActionResult> FormViewAsync(CategorySaveViewModel model, CancellationToken cancellationToken)
    {
        var tree = await _categoryQueryService.GetTreeAsync(cancellationToken);
        ViewData["ParentOptions"] = tree.Where(c => c.Id != model.Id).ToList();
        ViewData["HasChildren"] = tree.Any(c => c.Id == model.Id && c.Children.Count > 0);
        return View(model);
    }
}
