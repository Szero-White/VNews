using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VNews.Domain.Identity;
using VNews.Web.Demo;
using VNews.Web.Security;

namespace VNews.Web.Controllers;

/// <summary>
/// One-click demo sign-in for portfolio viewers. It is a normal ASP.NET Core Identity sign-in (a real authentication
/// cookie, real roles, real authorization afterwards), but the account is chosen by the SERVER from a fixed list.
/// Nothing here can run unless <see cref="DemoAuthGate"/> allows it: Development only, DemoAuth:Enabled=true, a
/// non-production database, loopback request. No password exists in the page, the URL or any script.
/// </summary>
[Route("tai-khoan/demo")]
public sealed class DemoAuthController : Controller
{
    private readonly DemoAccountService _accounts;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DemoAuthController> _logger;

    public DemoAuthController(DemoAccountService accounts, SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager, ILogger<DemoAuthController> logger)
    {
        _accounts = accounts;
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("dang-nhap")]
    [ServiceFilter(typeof(RequireDemoAuthFilter))]
    [EnableRateLimiting("auth")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(string role)
    {
        var persona = DemoPersonas.Find(role);
        if (persona is null) return BadRequest();

        var user = await _accounts.EnsureAsync(persona);
        if (user is null) return StatusCode(StatusCodes.Status500InternalServerError);

        // Switching persona replaces the previous session instead of stacking on it.
        await _signInManager.SignOutAsync();
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, new[] { new Claim(DemoPersonas.ClaimType, "true") });

        var roles = await _userManager.GetRolesAsync(user);
        _logger.LogInformation("Demo sign-in as {Persona} ({UserId}).", persona.Key, user.Id);
        return LocalRedirect(PostLoginDestination.For(Url, roles, null));
    }
}
