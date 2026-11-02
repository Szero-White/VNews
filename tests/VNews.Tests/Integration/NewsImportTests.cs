using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.News;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>The importer against an isolated database and a fake HTTP handler (no network): idempotent, mapped to categories, nothing copied beyond summary + link.</summary>
public sealed class NewsImportTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public NewsImportTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private sealed class FakeFeedHandler : HttpMessageHandler
    {
        public List<string> Requested { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!.AbsoluteUri);
            // Only the technology channel has news; every other channel answers with an empty feed.
            var items = request.RequestUri.AbsolutePath.Contains("khoa-hoc-cong-nghe")
                ? """
                  <item><title>Chip mới ra mắt</title><description><![CDATA[<a href="x"><img src="y"></a></br>Tóm tắt về chip mới.]]></description>
                  <pubDate>Sun, 01 Nov 2026 11:00:00 +0700</pubDate><link>https://vnexpress.net/chip-moi-ra-mat-5200001.html</link>
                  <enclosure type="image/jpeg" length="1" url="https://i1-vnexpress.vnecdn.net/chip.jpg"/></item>
                  <item><title>Chip mới ra mắt</title><description>Bài trùng tiêu đề, khác liên kết.</description>
                  <pubDate>Sun, 01 Nov 2026 10:00:00 +0700</pubDate><link>https://vnexpress.net/chip-moi-ra-mat-5200002.html</link></item>
                  """
                : string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"<rss><channel>{items}</channel></rss>") });
        }
    }

    private static RssNewsImportService Create(IServiceScope scope, FakeFeedHandler handler, bool thumbnails)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["NewsImport:ShowSourceThumbnails"] = thumbnails.ToString() }).Build();
        return new RssNewsImportService(new HttpClient(handler), scope.ServiceProvider.GetRequiredService<VNewsDbContext>(),
            scope.ServiceProvider.GetRequiredService<IAuditLogService>(), config, NullLogger<RssNewsImportService>.Instance);
    }

    [Fact]
    public async Task Import_AddsPublishedArticlesWithSource_TwiceIsHarmless_AndNoPictureByDefault()
    {
        var handler = new FakeFeedHandler();
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.Where(a => a.SourceUrl != null).ExecuteDeleteAsync(); // tests in this class share one database
        var service = Create(scope, handler, thumbnails: false);

        var first = await service.ImportAsync("admin-1", "Quản trị");
        Assert.Equal(2, first.Added);
        Assert.False(first.HasProblems);
        Assert.Contains(handler.Requested, u => u == "https://vnexpress.net/rss/khoa-hoc-cong-nghe.rss");

        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var imported = await db.Articles.AsNoTracking().Include(a => a.Category).Where(a => a.SourceUrl != null).ToListAsync();
        Assert.Equal(2, imported.Count);
        Assert.All(imported, a =>
        {
            Assert.Equal(ArticleStatus.Published, a.Status);
            Assert.Equal("VnExpress", a.SourceName);
            Assert.Equal("cong-nghe", a.Category.Slug);
            Assert.Null(a.CoverImageUrl);                                 // the publisher's picture is not used by default
            Assert.Contains(a.SourceUrl!, a.Body);                        // credit + link back
            Assert.DoesNotContain("<img", a.Body, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(2, imported.Select(a => a.Slug).Distinct().Count()); // same headline, different links -> different slugs
        Assert.Equal("Tóm tắt về chip mới.", imported.Single(a => a.SourceUrl!.EndsWith("5200001.html")).Summary);

        var second = await Create(scope, new FakeFeedHandler(), thumbnails: false).ImportAsync("admin-1", "Quản trị");
        Assert.Equal(0, second.Added);
        Assert.Equal(2, await db.Articles.CountAsync(a => a.SourceUrl != null));
        Assert.True(await db.AuditLogs.AnyAsync(l => l.Action == "ImportNews"));
    }

    [Fact]
    public async Task Import_UsesThePublisherThumbnailOnlyWhenTheOwnerSwitchesItOn()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        await db.Articles.Where(a => a.SourceUrl != null).ExecuteDeleteAsync(); // only this test's own rows (isolated database)

        await Create(scope, new FakeFeedHandler(), thumbnails: true).ImportAsync("admin-1", "Quản trị");
        var withPicture = await db.Articles.AsNoTracking().SingleAsync(a => a.SourceUrl!.EndsWith("5200001.html"));
        Assert.StartsWith("https://i1-vnexpress.vnecdn.net/", withPicture.CoverImageUrl);
    }

    [Fact]
    public async Task ImportPage_IsAdminOnly()
    {
        using var editor = await _factory.LoginCmsAsync("editor@vnews.local");
        var page = await editor.GetAsync("/cms/CmsArticles");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.DoesNotContain("Nạp tin từ VnExpress", System.Net.WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync()));

        using var admin = await _factory.LoginCmsAsync("admin@vnews.local");
        Assert.Contains("Nạp tin từ VnExpress", System.Net.WebUtility.HtmlDecode(await admin.GetStringAsync("/cms/CmsArticles")));
    }
}
