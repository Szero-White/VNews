using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using VNews.Web.Middleware;
using Microsoft.AspNetCore.Identity;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Infrastructure;
using VNews.Infrastructure.Persistence;
using VNews.Infrastructure.Seed;
using VNews.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// One-click demo sign-in: OFF unless DemoAuth:Enabled=true, and even then Development + a demo/test database + loopback only.
builder.Services.Configure<VNews.Web.Demo.DemoAuthOptions>(builder.Configuration.GetSection(VNews.Web.Demo.DemoAuthOptions.SectionName));
builder.Services.AddSingleton<VNews.Web.Demo.DemoAuthGate>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<VNews.Web.Demo.DemoAccountService>();
builder.Services.AddScoped<VNews.Web.Demo.RequireDemoAuthFilter>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<VNews.Domain.Identity.ApplicationUser>, VNews.Web.Security.DisplayNameClaimsFactory>();

// Auth/antiforgery cookies: never readable by script, never sent cross-site on POST. Secure is
// forced outside Development; Development (plain http://localhost) keeps working.
var cookieSecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});
// A role change must apply on the user next request, not when the cookie expires: re-validate the security stamp every request.
builder.Services.Configure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

// Brute-force protection for sign-in / registration POSTs, per client IP.
// (Behind a reverse proxy, enable ForwardedHeaders so RemoteIpAddress is the real client.)
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
var authWindowSeconds = builder.Configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromSeconds(authWindowSeconds),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("VNews.RateLimiting");
        logger.LogWarning("Rate limit hit on {Path} from {RemoteIp}.", context.HttpContext.Request.Path.Value, context.HttpContext.Connection.RemoteIpAddress);
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync("Bạn thao tác quá nhanh. Vui lòng đợi một lúc rồi thử lại.", cancellationToken);
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
    await DbInitializer.SeedAsync(context);

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await IdentitySeeder.SeedAsync(roleManager, userManager, seedDemoAccounts: app.Environment.IsDevelopment());

    // First administrator for a real deployment: only from secret configuration (Bootstrap:AdminEmail / Bootstrap:AdminPassword,
    // e.g. environment variables or user-secrets), only when no Admin exists, never with the public demo password.
    var bootstrap = await IdentitySeeder.EnsureBootstrapAdminAsync(userManager, app.Configuration["Bootstrap:AdminEmail"], app.Configuration["Bootstrap:AdminPassword"]);
    var startupLog = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("VNews.Bootstrap");
    if (bootstrap == IdentitySeeder.BootstrapAdminResult.Created) startupLog.LogWarning("Bootstrap administrator created from configuration. Remove Bootstrap:AdminPassword from configuration now.");
    else if (bootstrap == IdentitySeeder.BootstrapAdminResult.RejectedPublicPassword) startupLog.LogError("Bootstrap administrator refused: the configured password is the public demo password.");
    else if (bootstrap == IdentitySeeder.BootstrapAdminResult.Failed) startupLog.LogError("Bootstrap administrator could not be created (check the e-mail and the password policy).");
}

// Configure the HTTP request pipeline.
// Security headers first, so error responses carry them too.
app.UseMiddleware<SecurityHeadersMiddleware>();

// /api: unhandled exceptions become ProblemDetails (all environments, never a stack trace).
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseExceptionHandler(errorApp => errorApp.Run(ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return Task.CompletedTask;
    })));

if (!app.Environment.IsDevelopment())
{
    // MVC: friendly Vietnamese error page with a trace id (no stack trace).
    app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"),
        branch => branch.UseExceptionHandler("/Home/Error"));
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/PageNotFound", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "article",
    pattern: "tin/{slug}",
    defaults: new { controller = "Article", action = "Detail" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "category",
    pattern: "chuyen-muc/{slug}",
    defaults: new { controller = "Category", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "tag",
    pattern: "chu-de/{slug}",
    defaults: new { controller = "Tag", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "search",
    pattern: "tim-kiem",
    defaults: new { controller = "Search", action = "Index" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-login",
    pattern: "tai-khoan/dang-nhap",
    defaults: new { controller = "Account", action = "Login" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-register",
    pattern: "tai-khoan/dang-ky",
    defaults: new { controller = "Account", action = "Register" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-saved",
    pattern: "tai-khoan/bai-da-luu",
    defaults: new { controller = "Account", action = "SavedArticles" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-profile",
    pattern: "tai-khoan/ho-so",
    defaults: new { controller = "Account", action = "Profile" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "cms-login",
    pattern: "cms/dang-nhap",
    defaults: new { controller = "Account", action = "CmsLogin" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-logout",
    pattern: "tai-khoan/dang-xuat",
    defaults: new { controller = "Account", action = "Logout" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "account-denied",
    pattern: "tai-khoan/khong-co-quyen",
    defaults: new { controller = "Account", action = "AccessDenied" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "cms",
    pattern: "cms/{controller=CmsArticles}/{action=Index}/{id?}",
    constraints: new { controller = "CmsArticles|CmsCategories|CmsUsers|CmsOverview|CmsAudit" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
