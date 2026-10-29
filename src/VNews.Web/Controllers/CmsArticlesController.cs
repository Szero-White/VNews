using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Editor + "," + Roles.Author)]
public class CmsArticlesController : Controller
{
    private readonly IArticleCommandService _articleCommandService;
    private readonly ICategoryQueryService _categoryQueryService;
    private readonly IImageUploadService _imageUploadService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CmsArticlesController(
        IArticleCommandService articleCommandService,
        ICategoryQueryService categoryQueryService,
        IImageUploadService imageUploadService,
        UserManager<ApplicationUser> userManager)
    {
        _articleCommandService = articleCommandService;
        _categoryQueryService = categoryQueryService;
        _imageUploadService = imageUploadService;
        _userManager = userManager;
    }

    private string CurrentUserId => _userManager.GetUserId(User)!;
    private bool IsPrivileged => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor);

    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var authorFilter = IsPrivileged ? null : CurrentUserId;
        var result = await _articleCommandService.ListForCmsAsync(status, authorFilter, page, 20, cancellationToken);

        ViewBag.StatusFilter = status;
        ViewBag.IsPrivileged = IsPrivileged;
        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewBag.Categories = await _categoryQueryService.GetAllAsync(cancellationToken);
        return View(new ArticleSaveRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArticleSaveRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _categoryQueryService.GetAllAsync(cancellationToken);
            return View(request);
        }

        var displayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Unknown";
        var user = await _userManager.GetUserAsync(User);
        var authorName = user?.DisplayName ?? displayName;

        var id = await _articleCommandService.CreateDraftAsync(request, CurrentUserId, authorName, cancellationToken);
        TempData["Message"] = "Đã tạo bản nháp.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var article = await _articleCommandService.GetForEditAsync(id, cancellationToken);
        if (article is null)
        {
            return NotFound();
        }

        if (!IsPrivileged && article.AuthorUserId != CurrentUserId)
        {
            return Forbid();
        }

        ViewBag.Categories = await _categoryQueryService.GetAllAsync(cancellationToken);
        ViewBag.Article = article;
        return View(ToSaveRequest(article));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ArticleSaveRequest request, string rowVersionBase64, CancellationToken cancellationToken)
    {
        var existing = await _articleCommandService.GetForEditAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        if (!IsPrivileged && existing.AuthorUserId != CurrentUserId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _categoryQueryService.GetAllAsync(cancellationToken);
            ViewBag.Article = existing;
            return View(request);
        }

        try
        {
            await _articleCommandService.UpdateDraftAsync(id, request, Convert.FromBase64String(rowVersionBase64), CurrentUserId, cancellationToken);
            TempData["Message"] = "Đã lưu thay đổi.";
        }
        catch (ArticleWorkflowException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Categories = await _categoryQueryService.GetAllAsync(cancellationToken);
            ViewBag.Article = existing;
            return View(request);
        }
        catch (ArticleConcurrencyException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Categories = await _categoryQueryService.GetAllAsync(cancellationToken);
            ViewBag.Article = await _articleCommandService.GetForEditAsync(id, cancellationToken);
            return View(request);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int id, CancellationToken cancellationToken)
    {
        var existing = await _articleCommandService.GetForEditAsync(id, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        if (!IsPrivileged && existing.AuthorUserId != CurrentUserId)
        {
            return Forbid();
        }

        try
        {
            await _articleCommandService.SubmitForReviewAsync(id, CurrentUserId, cancellationToken);
            TempData["Message"] = "Đã gửi duyệt.";
        }
        catch (ArticleWorkflowException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Editor)]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        try
        {
            await _articleCommandService.PublishAsync(id, CurrentUserId, user?.DisplayName ?? "Editor", cancellationToken);
            TempData["Message"] = "Đã xuất bản bài viết.";
        }
        catch (ArticleWorkflowException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Editor)]
    public async Task<IActionResult> Reject(int id, string reason, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        try
        {
            await _articleCommandService.RejectAsync(id, CurrentUserId, user?.DisplayName ?? "Editor", reason, cancellationToken);
            TempData["Message"] = "Đã từ chối bài viết.";
        }
        catch (ArticleWorkflowException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadCoverImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Chưa chọn tệp." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _imageUploadService.SaveAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { url = result.Url });
    }

    private static ArticleSaveRequest ToSaveRequest(ArticleEditDto dto) => new()
    {
        Title = dto.Title,
        Summary = dto.Summary,
        Body = dto.Body,
        CoverImageUrl = dto.CoverImageUrl,
        CategoryId = dto.CategoryId
    };
}
