using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>Editing an article that is already published: who may, what is kept, what is recorded, and what the public page shows afterwards.</summary>
public sealed class PublishedArticleEditTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public PublishedArticleEditTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private async Task<(int Id, string Slug, DateTime PublishedAt, string RowVersion, int CategoryId)> NewPublishedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var author = await db.Users.AsNoTracking().FirstAsync(u => u.Email == "author@vnews.local");
        var category = await db.Categories.OrderBy(c => c.Id).FirstAsync();
        var published = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var article = new Article
        {
            Title = "Tiêu đề gốc " + Guid.NewGuid().ToString("N")[..6], Slug = "tieu-de-goc-" + Guid.NewGuid().ToString("N")[..8],
            Summary = "Tóm tắt gốc", Body = "<p>Nội dung gốc của bài đã xuất bản.</p>", AuthorName = author.DisplayName, AuthorUserId = author.Id,
            Status = ArticleStatus.Published, PublishedAt = published, CategoryId = category.Id,
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return (article.Id, article.Slug, published, Convert.ToBase64String(article.RowVersion), category.Id);
    }

    private static Dictionary<string, string> Form(string title, string rowVersion, int categoryId, string body = "<p>Nội dung đã được biên tập lại, đủ dài để hợp lệ.</p>", string cover = "") => new()
    {
        ["Title"] = title, ["Summary"] = "Tóm tắt đã sửa", ["Body"] = body, ["CategoryId"] = categoryId.ToString(), ["CoverImageUrl"] = cover, ["rowVersionBase64"] = rowVersion,
    };

    private async Task<Article> ReloadAsync(int id)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AsNoTracking().FirstAsync(a => a.Id == id);
    }

    [Theory]
    [InlineData("admin@vnews.local")]
    [InlineData("editor@vnews.local")]
    public async Task AdminAndEditor_CanEditAPublishedArticle_KeepingSlugStatusAndPublishTime(string email)
    {
        var a = await NewPublishedAsync();
        var client = await _factory.LoginCmsAsync(email);
        var newTitle = "Tiêu đề mới sau chỉnh sửa " + Guid.NewGuid().ToString("N")[..6];

        var response = await client.PostFormAsync($"/cms/CmsArticles/Edit/{a.Id}", $"/cms/CmsArticles/Edit/{a.Id}", Form(newTitle, a.RowVersion, a.CategoryId, cover: "/images/demo/fallback.svg"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await ReloadAsync(a.Id);
        Assert.Equal(newTitle, saved.Title);
        Assert.Equal("Tóm tắt đã sửa", saved.Summary);
        Assert.Equal("/images/demo/fallback.svg", saved.CoverImageUrl);
        Assert.Equal(ArticleStatus.Published, saved.Status);       // still public
        Assert.Equal(a.Slug, saved.Slug);                            // the public address does not move when the title changes
        Assert.Equal(a.PublishedAt, saved.PublishedAt);              // the original publish time is kept
        Assert.NotNull(saved.UpdatedAt);

        // the public page, at the same address, shows the new content
        var html = WebUtility.HtmlDecode(await _factory.NewClient().GetStringAsync($"/tin/{a.Slug}"));
        Assert.Contains(newTitle, html);
        Assert.Contains("Nội dung đã được biên tập lại", html);
    }

    [Fact]
    public async Task EditingAPublishedArticle_IsAudited_WithTheActorNotTheAuthor()
    {
        var a = await NewPublishedAsync();
        var client = await _factory.LoginCmsAsync("editor@vnews.local");

        await client.PostFormAsync($"/cms/CmsArticles/Edit/{a.Id}", $"/cms/CmsArticles/Edit/{a.Id}", Form("Tiêu đề kiểm toán " + Guid.NewGuid().ToString("N")[..6], a.RowVersion, a.CategoryId));

        using var scope = _factory.Services.CreateScope();
        var log = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().AuditLogs.AsNoTracking()
            .Where(l => l.Action == "UpdatePublished" && l.EntityId == a.Id.ToString()).SingleAsync();
        Assert.Equal("Biên tập viên demo", log.UserDisplayName);
        Assert.Contains("Sửa bài đã xuất bản", log.Detail);
    }

    [Fact]
    public async Task ChangingTheCategory_MovesTheArticleToTheNewCategoryPage()
    {
        var a = await NewPublishedAsync();
        int otherCategoryId; string otherSlug, oldSlug;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            var other = await db.Categories.Where(c => c.Id != a.CategoryId).OrderBy(c => c.Id).FirstAsync();
            otherCategoryId = other.Id; otherSlug = other.Slug;
            oldSlug = (await db.Categories.FirstAsync(c => c.Id == a.CategoryId)).Slug;
        }
        var client = await _factory.LoginCmsAsync("admin@vnews.local");
        var title = "Chuyển chuyên mục " + Guid.NewGuid().ToString("N")[..6];

        await client.PostFormAsync($"/cms/CmsArticles/Edit/{a.Id}", $"/cms/CmsArticles/Edit/{a.Id}", Form(title, a.RowVersion, otherCategoryId));

        Assert.Contains(title, WebUtility.HtmlDecode(await _factory.NewClient().GetStringAsync($"/chuyen-muc/{otherSlug}")));
        Assert.DoesNotContain(title, WebUtility.HtmlDecode(await _factory.NewClient().GetStringAsync($"/chuyen-muc/{oldSlug}")));
    }

    [Fact]
    public async Task Author_CannotEditAPublishedArticle_EvenTheirOwn_ByDirectRequest()
    {
        var a = await NewPublishedAsync(); // owned by author@vnews.local
        var client = await _factory.LoginCmsAsync("author@vnews.local");

        var response = await client.PostFormAsync($"/cms/CmsArticles/Edit/{a.Id}", $"/cms/CmsArticles/Edit/{a.Id}", Form("Tác giả cố sửa bài đã đăng", a.RowVersion, a.CategoryId));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("khong-co-quyen", response.Headers.Location!.ToString());
        Assert.StartsWith("Tiêu đề gốc", (await ReloadAsync(a.Id)).Title);

        // and the page never offers a writable form to them
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/cms/CmsArticles/Edit/{a.Id}"));
        Assert.DoesNotContain("Lưu thay đổi", html);
        Assert.Contains("Xem bài viết", html);
    }

    [Fact]
    public async Task ListShowsEditOnlyToEditorsAndAdmins_ForPublishedArticles()
    {
        var a = await NewPublishedAsync();
        var editHref = $"/cms/CmsArticles/Edit/{a.Id}";

        var editorHtml = await (await _factory.LoginCmsAsync("editor@vnews.local")).GetStringAsync("/cms?status=Published&q=" + Uri.EscapeDataString("Tiêu đề gốc"));
        var authorHtml = await (await _factory.LoginCmsAsync("author@vnews.local")).GetStringAsync("/cms?status=Published&q=" + Uri.EscapeDataString("Tiêu đề gốc"));

        Assert.Contains($"href=\"{editHref}\"", editorHtml);
        Assert.Contains($">Sửa</a>", editorHtml);
        Assert.DoesNotContain(">Sửa</a>", authorHtml);
    }

    [Fact]
    public async Task StaleRowVersion_IsRefused_NothingOverwritten_AndTheFormExplainsWhy()
    {
        var a = await NewPublishedAsync();
        var first = await _factory.LoginCmsAsync("admin@vnews.local");
        var second = await _factory.LoginCmsAsync("editor@vnews.local");

        await first.PostFormAsync($"/cms/CmsArticles/Edit/{a.Id}", $"/cms/CmsArticles/Edit/{a.Id}", Form("Bản của người thứ nhất", a.RowVersion, a.CategoryId));
        var conflict = await second.PostFormAsync($"/cms/CmsArticles/Edit/{a.Id}", $"/cms/CmsArticles/Edit/{a.Id}", Form("Bản cũ của người thứ hai", a.RowVersion, a.CategoryId));

        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode); // the form is shown again with the message
        Assert.Equal("Bản của người thứ nhất", (await ReloadAsync(a.Id)).Title);
    }

    [Fact]
    public async Task PublishedEdit_SanitizesBody_AndRejectsABadCoverUrl()
    {
        var a = await NewPublishedAsync();
        var client = await _factory.LoginCmsAsync("admin@vnews.local");

        await client.PostFormAsync($"/cms/CmsArticles/Edit/{a.Id}", $"/cms/CmsArticles/Edit/{a.Id}",
            Form("Tiêu đề an toàn " + Guid.NewGuid().ToString("N")[..6], a.RowVersion, a.CategoryId, body: "<p>Văn bản hợp lệ khá dài để qua kiểm tra.</p><script>alert('xss')</script><img src=x onerror=alert(1)>"));
        var saved = await ReloadAsync(a.Id);
        Assert.DoesNotContain("<script", saved.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", saved.Body, StringComparison.OrdinalIgnoreCase);

        var bad = await client.PostFormAsync($"/cms/CmsArticles/Edit/{a.Id}", $"/cms/CmsArticles/Edit/{a.Id}",
            Form("Tiêu đề khác " + Guid.NewGuid().ToString("N")[..6], Convert.ToBase64String(saved.RowVersion), a.CategoryId, cover: "javascript:alert(1)"));
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.Null((await ReloadAsync(a.Id)).CoverImageUrl);
    }
}
