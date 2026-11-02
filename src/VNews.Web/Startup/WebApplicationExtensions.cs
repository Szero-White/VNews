using VNews.Web.Middleware;
using VNews.Web.Services;

namespace VNews.Web.Startup;

public static class WebApplicationExtensions
{
    /// <summary>The request pipeline, in order: security headers first (error responses carry them too), error handling, routing, rate limiting, auth.</summary>
    public static WebApplication UseVNewsPipeline(this WebApplication app)
    {
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
            // The default HSTS value is 30 days; see https://aka.ms/aspnetcore-hsts for production scenarios.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/Home/PageNotFound", "?statusCode={0}");

        // Pictures uploaded while the site runs are read from disk on each request; MapStaticAssets only serves what existed at build time,
        // so without this a freshly uploaded cover would be a broken image until the next publish (and then be lost).
        var storage = app.Services.GetRequiredService<UploadStorage>();
        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = UploadStorage.RequestPath,
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(storage.Root),
            ServeUnknownFileTypes = false,
        });
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>
    /// Vietnamese public addresses first, then the CMS area, then the conventional default. Specific routes must stay above "cms" and
    /// "default": a catch-all registered earlier would change how every other URL is generated.
    /// </summary>
    public static WebApplication MapVNewsRoutes(this WebApplication app)
    {
        app.MapStaticAssets();

        Route(app, "article", "tin/{slug}", "Article", "Detail");
        Route(app, "category", "chuyen-muc/{slug}", "Category", "Index");
        Route(app, "tag", "chu-de/{slug}", "Tag", "Index");
        Route(app, "search", "tim-kiem", "Search", "Index");
        Route(app, "account-login", "tai-khoan/dang-nhap", "Account", "Login");
        Route(app, "account-register", "tai-khoan/dang-ky", "Account", "Register");
        Route(app, "account-saved", "tai-khoan/bai-da-luu", "ReaderAccount", "SavedArticles");
        Route(app, "account-profile", "tai-khoan/ho-so", "ReaderAccount", "Profile");
        Route(app, "cms-login", "cms/dang-nhap", "Account", "CmsLogin");
        Route(app, "account-logout", "tai-khoan/dang-xuat", "Account", "Logout");
        Route(app, "account-denied", "tai-khoan/khong-co-quyen", "Account", "AccessDenied");

        app.MapControllerRoute(
            name: "cms",
            pattern: "cms/{controller=CmsArticles}/{action=Index}/{id?}",
            constraints: new { controller = "CmsArticles|CmsCategories|CmsUsers|CmsOverview|CmsAudit|CmsMedia|CmsComments|CmsTrash|CmsImport|CmsAnalytics|CmsImages" })
            .WithStaticAssets();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

        return app;
    }

    private static void Route(WebApplication app, string name, string pattern, string controller, string action) =>
        app.MapControllerRoute(name, pattern, defaults: new { controller, action }).WithStaticAssets();
}
