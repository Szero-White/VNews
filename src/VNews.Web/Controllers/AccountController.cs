using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Domain.Identity;
using VNews.Web.Models.Account;

namespace VNews.Web.Controllers;

/// <summary>
/// One Identity store, two clearly separated experiences: readers and newsroom staff.
/// The server validates all return URLs to prevent open redirects.
/// </summary>
public sealed class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocalOrHome(returnUrl);

        return View(new LoginViewModel { ReturnUrl = SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            AddLoginError(result);
            return View(model);
        }

        return RedirectToLocalOrHome(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult CmsLogin(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && IsStaff())
            return RedirectToStaffOrLocal(returnUrl);

        return View(new LoginViewModel { ReturnUrl = SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CmsLogin(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            AddLoginError(result);
            return View(model);
        }

        // A reader account is NEVER allowed to sign in to the newsroom.
        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !await _userManager.IsInRoleAsync(user, Roles.Admin)
            && !await _userManager.IsInRoleAsync(user, Roles.Editor)
            && !await _userManager.IsInRoleAsync(user, Roles.Author))
        {
            await _signInManager.SignOutAsync();
            ModelState.AddModelError(string.Empty, "Tài khoản không có quyền truy cập tòa soạn.");
            return View(model);
        }

        return RedirectToStaffOrLocal(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocalOrHome(returnUrl);
        return View(new RegisterViewModel { ReturnUrl = SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
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

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToLocalOrHome(model.ReturnUrl);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(new ProfileViewModel(user.DisplayName, user.Email ?? string.Empty, IsStaff()));
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

    private void AddLoginError(Microsoft.AspNetCore.Identity.SignInResult result) => ModelState.AddModelError(string.Empty,
        result.IsLockedOut
            ? "Tài khoản tạm khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau."
            : "Email hoặc mật khẩu không đúng.");

    private string? SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    private IActionResult RedirectToLocalOrHome(string? returnUrl) =>
        SafeReturnUrl(returnUrl) is { } path ? LocalRedirect(path) : RedirectToAction("Index", "Home")!;

    private IActionResult RedirectToStaffOrLocal(string? returnUrl) =>
        SafeReturnUrl(returnUrl) is { } path && path.StartsWith("/cms", StringComparison.OrdinalIgnoreCase)
            ? LocalRedirect(path)
            : RedirectToAction("Index", "CmsArticles")!;
}
