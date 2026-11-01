using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Web.Demo;
using VNews.Web.Helpers;
using VNews.Web.Models.Account;

namespace VNews.Web.Controllers;

/// <summary>The signed-in user's own account pages: profile (display name) and saved articles. Sign-in and registration are AccountController.</summary>
[Authorize]
public sealed class ReaderAccountController : Controller
{
    private const int RecentSavedCount = 4;
    private const int SavedPageSize = 12;

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISavedArticleService _saved;

    public ReaderAccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ISavedArticleService saved)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _saved = saved;
    }

    private bool IsDemo => User.HasClaim(DemoPersonas.ClaimType, "true");
    private bool IsStaff => User.IsNewsroomStaff();

    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(new ProfileViewModel
        {
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            Roles = (await _userManager.GetRolesAsync(user)).ToList(),
            IsStaff = IsStaff,
            IsDemo = IsDemo,
            SavedCount = await _saved.CountAsync(user.Id, cancellationToken),
            RecentSaved = await _saved.RecentAsync(user.Id, RecentSavedCount, cancellationToken),
        });
    }

    /// <summary>Changes the display name only. Roles, e-mail and password are never touched here; demo accounts are read-only.</summary>
    [HttpPost]
    [ActionName("Profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileModel model)
    {
        if (IsDemo)
        {
            TempData["Error"] = "Tài khoản demo không thể đổi hồ sơ.";
            return RedirectToAction(nameof(Profile));
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Tên hiển thị không hợp lệ.";
            return RedirectToAction(nameof(Profile));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        user.DisplayName = model.DisplayName.Trim();
        var result = await _userManager.UpdateAsync(user);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded ? "Đã cập nhật tên hiển thị." : "Không thể cập nhật hồ sơ lúc này.";
        // Refresh the cookie so the new name shows immediately.
        if (result.Succeeded) await _signInManager.RefreshSignInAsync(user);
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public async Task<IActionResult> SavedArticles(int page = 1, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        return View(new SavedArticlesViewModel
        {
            DisplayName = user.DisplayName,
            Items = await _saved.ListAsync(user.Id, page, SavedPageSize, cancellationToken),
        });
    }

    [HttpPost("tai-khoan/bai-da-luu/bo-luu/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSaved(int id, CancellationToken cancellationToken)
    {
        if (await _saved.RemoveAsync(_userManager.GetUserId(User)!, id, cancellationToken))
        {
            TempData["Message"] = "Đã bỏ lưu bài viết.";
        }
        return RedirectToAction(nameof(SavedArticles));
    }
}
