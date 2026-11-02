using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Infrastructure;
using VNews.Web.Demo;
using VNews.Web.Middleware;
using VNews.Web.Security;
using VNews.Web.Services;

namespace VNews.Web.Startup;

/// <summary>Dependency-injection wiring of the web layer, grouped by concern so Program.cs reads as the application's table of contents.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVNewsWeb(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddControllersWithViews();
        services.AddVNewsDataProtection();
        services.AddInfrastructure(configuration);
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddNewsroomServices();
        services.AddDemoAuth(configuration);
        services.AddCookieAndStampSecurity(environment);
        services.AddAuthRateLimiting(configuration);
        return services;
    }

    /// <summary>
    /// Sign-in cookies and antiforgery tokens are encrypted with data-protection keys. By default the "application name" that scopes those
    /// keys is the folder the app runs from, so publishing or moving the app to another folder silently invalidates every open session and
    /// every form that is being edited (the next Save is bounced to the sign-in page). A fixed name keeps them valid across restarts and
    /// deployments on the same machine.
    /// </summary>
    private static IServiceCollection AddVNewsDataProtection(this IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName("VNews");
        return services;
    }

    /// <summary>Web-side services of the newsroom: uploads and their cleanup, the workspace arrangement.</summary>
    private static IServiceCollection AddNewsroomServices(this IServiceCollection services)
    {
        services.AddSingleton<UploadStorage>();
        services.AddScoped<IImageUploadService, ImageUploadService>();
        services.AddHttpClient<IRemoteImageFetcher, RemoteImageFetcher>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; VNewsEditor/1.0)");
            })
            .ConfigurePrimaryHttpMessageHandler(RemoteAddressPolicy.CreateHandler);
        services.AddScoped<UploadCleanupService>();
        services.AddScoped<CmsWorkspaceBuilder>();
        return services;
    }

    /// <summary>One-click demo sign-in: OFF unless DemoAuth:Enabled=true, and even then Development + a demo/test database + loopback only.</summary>
    private static IServiceCollection AddDemoAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DemoAuthOptions>(configuration.GetSection(DemoAuthOptions.SectionName));
        services.AddSingleton<DemoAuthGate>();
        services.AddHttpContextAccessor();
        services.AddScoped<DemoAccountService>();
        services.AddScoped<RequireDemoAuthFilter>();
        // The claims factory also issues the demo marker for the four demo accounts (while the gate is open), so it survives stamp re-validation.
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, DisplayNameClaimsFactory>();
        return services;
    }

    /// <summary>
    /// Auth/antiforgery cookies: never readable by script, never sent cross-site on POST; Secure is forced outside Development (plain
    /// http://localhost keeps working in Development). A role change applies on the next request of the affected user, not when the cookie
    /// expires: the security stamp is re-validated on every request.
    /// </summary>
    private static IServiceCollection AddCookieAndStampSecurity(this IServiceCollection services, IHostEnvironment environment)
    {
        var securePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = securePolicy;
        });
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        services.AddAntiforgery(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = securePolicy;
        });
        return services;
    }
}
