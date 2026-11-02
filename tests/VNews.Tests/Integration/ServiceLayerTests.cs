using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Common;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>
/// The application services on their own (no HTTP): the rules that used to live in controllers and must hold for every caller.
/// Isolated database per fixture.
/// </summary>
public sealed class ServiceLayerTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public ServiceLayerTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private T Service<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    // ---------- user administration ----------

    [Fact]
    public async Task SetRole_RefusesOwnRole_UnknownRole_AndUnknownUser()
    {
        var admin = await _factory.CreateUserAsync(Roles.Admin);
        var other = await _factory.CreateUserAsync(Roles.Reader);
        using var scope = _factory.Services.CreateScope();
        var users = Service<IUserAdministrationService>(scope);

        Assert.Equal(SetRoleOutcome.CannotChangeOwnRole, (await users.SetRoleAsync(admin.Id, admin.Id, Roles.Reader)).Outcome);
        Assert.Equal(SetRoleOutcome.InvalidRole, (await users.SetRoleAsync(admin.Id, other.Id, "SuperUser")).Outcome);
        Assert.Equal(SetRoleOutcome.UserNotFound, (await users.SetRoleAsync(admin.Id, "no-such-user", Roles.Author)).Outcome);
        Assert.Equal(SetRoleOutcome.Changed, (await users.SetRoleAsync(admin.Id, other.Id, Roles.Author)).Outcome);
    }

    [Fact]
    public async Task SetRole_NeverDemotesTheLastAdministrator()
    {
        // Make exactly ONE administrator exist by demoting every other one through the service, acting as the survivor.
        var survivor = await _factory.CreateUserAsync(Roles.Admin);
        using var scope = _factory.Services.CreateScope();
        var users = Service<IUserAdministrationService>(scope);
        var db = Service<VNewsDbContext>(scope);
        var adminRoleId = await db.Roles.Where(r => r.Name == Roles.Admin).Select(r => r.Id).SingleAsync();
        var otherAdmins = await db.UserRoles.Where(ur => ur.RoleId == adminRoleId && ur.UserId != survivor.Id).Select(ur => ur.UserId).ToListAsync();
        foreach (var id in otherAdmins) Assert.Equal(SetRoleOutcome.Changed, (await users.SetRoleAsync(survivor.Id, id, Roles.Editor)).Outcome);

        var target = await _factory.CreateUserAsync(Roles.Reader);
        // A different actor (not the survivor) tries to demote the only administrator: refused by the rule itself.
        Assert.Equal(SetRoleOutcome.LastAdministrator, (await users.SetRoleAsync(target.Id, survivor.Id, Roles.Reader)).Outcome);
        Assert.Equal(1, (await users.CountByRoleAsync())[Roles.Admin]);
    }

    [Fact]
    public async Task ListAsync_FiltersByRoleAndSearch_PagesOnTheServer_AndCountsEveryRole()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await _factory.CreateUserAsync(Roles.Editor, $"Biên tập {marker}");
        await _factory.CreateUserAsync(Roles.Author, $"Tác giả {marker}");
        using var scope = _factory.Services.CreateScope();
        var users = Service<IUserAdministrationService>(scope);

        var editors = await users.ListAsync(marker, Roles.Editor, 1, 10);
        Assert.Single(editors.Items);
        Assert.Equal(new[] { Roles.Editor }, editors.Items[0].Roles);

        var both = await users.ListAsync(marker, null, 1, 1);
        Assert.Equal(2, both.TotalCount);
        Assert.Single(both.Items);                                 // paged
        Assert.Equal(2, (await users.ListAsync(marker, null, 99, 1)).Page); // a page past the end is clamped to the last

        var counts = await users.CountByRoleAsync();
        Assert.All(Roles.All, role => Assert.True(counts.ContainsKey(role)));
    }

    // ---------- saved articles ----------

    [Fact]
    public async Task SavedArticles_AreScopedToTheUser_SaveTwiceIsHarmless_AndUnpublishedCannotBeSaved()
    {
        var alice = await _factory.CreateUserAsync(Roles.Reader);
        var bob = await _factory.CreateUserAsync(Roles.Reader);
        using var scope = _factory.Services.CreateScope();
        var saved = Service<ISavedArticleService>(scope);
        var db = Service<VNewsDbContext>(scope);
        var published = await db.Articles.AsNoTracking().Where(a => a.Status == ArticleStatus.Published).Select(a => a.Id).FirstAsync();
        var draft = new Article { Title = "Nháp riêng", Slug = "nhap-rieng-" + Guid.NewGuid().ToString("N")[..8], Summary = "s", Body = "<p>b</p>", AuthorName = "A", Status = ArticleStatus.Draft, CategoryId = await db.Categories.Select(c => c.Id).FirstAsync() };
        db.Articles.Add(draft);
        await db.SaveChangesAsync();

        Assert.True(await saved.SaveAsync(alice.Id, published));
        Assert.True(await saved.SaveAsync(alice.Id, published));            // second tab / double click
        Assert.False(await saved.SaveAsync(alice.Id, draft.Id));            // not published
        Assert.False(await saved.SaveAsync(alice.Id, 987_654));             // not there

        Assert.Equal(1, await saved.CountAsync(alice.Id));
        Assert.Equal(0, await saved.CountAsync(bob.Id));                    // nobody else's list
        Assert.False(await saved.RemoveAsync(bob.Id, published));           // cannot remove someone else's
        Assert.True(await saved.RemoveAsync(alice.Id, published));
        Assert.Equal(0, await saved.CountAsync(alice.Id));
    }

    // ---------- audit log ----------

    [Fact]
    public async Task AuditLog_PageBeyondTheEnd_ReturnsTheLastPage_NewestFirst()
    {
        using var scope = _factory.Services.CreateScope();
        var audit = Service<IAuditLogService>(scope);
        for (var i = 0; i < 3; i++) await audit.LogAsync("u", "Người thử", "Create", "Article", i.ToString(), $"mục {i}");
        var reader = Service<IAuditLogQueryService>(scope);

        var last = await reader.GetPageAsync(10_000, 2);
        Assert.Equal(last.TotalPages, last.Page);
        var first = await reader.GetPageAsync(1, 2);
        Assert.True(first.Items[0].CreatedAt >= first.Items[1].CreatedAt);
        Assert.Equal(2, (await reader.GetRecentAsync(2)).Count);
    }

    // ---------- home page ----------

    [Fact]
    public async Task HomePage_PanelsComeFromOneQuery_AreNewestFirst_AndSkipEmptyCategories()
    {
        using var scope = _factory.Services.CreateScope();
        var data = await Service<IHomePageQueryService>(scope).GetHomePageDataAsync();
        var db = Service<VNewsDbContext>(scope);

        Assert.NotEmpty(data.CategoryPanels);
        Assert.All(data.CategoryPanels, panel =>
        {
            Assert.InRange(panel.Articles.Count, 1, 5);
            Assert.All(panel.Articles, a => Assert.Equal(panel.Category.Slug, a.CategorySlug));
            Assert.Equal(panel.Articles.OrderByDescending(a => a.PublishedAt).Select(a => a.Id), panel.Articles.Select(a => a.Id));
        });
        var withStories = await db.Articles.Where(a => a.Status == ArticleStatus.Published).Select(a => a.CategoryId).Distinct().CountAsync();
        Assert.Equal(withStories, data.CategoryPanels.Count);
        Assert.NotNull(data.Hero);
        Assert.DoesNotContain(data.SideFeatures, a => a.Id == data.Hero!.Id);
    }

    // ---------- sanitizer ----------

    [Theory]
    [InlineData("<p>ok</p><script>alert(1)</script>", "<script")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript:")]
    [InlineData("<p style=\"position:fixed\">x</p>", "style=")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>", "<iframe")]
    public void ArticleHtml_StripsEverythingOutsideTheAllowList(string input, string forbidden) =>
        Assert.DoesNotContain(forbidden, ArticleHtmlSanitizer.Sanitize(input), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void ArticleHtml_KeepsOnlyTheEditorsFontClasses_OnSpans()
    {
        var clean = ArticleHtmlSanitizer.Sanitize("<p><span class=\"fs-24 ff-georgia\">to</span> <span class=\"evil fs-99\" style=\"position:fixed\" onclick=\"x()\">xấu</span></p>");
        Assert.Contains("fs-24", clean);
        Assert.Contains("ff-georgia", clean);
        Assert.DoesNotContain("evil", clean);
        Assert.DoesNotContain("fs-99", clean);
        Assert.DoesNotContain("style=", clean);
        Assert.DoesNotContain("onclick", clean);
        Assert.DoesNotContain("class=\"x\"", ArticleHtmlSanitizer.Sanitize("<p class=\"x\">a</p>")); // unknown classes are dropped from every tag
    }

    [Fact]
    public void ArticleHtml_KeepsAPictureWithItsCaption_ButNotScriptsInIt()
    {
        var clean = ArticleHtmlSanitizer.Sanitize("<figure><img src=\"/uploads/articles/" + new string('a', 32) + ".png\" alt=\"\" onerror=\"x()\"><figcaption>Đi bộ buổi sáng. Ảnh: <em>Ngọc Thành</em><script>alert(1)</script></figcaption></figure>");
        Assert.Contains("<figure>", clean);
        Assert.Contains("<figcaption>", clean);
        Assert.Contains("<em>Ngọc Thành</em>", clean);
        Assert.Contains("/uploads/articles/", clean);
        Assert.DoesNotContain("onerror", clean);
        Assert.DoesNotContain("<script", clean);
    }

    [Fact]
    public void ArticleHtml_KeepsTheEditorialTags()
    {
        var clean = ArticleHtmlSanitizer.Sanitize("<h2>Tiêu đề</h2><p><strong>đậm</strong> <em>nghiêng</em> <a href=\"https://example.com\">liên kết</a></p><ul><li>một</li></ul><blockquote>trích</blockquote>");
        foreach (var tag in new[] { "<h2>", "<strong>", "<em>", "<a href=\"https://example.com\">", "<li>", "<blockquote>" }) Assert.Contains(tag, clean);
    }
}
