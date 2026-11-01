using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VNews.Domain.Identity;
using VNews.Web.Demo;

namespace VNews.Web.Security;

/// <summary>
/// Builds the cookie principal for every sign-in AND every security-stamp re-validation. It adds the display name (so pages
/// can greet the user without a database call) and the demo marker for the four server-owned demo accounts. The marker must
/// come from here: the stamp validator rebuilds the principal from this factory on each request and would otherwise drop any
/// claim that was only attached at sign-in. It is only issued while the demo gate is open (Development, demo DB, loopback).
/// </summary>
public sealed class DisplayNameClaimsFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string ClaimType = "DisplayName";
    private readonly IHttpContextAccessor _http;
    private readonly DemoAuthGate _gate;

    public DisplayNameClaimsFactory(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IOptions<IdentityOptions> options,
        IHttpContextAccessor http, DemoAuthGate gate)
        : base(userManager, roleManager, options)
    {
        _http = http;
        _gate = gate;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.DisplayName)) identity.AddClaim(new Claim(ClaimType, user.DisplayName));

        var context = _http.HttpContext;
        if (context is not null && DemoPersonas.All.Any(p => string.Equals(p.Email, user.Email, StringComparison.OrdinalIgnoreCase)) && _gate.IsAvailable(context))
        {
            identity.AddClaim(new Claim(DemoPersonas.ClaimType, "true"));
        }
        return identity;
    }
}
