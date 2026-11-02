using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;
using VNews.Web.Services;

namespace VNews.Tests.Integration;

/// <summary>The CMS article list filters: search reaches the category name, a category picks its sub-categories too, the date range, and who may filter by author.</summary>
public sealed class ArticleListFilterTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public ArticleListFilterTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private async Task<(int Parent, int Child, string Marker)> SeedAsync()
    {
        var marker = "loc" + Guid.NewGuid().ToString("N")[..8];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var parent = new Category { Name = "Cha " + marker, Slug = "cha-" + marker, DisplayOrder = 80 };
        db.Categories.Add(parent);
        await db.SaveChangesAsync();
        var child = new Category { Name = "Con " + marker, Slug = "con-" + marker, DisplayOrder = 81, ParentId = parent.Id };
        var other = new Category { Name = "Khác " + marker, Slug = "khac-" + marker, DisplayOrder = 82 };
        db.Categories.AddRange(child, other);
        await db.SaveChangesAsync();

        Article Make(string title, int categoryId, string authorId, DateTime updated) => new()
        {
            Title = title + " " + marker, Slug = Guid.NewGuid().ToString("N"), Summary = "Tóm tắt " + title, Body = "<p>b</p>", AuthorName = "Tác giả " + authorId, AuthorUserId = authorId,
            Status = ArticleStatus.Published, PublishedAt = updated, CreatedAt = updated, UpdatedAt = updated, CategoryId = categoryId,
        };
        db.Articles.AddRange(
            Make("Bài cha", parent.Id, "u1", new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc)),
            Make("Bài con", child.Id, "u2", new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc)),
            Make("Bài khác", other.Id, "u1", new DateTime(2026, 5, 30, 23, 30, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();
        return (parent.Id, child.Id, marker);
    }

    [Fact]
    public async Task Search_FindsArticlesByTheirCategoryNameAndSummary()
    {
        var (_, _, marker) = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IArticleCmsQueryService>();

        var byCategoryName = await svc.ListForCmsAsync(null, null, 1, 50, search: "Khác " + marker);
        Assert.Single(byCategoryName.Items);
        Assert.StartsWith("Bài khác", byCategoryName.Items[0].Title);

        var bySummary = await svc.ListForCmsAsync(null, null, 1, 50, search: "tóm tắt bài con " + marker);
        Assert.Contains(bySummary.Items, i => i.Title.StartsWith("Bài con"));
    }

    [Fact]
    public async Task CategoryFilter_ParentCoversSubCategories_ChildOnlyItself()
    {
        var (parent, child, marker) = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IArticleCmsQueryService>();

        var underParent = await svc.ListForCmsAsync(null, null, 1, 50, search: marker, categoryId: parent);
        Assert.Equal(new[] { "Bài cha", "Bài con" }, underParent.Items.Select(i => i.Title.Replace(" " + marker, "")).OrderBy(t => t).ToArray());

        var underChild = await svc.ListForCmsAsync(null, null, 1, 50, search: marker, categoryId: child);
        Assert.Single(underChild.Items);
    }

    [Fact]
    public async Task DateRange_IncludesTheWholeLastDay_AndAuthorFilterNarrowsTheList()
    {
        var (_, _, marker) = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IArticleCmsQueryService>();

        // the 30 May article was changed at 23:30: a range ending on 30 May must still contain it
        var may = await svc.ListForCmsAsync(null, null, 1, 50, search: marker, updatedFrom: new DateTime(2026, 5, 1), updatedTo: new DateTime(2026, 5, 30));
        Assert.Single(may.Items);
        var spring = await svc.ListForCmsAsync(null, null, 1, 50, search: marker, updatedFrom: new DateTime(2026, 4, 1), updatedTo: new DateTime(2026, 4, 30));
        Assert.StartsWith("Bài con", Assert.Single(spring.Items).Title);

        var byName = await svc.ListForCmsAsync(null, null, 1, 50, search: marker, authorName: "Tác giả u1");
        Assert.Equal(2, byName.Items.Count);
        Assert.Contains("Tác giả u1", await svc.GetAuthorNamesAsync());
        var mine = await svc.ListForCmsAsync(null, "u1", 1, 50, search: marker); // the account restriction used for authors
        Assert.Equal(2, mine.Items.Count);
    }

    [Fact]
    public async Task Workspace_AnAuthorCannotWidenTheListToAnotherAuthor_ButAnEditorCanNarrowIt()
    {
        var (_, _, marker) = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var workspace = scope.ServiceProvider.GetRequiredService<CmsWorkspaceBuilder>();

        var author = new CmsWorkspaceBuilder.Viewer("u1", "Tác giả u1", IsPrivileged: false, "Author");
        var asAuthor = await workspace.BuildAsync(author, new CmsWorkspaceBuilder.Filters(null, marker, null, "Tác giả u2", null, null), 1, CancellationToken.None);
        Assert.All(asAuthor.List.Items, i => Assert.Equal("u1", i.AuthorUserId)); // asking for another author changes nothing: still only their own
        Assert.Empty(asAuthor.Authors);

        var editor = new CmsWorkspaceBuilder.Viewer("ed", "Biên tập", IsPrivileged: true, "Editor");
        var asEditor = await workspace.BuildAsync(editor, new CmsWorkspaceBuilder.Filters(null, marker, null, "Tác giả u2", null, null), 1, CancellationToken.None);
        Assert.Single(asEditor.List.Items);
        Assert.NotEmpty(asEditor.Authors);
    }
}
