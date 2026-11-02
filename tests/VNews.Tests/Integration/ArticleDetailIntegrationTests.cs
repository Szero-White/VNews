using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>The article page shows dateline, sign-off, tags, related stories and "Xem nhiều" from real data.</summary>
public class ArticleDetailIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public ArticleDetailIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private async Task<Article> AddPublishedAsync(string label, int viewCount, DateTime publishedUtc, params Tag[] tags)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var id = Guid.NewGuid().ToString("N");
        var article = new Article
        {
            Title = $"{label} {id}",
            Slug = $"{label.ToLowerInvariant()}-{id}",
            Summary = "Tóm tắt bài kiểm thử trang chi tiết.",
            Body = "<p>Nội dung kiểm thử.</p>",
            AuthorName = $"Tác giả {label}",
            Status = ArticleStatus.Published,
            PublishedAt = publishedUtc,
            ViewCount = viewCount,
            CategoryId = await db.Categories.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync()
        };
        foreach (var tag in tags)
        {
            var existing = await db.Tags.FirstOrDefaultAsync(t => t.Slug == tag.Slug) ?? tag;
            article.ArticleTags.Add(new ArticleTag { Tag = existing });
        }
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article;
    }

    [Fact]
    public async Task Detail_ShowsGmtPlus7Dateline_SignOff_BackLink_AndRealTags()
    {
        var tagSlug = $"tag-{Guid.NewGuid():N}";
        var article = await AddPublishedAsync("Chitiet", 10, new DateTime(2026, 10, 31, 18, 5, 0, DateTimeKind.Utc),
            new Tag { Name = "Chuyển đổi số", Slug = tagSlug });
        var client = _factory.NewClient();

        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync($"/tin/{article.Slug}"));

        Assert.Contains("Chủ nhật, 1/11/2026, 01:05 (GMT+7)", html); // 18:05 UTC Saturday = 01:05 Sunday in Vietnam
        Assert.Contains("datetime=\"2026-11-01T01:05:00+07:00\"", html);
        Assert.Contains("<span class=\"article-signoff__name\">Tác giả Chitiet</span>", html);
        Assert.Contains("article-back-link", html);
        Assert.Contains("Trở lại ", html); // "Trở lại {category}" next to the "Lưu" button
        Assert.Contains($"href=\"/chu-de/{tagSlug}\"", html);
        Assert.Contains("Chuyển đổi số", html);

        // The tag link opens a working page that lists this article.
        var tagPage = await client.GetAsync($"/chu-de/{tagSlug}");
        Assert.Equal(HttpStatusCode.OK, tagPage.StatusCode);
        Assert.Contains(article.Slug, await tagPage.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Detail_WithoutTags_RendersNoTagBlock()
    {
        var article = await AddPublishedAsync("Khongtag", 10, DateTime.UtcNow.AddHours(-1));

        var html = await _factory.NewClient().GetStringAsync($"/tin/{article.Slug}");

        Assert.DoesNotContain("article-tags", html);
    }

    [Fact]
    public async Task Detail_MostViewedSidebar_UsesStoredViewCounts_AndExcludesTheCurrentArticle()
    {
        var top = await AddPublishedAsync("Topview", 900_000, DateTime.UtcNow.AddHours(-3));
        var current = await AddPublishedAsync("Hientai", 950_000, DateTime.UtcNow.AddHours(-2));
        var client = _factory.NewClient();

        var html = await client.GetStringAsync($"/tin/{current.Slug}");
        var asideStart = html.IndexOf("article-layout__aside", StringComparison.Ordinal);
        var aside = html[asideStart..html.IndexOf("</aside>", asideStart, StringComparison.Ordinal)];

        Assert.Contains("Xem nhiều", aside);
        Assert.Contains($"/tin/{top.Slug}", aside);
        Assert.DoesNotContain($"/tin/{current.Slug}", aside);
        Assert.DoesNotContain("lượt xem", html); // no per-article view figures are displayed
    }

    [Fact]
    public async Task Detail_ReadingAnArticle_IncrementsItsStoredViewCount()
    {
        var article = await AddPublishedAsync("Dem", 5, DateTime.UtcNow.AddHours(-1));

        await _factory.NewClient().GetAsync($"/tin/{article.Slug}");

        using var scope = _factory.Services.CreateScope();
        var views = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles
            .AsNoTracking().Where(a => a.Id == article.Id).Select(a => a.ViewCount).SingleAsync();
        Assert.Equal(6, views);
    }
}
