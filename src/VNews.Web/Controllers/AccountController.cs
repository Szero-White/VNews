using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Web.Demo;
using VNews.Web.Models.Account;
using VNews.Web.Security;

namespace VNews.Web.Controllers;

/// <summary>
/// One Identity store and one sign-in for readers and newsroom staff. What a user may open afterwards is decided
/// by their roles (authorization), and every return URL is validated to prevent open redirects.
/// </summary>
public sealed class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;
    private readonly VNewsDbContext _db;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<AccountController> logger, VNewsDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(PostLoginDestination.For(Url, User.FindAll(ClaimTypes.Role).Select(c => c.Value), SafeReturnUrl(returnUrl)));

        return View(new LoginViewModel { ReturnUrl = SafeReturnUrl(returnUrl) });
    }

    /// <summary>One sign-in for everybody: readers and newsroom staff use the same Identity account and this form.</summary>
    [HttpPost]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return WantsJson ? BadRequest(new { error = "Vui lòng nhập email và mật khẩu hợp lệ." }) : View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Sign-in failed (lockedOut: {LockedOut}) from {RemoteIp}.", result.IsLockedOut, HttpContext.Connection.RemoteIpAddress);
            AddLoginError(result);
            return WantsJson ? Unauthorized(new { error = LoginErrorMessage(result) }) : View(model);
        }

        // The destination depends on the roles the server granted, never on anything the client sent.
        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        var roles = user is null ? Array.Empty<string>() : (IList<string>)await _userManager.GetRolesAsync(user);
        _logger.LogInformation("User {UserId} signed in (roles: {Roles}).", user?.Id, string.Join(",", roles));
        var destination = PostLoginDestination.For(Url, roles, SafeReturnUrl(model.ReturnUrl));
        return WantsJson ? Ok(new { redirect = destination }) : LocalRedirect(destination);
    }

    /// <summary>
    /// Legacy newsroom sign-in address, kept so old links and bookmarks work: it forwards to the shared sign-in page
    /// with a validated local return URL (the CMS home by default). Authorization still decides who may enter.
    /// </summary>
    [HttpGet]
    public IActionResult CmsLogin(string? returnUrl = null)
    {
        var target = SafeReturnUrl(returnUrl);
        if (target is null || !target.StartsWith("/cms", StringComparison.OrdinalIgnoreCase)) target = "/cms";
        return RedirectToAction(nameof(Login), new { returnUrl = target });
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect(PostLoginDestination.For(Url, User.FindAll(ClaimTypes.Role).Select(c => c.Value), SafeReturnUrl(returnUrl)));
        return View(new RegisterViewModel { ReturnUrl = SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var email = model.Email.Trim();
        var user = new ApplicationUser
        {
            Email = email,
            UserName = email,
            DisplayName = model.DisplayName.Trim(),
            EmailConfirmed = false // Production should enable verified-email confirmation.
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var message = error.Code is "DuplicateEmail" or "DuplicateUserName"
                    ? "Email này đã được sử dụng."
                    : error.Description;
                ModelState.AddModelError(string.Empty, message);
            }
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Reader);
        if (!roleResult.Succeeded)
        {
            // Never leave a partially registered, privilege-less account behind.
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Không thể tạo tài khoản lúc này. Vui lòng thử lại.");
            return View(model);
        }

        _logger.LogInformation("Reader account {UserId} registered.", user.Id);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(PostLoginDestination.For(Url, new[] { Roles.Reader }, SafeReturnUrl(model.ReturnUrl)));
    }

    private IQueryable<SavedArticle> SavedOf(string userId) =>
        _db.SavedArticles.AsNoTracking().Where(x => x.UserId == userId && x.Article.Status == ArticleStatus.Published);

    // An expression (not a method call) so EF translates it into the SQL projection.
    private static readonly System.Linq.Expressions.Expression<Func<SavedArticle, SavedArticleRow>> RowProjection =
        x => new SavedArticleRow(x.ArticleId, x.Article.Slug, x.Article.Title, x.Article.CoverImageUrl, x.Article.Category.Name, x.Article.PublishedAt, x.SavedAtUtc);

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var recent = await SavedOf(user.Id).OrderByDescending(x => x.SavedAtUtc).Take(4).Select(RowProjection).ToListAsync(cancellationToken);
        return View(new ProfileViewModel
        {
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            Roles = (await _userManager.GetRolesAsync(user)).ToList(),
            IsStaff = IsStaff(),
            IsDemo = User.HasClaim(DemoPersonas.ClaimType, "true"),
            SavedCount = await SavedOf(user.Id).CountAsync(cancellationToken),
            RecentSaved = recent,
        });
    }

    /// <summary>Changes the display name only. Roles, e-mail and password are never touched here; demo accounts are read-only.</summary>
    [Authorize]
    [HttpPost]
    [ActionName("Profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileModel model)
    {
        if (User.HasClaim(DemoPersonas.ClaimType, "true"))
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

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> SavedArticles(int page = 1, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        const int pageSize = 12;
        var total = await SavedOf(user.Id).CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, lastPage);
        var rows = await SavedOf(user.Id).OrderByDescending(x => x.SavedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(RowProjection).ToListAsync(cancellationToken);

        return View(new SavedArticlesViewModel
        {
            DisplayName = user.DisplayName,
            Items = new PagedResult<SavedArticleRow> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total },
        });
    }

    [Authorize]
    [HttpPost("tai-khoan/bai-da-luu/bo-luu/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSaved(int id, CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        var saved = await _db.SavedArticles.FirstOrDefaultAsync(x => x.UserId == userId && x.ArticleId == id, cancellationToken);
        if (saved is not null)
        {
            _db.SavedArticles.Remove(saved);
            await _db.SaveChangesAsync(cancellationToken);
            TempData["Message"] = "Đã bỏ lưu bài viết.";
        }
        return RedirectToAction(nameof(SavedArticles));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private bool IsStaff() => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor) || User.IsInRole(Roles.Author);

    /// <summary>The sign-in dialog asks for JSON (it stays on the page); full-page posts keep the normal redirect/view flow.</summary>
    private bool WantsJson => string.Equals(Request.Headers["X-Requested-With"], "fetch", StringComparison.Ordinal);

    private static string LoginErrorMessage(Microsoft.AspNetCore.Identity.SignInResult result) =>
        result.IsLockedOut
            ? "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau."
            : "Email hoặc mật khẩu không đúng.";

    private void AddLoginError(Microsoft.AspNetCore.Identity.SignInResult result) => ModelState.AddModelError(string.Empty, LoginErrorMessage(result));

    private string? SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
}
