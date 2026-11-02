using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;
using VNews.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace VNews.Tests.Integration;

/// <summary>Unused cover images: what counts as unused, what is never touched, and that the page is administrators only.</summary>
public sealed class UploadCleanupTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public UploadCleanupTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    // ---------- unused upload cleanup ----------

    private sealed class TempEnvironment : IWebHostEnvironment, IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "vnews-upload-test-" + Guid.NewGuid().ToString("N"));
        public TempEnvironment() { Directory.CreateDirectory(Path.Combine(Root, "wwwroot", "uploads", "articles")); }
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get => Path.Combine(Root, "wwwroot"); set { } }
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get => Root; set { } }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
        public string Uploads => Path.Combine(Root, "wwwroot", "uploads", "articles");
        public string Quarantine => Path.Combine(Root, "App_Data", "unused-uploads");
    }

    private static string Name(string seed) => seed.PadRight(32, '0')[..32] + ".png";

    private static void Touch(string path, DateTime utc, int length = 10)
    {
        File.WriteAllBytes(path, new byte[length]);
        File.SetLastWriteTimeUtc(path, utc);
    }

    [Fact]
    public async Task Cleanup_MovesOnlyOldUnreferencedGeneratedFiles_AndNeverTouchesAnythingElse()
    {
        using var env = new TempEnvironment();
        var old = DateTime.UtcNow.AddDays(-3);
        var usedByCover = Name("aa1"); var usedByDraft = Name("aa2"); var usedInBody = Name("aa3");
        var unusedOld = Name("bb1"); var unusedNew = Name("bb2"); var foreign = "logo-cua-toi.png"; var wrongShape = "ABC.png";
        foreach (var f in new[] { usedByCover, usedByDraft, usedInBody, unusedOld, foreign, wrongShape }) Touch(Path.Combine(env.Uploads, f), old);
        Touch(Path.Combine(env.Uploads, unusedNew), DateTime.UtcNow.AddHours(-1)); // inside the 24 h grace period

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var categoryId = await db.Categories.Select(c => c.Id).FirstAsync();
        Article Make(string slug, ArticleStatus status, string? cover, string body) => new() { Title = slug, Slug = slug + Guid.NewGuid().ToString("N")[..6], Summary = "s", Body = body, CoverImageUrl = cover, AuthorName = "A", Status = status, CategoryId = categoryId };
        db.Articles.AddRange(
            Make("a-cover", ArticleStatus.Published, "/uploads/articles/" + usedByCover, "<p>b</p>"),
            Make("a-draft", ArticleStatus.Draft, "/uploads/articles/" + usedByDraft, "<p>b</p>"),   // a DRAFT still counts as using it
            Make("a-body", ArticleStatus.Published, null, $"<p><img src=\"/uploads/articles/{usedInBody}\" /></p>"));
        await db.SaveChangesAsync();

        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var service = new UploadCleanupService(env, new UploadStorage(env, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()), db, audit, NullLogger<UploadCleanupService>.Instance);

        var scan = await service.ScanAsync();
        Assert.Equal(new[] { unusedOld }, scan.Unused.Select(u => u.FileName).ToArray());
        Assert.Equal(3, scan.ReferencedFiles);
        Assert.Equal(1, scan.TooNewFiles);

        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("admin@vnews.local"))!;
        var result = await service.QuarantineUnusedAsync(user.Id, user.DisplayName);

        Assert.Equal(1, result.Moved);
        Assert.False(File.Exists(Path.Combine(env.Uploads, unusedOld)));
        Assert.True(File.Exists(Path.Combine(env.Quarantine, unusedOld)));   // moved, recoverable, not deleted
        foreach (var kept in new[] { usedByCover, usedByDraft, usedInBody, unusedNew, foreign, wrongShape })
            Assert.True(File.Exists(Path.Combine(env.Uploads, kept)), kept);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "QuarantineUploads"));

        Assert.Equal(0, (await service.QuarantineUnusedAsync(user.Id, user.DisplayName)).Moved); // idempotent
    }

    [Fact]
    public async Task Cleanup_Page_IsAdminOnly_AndForgedPostsAreRefused()
    {
        foreach (var email in new[] { "author@vnews.local", "editor@vnews.local" })
        {
            var client = await _factory.LoginCmsAsync(email);
            var page = await client.GetAsync("/cms/CmsMedia");
            Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
            Assert.Contains("khong-co-quyen", page.Headers.Location!.ToString());
            var post = await client.PostFormAsync("/cms/CmsMedia/Quarantine", "/cms", new());
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
            Assert.Contains("khong-co-quyen", post.Headers.Location!.ToString());
        }
        var admin = await _factory.LoginCmsAsync("admin@vnews.local");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/cms/CmsMedia")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/cms/CmsMedia/Quarantine", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode); // no token
    }
}
