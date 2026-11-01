using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Common;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Web.Helpers;
using VNews.Web.Services;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Editor + "," + Roles.Author)]
public class CmsArticlesController : Controller
{
    private readonly IArticleCommandService _articleCommandService;
    private readonly IArticleCmsQueryService _cmsQuery;
    private readonly CmsWorkspaceBuilder _workspace;
    private readonly ICategoryQueryService _categoryQueryService;
    private readonly IImageUploadService _imageUploadService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CmsArticlesController(
        IArticleCommandService articleCommandService,
        IArticleCmsQueryService cmsQuery,
        CmsWorkspaceBuilder workspace,
        ICategoryQueryService categoryQueryService,
        IImageUploadService imageUploadService,
        UserManager<ApplicationUser> userManager)
    {
        _articleCommandService = articleCommandService;
        _cmsQuery = cmsQuery;
        _workspace = workspace;
        _categoryQueryService = categoryQueryService;
        _imageUploadService = imageUploadService;
        _userManager = userManager;
    }

    private async Task<string> CurrentDisplayNameAsync() => (await _userManager.GetUserAsync(User))?.DisplayName ?? User.Identity?.Name ?? "Unknown";

    private string CurrentUserId => _userManager.GetUserId(User)!;
    private bool IsPrivileged => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor);

    public async Task<IActionResult> Index(string? status, string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        var viewer = new CmsWorkspaceBuilder.Viewer(
            CurrentUserId,
            await CurrentDisplayNameAsync(),
            IsPrivileged,
            AppFormat.PrimaryRole(User.FindAll(ClaimTypes.Role).Select(c => c.Value)));
        return View(await _workspace.BuildAsync(viewer, status, q, page, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return await FormViewAsync(new ArticleSaveRequest(), null, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArticleSaveRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await FormViewAsync(request, null, cancellationToken);
        }

        var displayName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Unknown";
        var user = await _userManager.GetUserAsync(User);
        var authorName = user?.DisplayName ?? displayName;

        try
        {
            var id = await _articleCommandService.CreateDraftAsync(request, CurrentUserId, authorName, cancellationToken);
            TempData["Message"] = "Đã tạo bản nháp.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (ArticleWorkflowException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await FormViewAsync(request, null, cancellationToken);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var article = await _cmsQuery.GetForEditAsync(id, cancellationToken);
        if (article is null)
        {
            return NotFound();
        }

        if (!IsPrivileged && article.AuthorUserId != CurrentUserId)
        {
            return Forbid();
        }

        return await FormViewAsync(ToSaveRequest(article), article, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ArticleSaveRequest request, string rowVersionBase64, CancellationToken cancellationToken)
    {
        var existing = await _cmsQuery.GetForEditAsync(id, cancellationToken);
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
            return await FormViewAsync(request, existing, cancellationToken);
        }

        if (!TryReadRowVersion(rowVersionBase64, out var rowVersion))
        {
            ModelState.AddModelError(string.Empty, "Phiên bản bài viết không hợp lệ. Vui lòng tải lại trang và thử lại.");
            return await FormViewAsync(request, existing, cancellationToken);
        }

        try
        {
            await _articleCommandService.UpdateAsync(id, request, rowVersion, CurrentUserId, IsPrivileged, cancellationToken, await CurrentDisplayNameAsync());
            TempData["Message"] = "Đã lưu thay đổi.";
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (ArticleForbiddenException)
        {
            return Forbid();
        }
        catch (ArticleWorkflowException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await FormViewAsync(request, existing, cancellationToken);
        }
        catch (ArticleConcurrencyException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await FormViewAsync(request, await _cmsQuery.GetForEditAsync(id, cancellationToken), cancellationToken);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int id, CancellationToken cancellationToken)
    {
        var existing = await _cmsQuery.GetForEditAsync(id, cancellationToken);
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
            await _articleCommandService.SubmitForReviewAsync(id, CurrentUserId, IsPrivileged, cancellationToken);
            TempData["Message"] = "Đã gửi duyệt.";
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (ArticleForbiddenException)
        {
            return Forbid();
        }
        catch (Exception ex) when (ex is ArticleWorkflowException or ArticleConcurrencyException)
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
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is ArticleWorkflowException or ArticleConcurrencyException)
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
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is ArticleWorkflowException or ArticleConcurrencyException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ImageUploadLimits.RequestBytes)]
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

    /// <summary>The create/edit form: categories for the select, and (when editing) the stored article for the status and review note.</summary>
    private async Task<IActionResult> FormViewAsync(ArticleSaveRequest model, ArticleEditDto? article, CancellationToken cancellationToken)
    {
        ViewBag.Categories = await _categoryQueryService.GetAllAsync(cancellationToken);
        if (article is not null) ViewBag.Article = article;
        return View(model);
    }

    private static bool TryReadRowVersion(string? base64, out byte[] rowVersion)
    {
        rowVersion = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(base64) || base64.Length > 64)
        {
            return false;
        }

        var buffer = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
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
