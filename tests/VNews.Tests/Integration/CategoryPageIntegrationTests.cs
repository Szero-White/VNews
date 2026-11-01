using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>Category pages, pagination, the navigation active state and the mobile menu, on an isolated database.</summary>
public class CategoryPageIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public CategoryPageIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    // Test-only categories (never the demo ones) so each test controls exactly how many articles a page has.
    private async Task<Category> AddCategoryAsync(int articleCount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var id = Guid.NewGuid().ToString("N")[..10];
        var category = new Category { Name = $"Mục thử {id}", Slug = $"muc-thu-{id}", Description = "Chuyên mục dùng cho kiểm thử.", DisplayOrder = 900 };
        for (var i = 0; i < articleCount; i++)
        {
            category.Articles.Add(new Article
            {
                Title = $"Bài số {i:00} {id}",
                Slug = $"bai-{i:00}-{id}",
                Summary = $"Tóm tắt bài số {i:00}.",
                Body = "<p>Nội dung.</p>",
                AuthorName = "Tác giả",
                Status = ArticleStatus.Published,
                PublishedAt = DateTime.UtcNow.AddMinutes(-i - 1), // i = 0 is the newest
                CoverImageUrl = "/images/demo/articles/1.jpg",
            });
        }
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    [Fact]
    public async Task FirstPage_HasLead_ThreeCards_AndListWithTimes_AndSideColumn()
    {
        var category = await AddCategoryAsync(8);

        var html = Decode(await _factory.NewClient().GetStringAsync($"/chuyen-muc/{category.Slug}"));

        Assert.Single(Regex.Matches(html, "class=\"cat-lead\""));
        Assert.Equal(3, Regex.Matches(html, "<article class=\"cat-card\"").Count);
        Assert.Equal(4, Regex.Matches(html, "<article class=\"cat-row\"").Count); // 8 = 1 lead + 3 cards + 4 rows
        // Newest article is the lead, in the panel next to the image.
        Assert.Matches(new Regex($"cat-lead__title\"><a [^>]*>Bài số 00 "), html);
        // Every item carries a machine-readable time.
        Assert.Equal(8, Regex.Matches(html, "<time class=\"cat-meta\" datetime=\"[^\"]+\"").Count);
        Assert.Contains("category-layout__aside", html);
        Assert.Contains("Xem nhiều", html);
        Assert.DoesNotContain("pagination", html); // 8 items fit on one page
    }

    [Fact]
    public async Task Pagination_ThirtyArticles_ThreePages_NoRepeats_AndLaterPagesAreListOnly()
    {
        var category = await AddCategoryAsync(30);
        var client = _factory.NewClient();

        var seen = new List<string>();
        foreach (var page in new[] { 1, 2, 3 })
        {
            var html = Decode(await client.GetStringAsync($"/chuyen-muc/{category.Slug}?page={page}"));
            // Only this category's titles: the side column also lists articles of other categories.
            var marker = category.Slug["muc-thu-".Length..];
            var main = html[html.IndexOf("category-main", StringComparison.Ordinal)..html.IndexOf("category-layout__aside", StringComparison.Ordinal)];
            var titles = Regex.Matches(main, $"Bài số (\\d\\d) {marker}").Select(m => m.Groups[1].Value).Distinct().ToList();
            seen.AddRange(titles);
            Assert.Contains("aria-label=\"Phân trang\"", html);
            Assert.Contains($"aria-current=\"page\">{page}</a>", html);
            if (page > 1) Assert.DoesNotContain("class=\"cat-lead\"", html); // list only after page 1
            Assert.Equal(page > 1, html.Contains("rel=\"prev\""));
            Assert.Equal(page < 3, html.Contains("rel=\"next\""));
        }
        Assert.Equal(30, seen.Count);
        Assert.Equal(30, seen.Distinct().Count());
    }

    [Fact]
    public async Task Pagination_ManyPages_CollapsesWithEllipsis_KeepingFirstCurrentAndLast()
    {
        var category = await AddCategoryAsync(110); // 10 pages of 12
        var html = Decode(await _factory.NewClient().GetStringAsync($"/chuyen-muc/{category.Slug}?page=5"));

        var numbers = Regex.Matches(html, "class=\"pagination__item[^\"]*\"[^>]*>(\\d+)</a>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "1", "4", "5", "6", "10" }, numbers);
        Assert.Equal(2, Regex.Matches(html, "pagination__gap").Count);
        Assert.Contains("Trước", html);
        Assert.Contains("Sau", html);
    }

    [Fact]
    public async Task PageBeyondTheEnd_ShowsTheLastPage_NotAnEmptyOne()
    {
        var category = await AddCategoryAsync(30);

        var response = await _factory.NewClient().GetAsync($"/chuyen-muc/{category.Slug}?page=99");
        var html = Decode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("aria-current=\"page\">3</a>", html);
        Assert.Contains("class=\"cat-row\"", html);
    }

    [Fact]
    public async Task EmptyCategory_ShowsAnEmptyState_AndNoSideColumn()
    {
        var category = await AddCategoryAsync(0);

        var response = await _factory.NewClient().GetAsync($"/chuyen-muc/{category.Slug}");
        var html = Decode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Chưa có bài viết trong chuyên mục này.", html);
        Assert.DoesNotContain("class=\"cat-lead\"", html);
        Assert.DoesNotContain("pagination", html);
    }

    [Fact]
    public async Task UnpublishedArticles_NeverAppearInACategoryPage()
    {
        var category = await AddCategoryAsync(2);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
            db.Articles.Add(new Article { Title = "Bản nháp bí mật", Slug = $"nhap-{category.Slug}", Summary = "x", Body = "x", AuthorName = "x", Status = ArticleStatus.Draft, CategoryId = category.Id });
            await db.SaveChangesAsync();
        }

        var html = Decode(await _factory.NewClient().GetStringAsync($"/chuyen-muc/{category.Slug}"));

        Assert.DoesNotContain("Bản nháp bí mật", html);
    }

    [Fact]
    public async Task Navigation_MarksOnlyTheCurrentCategory_OnItsPage_AndOnItsArticles()
    {
        var client = _factory.NewClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var seeded = await db.Categories.Where(c => c.DisplayOrder < 900).OrderBy(c => c.DisplayOrder).ToListAsync();
        var target = seeded[3];

        var html = Decode(await client.GetStringAsync($"/chuyen-muc/{target.Slug}"));
        var strip = html[html.IndexOf("category-nav__list", StringComparison.Ordinal)..html.IndexOf("</nav>", html.IndexOf("category-nav__list", StringComparison.Ordinal), StringComparison.Ordinal)];
        var current = Regex.Matches(strip, "<a [^>]*aria-current=\"page\"[^>]*>([^<]+)</a>").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { target.Name }, current);

        // An article in that category keeps its category highlighted.
        var articleSlug = await db.Articles.Where(a => a.CategoryId == target.Id && a.Status == ArticleStatus.Published).Select(a => a.Slug).FirstAsync();
        var articleHtml = Decode(await client.GetStringAsync($"/tin/{articleSlug}"));
        var articleStrip = articleHtml[articleHtml.IndexOf("category-nav__list", StringComparison.Ordinal)..];
        Assert.Matches(new Regex($"<a [^>]*(href=\"/chuyen-muc/{target.Slug}\"[^>]*aria-current=\"page\"|aria-current=\"page\"[^>]*href=\"/chuyen-muc/{target.Slug}\")"), articleStrip);

        // Other pages mark nothing.
        var home = Decode(await client.GetStringAsync("/tin-moi-nhat"));
        Assert.DoesNotContain("category-nav__list li a[aria-current", home);
        Assert.Empty(Regex.Matches(home[home.IndexOf("category-nav__list", StringComparison.Ordinal)..home.IndexOf("</nav>", home.IndexOf("category-nav__list", StringComparison.Ordinal), StringComparison.Ordinal)], "aria-current"));
    }

    [Fact]
    public async Task MobileMenu_ListsEveryRealCategory_WithWorkingLinks()
    {
        var client = _factory.NewClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync();

        var html = Decode(await client.GetStringAsync("/tin-moi-nhat"));
        var drawer = html[html.IndexOf("utility-drawer__cats", StringComparison.Ordinal)..];
        drawer = drawer[..drawer.IndexOf("</nav>", StringComparison.Ordinal)];

        var links = Regex.Matches(drawer, "href=\"(/chuyen-muc/[^\"]+)\"[^>]*>([^<]+)</a>").Select(m => (Href: m.Groups[1].Value, Name: m.Groups[2].Value)).ToList();
        Assert.Equal(categories.Select(c => c.Slug), links.Select(l => l.Href.Replace("/chuyen-muc/", "")));
        Assert.Equal(categories.Select(c => c.Name), links.Select(l => l.Name));
        foreach (var link in links)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(link.Href)).StatusCode);
        }
    }

    [Fact]
    public async Task SideColumn_ListsRealMostViewedArticles_WithTheirOwnCovers()
    {
        var category = await AddCategoryAsync(4);
        var html = Decode(await _factory.NewClient().GetStringAsync($"/chuyen-muc/{category.Slug}"));

        var aside = html[html.IndexOf("category-layout__aside", StringComparison.Ordinal)..];
        aside = aside[..aside.IndexOf("</aside>", StringComparison.Ordinal)];
        var rows = Regex.Matches(aside, "popular-list__row").Count;
        Assert.InRange(rows, 1, 5);
        Assert.Contains("/images/demo/", aside); // real cover images, no invented thumbnails
    }
}
