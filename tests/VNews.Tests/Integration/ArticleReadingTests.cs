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

/// <summary>The article reading page: byline, reading time, previous/next links, and the single cover-upload size limit.</summary>
public sealed class ArticleReadingTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public ArticleReadingTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    // ---------- reading experience ----------

    [Fact]
    public async Task ArticlePage_ShowsBylineReadingTimeCommentCount_AndNeighbourLinks()
    {
        string slug;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            // the article in the middle of its category's timeline has both neighbours
            var ids = await db.Articles.AsNoTracking().Where(a => a.Status == ArticleStatus.Published).GroupBy(a => a.CategoryId).Where(g => g.Count() >= 3).Select(g => g.Key).FirstAsync();
            var ordered = await db.Articles.AsNoTracking().Where(a => a.Status == ArticleStatus.Published && a.CategoryId == ids).OrderBy(a => a.PublishedAt).ThenBy(a => a.Id).ToListAsync();
            slug = ordered[ordered.Count / 2].Slug;
        }
        var html = await _factory.NewClient().GetDecodedAsync($"/tin/{slug}");

        Assert.Contains("Tác giả:", html);
        Assert.Contains("phút đọc", html);
        Assert.Contains("href=\"#binh-luan\"", html);
        Assert.Contains("Bài trước", html);
        Assert.Contains("Bài sau", html);
        Assert.Contains("class=\"article-neighbors", html);
    }

    [Fact]
    public async Task Neighbours_AreInPublishOrder_NeverTheArticleItself_AndOnlyPublished()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var query = scope.ServiceProvider.GetRequiredService<IArticleQueryService>();
        var categoryId = await db.Categories.Select(c => c.Id).FirstAsync();
        var stamp = DateTime.UtcNow.AddDays(-400);
        Article Make(string name, ArticleStatus status, int hours) => new() { Title = name, Slug = $"{name}-{Guid.NewGuid():N}", Summary = "s", Body = "<p>b</p>", AuthorName = "A", Status = status, PublishedAt = stamp.AddHours(hours), CategoryId = categoryId };
        var older = Make("cu", ArticleStatus.Published, 1);
        var middle = Make("giua", ArticleStatus.Published, 2);
        var hiddenDraft = Make("nhap", ArticleStatus.Draft, 3);
        var newer = Make("moi", ArticleStatus.Published, 4);
        db.Articles.AddRange(older, middle, hiddenDraft, newer);
        await db.SaveChangesAsync();

        var around = await query.GetNeighborsAsync(middle.Id);

        Assert.Equal(older.Id, around.Previous!.Id);
        Assert.Equal(newer.Id, around.Next!.Id); // the draft between them is skipped
        Assert.NotEqual(older.Id, (await query.GetNeighborsAsync(older.Id)).Previous?.Id); // never itself
        Assert.Equal(middle.Id, (await query.GetNeighborsAsync(older.Id)).Next!.Id);
        Assert.Null((await query.GetNeighborsAsync(hiddenDraft.Id)).Previous);
    }

    // ---------- upload limit ----------

    [Fact]
    public async Task UploadLimit_IsOneNumber_SharedByTheFormAndTheServer()
    {
        var author = await _factory.LoginCmsAsync("author@vnews.local");
        var html = await author.GetDecodedAsync("/cms/CmsArticles/Create");

        Assert.Contains($"data-max-bytes=\"{ImageUploadLimits.MaxBytes}\"", html);
        Assert.Contains($"tối đa {ImageUploadLimits.Label}", html);
        Assert.DoesNotContain("5 MB", html);
        Assert.Equal(3L * 1024 * 1024, ImageUploadLimits.MaxBytes);
        Assert.True(ImageUploadLimits.RequestBytes > ImageUploadLimits.MaxBytes);
    }
}
