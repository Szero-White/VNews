using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly ILogger<AccountController> _logger;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocalOrHome(returnUrl);

        return View(new LoginViewModel { ReturnUrl = SafeReturnUrl(returnUrl) });
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Reader sign-in failed (lockedOut: {LockedOut}) from {RemoteIp}.", result.IsLockedOut, HttpContext.Connection.RemoteIpAddress);
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
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CmsLogin(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Verify the password WITHOUT issuing a cookie, check the role, and only then sign in:
        // a reader account never receives an authenticated session from the newsroom endpoint.
        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            _logger.LogWarning("CMS sign-in failed: unknown account from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        var check = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            _logger.LogWarning("CMS sign-in failed for user {UserId} (lockedOut: {LockedOut}) from {RemoteIp}.", user.Id, check.IsLockedOut, HttpContext.Connection.RemoteIpAddress);
            AddLoginError(check);
            return View(model);
        }

        if (!await _userManager.IsInRoleAsync(user, Roles.Admin)
            && !await _userManager.IsInRoleAsync(user, Roles.Editor)
            && !await _userManager.IsInRoleAsync(user, Roles.Author))
        {
            _logger.LogWarning("CMS sign-in denied: user {UserId} has no newsroom role.", user.Id);
            ModelState.AddModelError(string.Empty, "Tài khoản không có quyền truy cập tòa soạn.");
            return View(model);
        }

        await _signInManager.SignInAsync(user, model.RememberMe);
        _logger.LogInformation("Newsroom user {UserId} signed in.", user.Id);
        return RedirectToStaffOrLocal(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToLocalOrHome(returnUrl);
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
