using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.Common;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>Parent / sub-category: the rules, the CMS, the navigation and the category page. Isolated database (migrations applied there only).</summary>
public sealed class CategoryHierarchyTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public CategoryHierarchyTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private static string Unique(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..8];

    private async Task<int> CreateAsync(string name, string slug, int? parentId = null, int order = 50)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICategoryCommandService>().CreateAsync(name, slug, null, order, parentId);
    }

    private async Task AddArticlesAsync(int categoryId, string titlePrefix, int count, int firstHourOffset = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var baseTime = DateTime.UtcNow.AddDays(-30);
        for (var i = 0; i < count; i++)
        {
            db.Articles.Add(new Article
            {
                Title = $"{titlePrefix} bài {i}", Slug = $"{titlePrefix}-{i}-{Guid.NewGuid():N}"[..Math.Min(80, $"{titlePrefix}-{i}-{Guid.NewGuid():N}".Length)],
                Summary = "Tóm tắt thử", Body = "<p>Nội dung thử nghiệm.</p>", AuthorName = "Tác giả", Status = ArticleStatus.Published,
                PublishedAt = baseTime.AddHours(firstHourOffset + i), CategoryId = categoryId,
            });
        }
        await db.SaveChangesAsync();
    }


    // ---------- rules ----------

    [Fact]
    public async Task Rules_OneLevelOnly_NoSelfParent_NoUnknownParent_NoParentWithChildrenBecomingAChild()
    {
        var top = await CreateAsync("Cha", Unique("cha"));
        var child = await CreateAsync("Con", Unique("con"), top);
        var other = await CreateAsync("Khác", Unique("khac"));
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICategoryCommandService>();

        await Assert.ThrowsAsync<ArticleWorkflowException>(() => svc.CreateAsync("Cháu", Unique("chau"), null, 1, child));          // a child cannot be a parent
        await Assert.ThrowsAsync<ArticleWorkflowException>(() => svc.UpdateAsync(top, "Cha", "cha-" + top, null, 1, top));          // not its own parent
        await Assert.ThrowsAsync<ArticleWorkflowException>(() => svc.CreateAsync("Mồ côi", Unique("moi"), null, 1, 999_999));        // parent must exist
        await Assert.ThrowsAsync<ArticleWorkflowException>(() => svc.UpdateAsync(top, "Cha", "cha-" + top, null, 1, other));        // has children: cannot become a child
        await svc.UpdateAsync(child, "Con", "con-" + child, null, 1, other);                                                       // moving a child between parents is fine
        using var check = _factory.Services.CreateScope();
        Assert.Equal(other, (await check.ServiceProvider.GetRequiredService<VNewsDbContext>().Categories.AsNoTracking().FirstAsync(c => c.Id == child)).ParentId);
    }

    [Fact]
    public async Task Tree_ListsTopLevelWithChildren_AndFlatListHasEachChildAfterItsParent()
    {
        var top = await CreateAsync("Khoa học", Unique("kh"), order: 900);
        var c1 = await CreateAsync("Vũ trụ", Unique("vt"), top, 1);
        var c2 = await CreateAsync("Thiết bị", Unique("tb"), top, 2);

        using var scope = _factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<ICategoryQueryService>();
        var tree = await query.GetTreeAsync();
        Assert.DoesNotContain(tree, t => t.Id == c1 || t.Id == c2);                      // children are not top-level
        Assert.Equal(new[] { c1, c2 }, tree.Single(t => t.Id == top).Children.Select(c => c.Id).ToArray());

        var flat = (await query.GetAllAsync()).Select(c => c.Id).ToList();
        Assert.Equal(flat.IndexOf(top) + 1, flat.IndexOf(c1));
        Assert.Equal(flat.IndexOf(top) + 2, flat.IndexOf(c2));
        Assert.Equal("Khoa học › Vũ trụ", (await query.GetAllAsync()).Single(c => c.Id == c1).FullName);
    }

    // ---------- CMS ----------

    [Fact]
    public async Task AdminCreatesASubCategory_ItAppearsInTheNavigation_AndEditorOrAuthorCannot()
    {
        var parentSlug = Unique("tech");
        await CreateAsync("Công nghệ thử", parentSlug);
        int parentId;
        using (var scope = _factory.Services.CreateScope())
            parentId = (await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Categories.FirstAsync(c => c.Slug == parentSlug)).Id;

        foreach (var email in new[] { "editor@vnews.local", "author@vnews.local" })
        {
            var denied = await (await _factory.LoginCmsAsync(email)).PostFormAsync("/cms/CmsCategories/Create", "/cms",
                new() { ["Name"] = "Lén", ["Slug"] = Unique("len"), ["DisplayOrder"] = "1", ["ParentId"] = parentId.ToString() });
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("khong-co-quyen", denied.Headers.Location!.ToString());
        }

        var childSlug = Unique("ai");
        var admin = await _factory.LoginCmsAsync("admin@vnews.local");
        var created = await admin.PostFormAsync("/cms/CmsCategories/Create", "/cms/CmsCategories/Create",
            new() { ["Name"] = "Trí tuệ nhân tạo", ["Slug"] = childSlug, ["DisplayOrder"] = "1", ["ParentId"] = parentId.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        var home = await _factory.NewClient().GetDecodedAsync("/");
        Assert.Contains($"aria-controls=\"subnav-{parentSlug}\"", home);                         // the parent now has a toggle and a list
        Assert.Matches(new Regex($"<ul id=\"subnav-{Regex.Escape(parentSlug)}\" class=\"category-subnav\" hidden>.*?/chuyen-muc/{Regex.Escape(childSlug)}", RegexOptions.Singleline), home);
        Assert.Contains($"drawer-sub-{parentSlug}", home);                                       // and the mobile drawer has its own list
        Assert.Contains("Chuyên mục con của Công nghệ thử", home);
    }

    [Fact]
    public async Task CategoryWithoutChildren_GetsNoSubmenuAtAll()
    {
        var slug = Unique("don");
        await CreateAsync("Đơn lẻ", slug);

        var home = await _factory.NewClient().GetDecodedAsync("/");

        Assert.Contains($"/chuyen-muc/{slug}", home);
        Assert.DoesNotContain($"subnav-{slug}", home);                                           // no invented list, no empty toggle
        Assert.DoesNotContain($"drawer-sub-{slug}", home);
    }

    [Fact]
    public async Task CmsCategoryForm_RejectsInvalidParents_AndEditShowsTheStoredOrderAndParent()
    {
        var top = await CreateAsync("Cha form", Unique("chaf"));
        var child = await CreateAsync("Con form", Unique("conf"), top, 7);
        var admin = await _factory.LoginCmsAsync("admin@vnews.local");

        var editHtml = await admin.GetDecodedAsync($"/cms/CmsCategories/Edit/{child}");
        Assert.Contains("value=\"7\"", editHtml);                                                 // the stored display order is shown (it used to reset to 0)
        Assert.Matches(new Regex($"<option value=\"{top}\" selected"), editHtml);

        var response = await admin.PostFormAsync($"/cms/CmsCategories/Edit/{top}", $"/cms/CmsCategories/Edit/{top}",
            new() { ["Name"] = "Cha form", ["Slug"] = "cha-form-" + top, ["DisplayOrder"] = "1", ["ParentId"] = child.ToString() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);                                     // form shown again with the message
        Assert.Contains("chuyên mục cha", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
    }

    // ---------- category page ----------

    [Fact]
    public async Task ParentPage_ShowsTheSubCategoryBar_ListsChildrenArticles_AndChildPageOnlyItsOwn()
    {
        var parentSlug = Unique("kh");
        var parent = await CreateAsync("Khoa học thử", parentSlug);
        var childSlug = Unique("vt");
        var child = await CreateAsync("Vũ trụ thử", childSlug, parent);
        await AddArticlesAsync(parent, "Bài của cha", 2);
        await AddArticlesAsync(child, "Bài của con", 2, 10);
        var client = _factory.NewClient();

        var parentHtml = await client.GetDecodedAsync($"/chuyen-muc/{parentSlug}");
        Assert.Contains("class=\"cat-subnav\"", parentHtml);
        Assert.Contains($"/chuyen-muc/{childSlug}", parentHtml);
        Assert.Contains("Bài của cha bài 0", parentHtml);
        Assert.Contains("Bài của con bài 1", parentHtml);                                        // a parent lists its children's stories too

        var childHtml = await client.GetDecodedAsync($"/chuyen-muc/{childSlug}");
        Assert.Contains("Bài của con bài 0", childHtml);
        Assert.DoesNotContain("Bài của cha bài", childHtml);
        Assert.Contains($"aria-current=\"page\" href=\"/chuyen-muc/{childSlug}\">Vũ trụ thử</a>", childHtml);                       // active state in the bar
        Assert.Contains("Tất cả Khoa học thử", childHtml);                                       // way back to the parent
        Assert.Matches(new Regex($"/chuyen-muc/{parentSlug}\">Khoa học thử</a>\\s*<span aria-hidden=\"true\">/</span>\\s*<span aria-current=\"page\">Vũ trụ thử"), childHtml); // breadcrumb
    }

    [Fact]
    public async Task ParentPage_Groups_OmitEmptySubCategories_AndNeverRepeatAStory()
    {
        var parentSlug = Unique("gr");
        var parent = await CreateAsync("Nhóm cha", parentSlug);
        var busySlug = Unique("dong"); var quietSlug = Unique("trong");
        var busy = await CreateAsync("Nhóm đông", busySlug, parent, 1);
        await CreateAsync("Nhóm trống", quietSlug, parent, 2); // a sub-category with no articles
        await AddArticlesAsync(busy, "Tin đông", 40);                                            // lead + cards (4) + group (4) + a feed of 32 = three pages
        var client = _factory.NewClient();

        var html = await client.GetDecodedAsync($"/chuyen-muc/{parentSlug}");

        Assert.Contains("class=\"cat-group\"", html);
        Assert.Contains($"id=\"group-{busySlug}\"", html);
        Assert.DoesNotContain($"id=\"group-{quietSlug}\"", html);                              // no empty block for a sub-category without stories
        Assert.Contains($"/chuyen-muc/{quietSlug}", html);                                         // ...but it is still in the link bar
        var titles = Regex.Matches(html, @"href=""/tin/[^""]+""[^>]*>(Tin đông bài \d+)</a>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(titles.Count, titles.Distinct().Count());                                   // nothing is shown twice on the page

        // across pages: every article appears exactly once
        var seen = new List<string>();
        for (var p = 1; p <= 3; p++)
        {
            var pageHtml = await client.GetDecodedAsync($"/chuyen-muc/{parentSlug}?page={p}");
            seen.AddRange(Regex.Matches(pageHtml, @"href=""/tin/[^""]+""[^>]*>(Tin đông bài \d+)</a>").Select(m => m.Groups[1].Value).Distinct());
        }
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(40, seen.Distinct().Count());                                                // every article exactly once across the three pages
        Assert.Contains("?page=3", await client.GetDecodedAsync($"/chuyen-muc/{parentSlug}"));      // and the pager really offers them
    }

    [Fact]
    public async Task CategoryWithoutChildren_KeepsTheCurrentLayout_NoBarNoGroups()
    {
        var slug = Unique("phang");
        var id = await CreateAsync("Phẳng", slug);
        await AddArticlesAsync(id, "Tin phẳng", 5);

        var html = await _factory.NewClient().GetDecodedAsync($"/chuyen-muc/{slug}");

        Assert.DoesNotContain("class=\"cat-subnav\"", html);
        Assert.DoesNotContain("class=\"cat-group\"", html);
        Assert.Contains("Tin phẳng bài 4", html);
    }

    [Fact]
    public async Task MovingAnArticleToASubCategory_ShowsItOnTheChildAndTheParentPage()
    {
        var parentSlug = Unique("mp"); var childSlug = Unique("mc");
        var parent = await CreateAsync("Cha chuyển", parentSlug);
        var child = await CreateAsync("Con chuyển", childSlug, parent);
        await AddArticlesAsync(parent, "Sẽ chuyển", 1);
        int articleId; string rowVersion;
        using (var scope = _factory.Services.CreateScope())
        {
            var a = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AsNoTracking().FirstAsync(x => x.Title == "Sẽ chuyển bài 0");
            articleId = a.Id; rowVersion = Convert.ToBase64String(a.RowVersion);
        }

        var editor = await _factory.LoginCmsAsync("editor@vnews.local");
        await editor.PostFormAsync($"/cms/CmsArticles/Edit/{articleId}", $"/cms/CmsArticles/Edit/{articleId}",
            new() { ["Title"] = "Sẽ chuyển bài 0", ["Summary"] = "Tóm tắt đủ dài để hợp lệ", ["Body"] = "<p>Nội dung đủ dài để hợp lệ khi lưu bài.</p>", ["CategoryId"] = child.ToString(), ["CoverImageUrl"] = "", ["rowVersionBase64"] = rowVersion });

        var client = _factory.NewClient();
        Assert.Contains("Sẽ chuyển bài 0", await client.GetDecodedAsync($"/chuyen-muc/{childSlug}"));
        Assert.Contains("Sẽ chuyển bài 0", await client.GetDecodedAsync($"/chuyen-muc/{parentSlug}"));
    }
}
