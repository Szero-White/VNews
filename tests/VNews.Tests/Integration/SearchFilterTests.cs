using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.Common;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>The search page's narrowing: category (with sub-categories), period, original vs imported, and the two sort orders.</summary>
public sealed class SearchFilterTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public SearchFilterTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Filters_NarrowByCategoryKindAndPeriod_AndSortsLatestOrRelevance()
    {
        var marker = "lc" + Guid.NewGuid().ToString("N")[..10];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var categories = await db.Categories.OrderBy(c => c.Id).Take(2).ToListAsync();
        var now = DateTime.UtcNow;

        Article Make(string title, string summary, Category category, DateTime at, string? sourceUrl = null) => new()
        {
            Title = title, Slug = "loc-" + Guid.NewGuid().ToString("N"), Summary = summary, Body = "<p>x</p>", AuthorName = "A",
            Status = ArticleStatus.Published, PublishedAt = at, CategoryId = category.Id, SourceUrl = sourceUrl, SourceName = sourceUrl is null ? null : "Nguồn",
        };

        var titleHit = Make($"Tiêu đề {marker}", "tóm tắt", categories[0], now.AddDays(-20));                       // phrase in the title, oldest
        var summaryHit = Make("Tin khác", $"có {marker} trong tóm tắt", categories[1], now.AddHours(-2));          // phrase only in the summary, newest
        var imported = Make("Tin ngoài", $"nguồn {marker}", categories[0], now.AddDays(-3), "https://example.test/a-1"); // imported from a feed
        db.Articles.AddRange(titleHit, summaryHit, imported);
        await db.SaveChangesAsync();
        var search = scope.ServiceProvider.GetRequiredService<IArticleQueryService>();

        async Task<List<int>> Ids(SearchFilter filter) => (await search.SearchAsync(marker, 1, 50, default, filter)).Items.Select(i => i.Id).ToList();

        Assert.Equal(3, (await Ids(new SearchFilter())).Count);
        Assert.Equal(new[] { summaryHit.Id, imported.Id, titleHit.Id }, await Ids(new SearchFilter(Sort: SearchSort.Latest)));
        Assert.Equal(titleHit.Id, (await Ids(new SearchFilter(Sort: SearchSort.Relevance)))[0]); // the title hit outranks newer summary hits

        Assert.Equal(new[] { summaryHit.Id }, await Ids(new SearchFilter(CategorySlug: categories[1].Slug)));
        Assert.Equal(new[] { imported.Id }, await Ids(new SearchFilter(Kind: SearchKind.External)));
        Assert.DoesNotContain(imported.Id, await Ids(new SearchFilter(Kind: SearchKind.Original)));
        Assert.Equal(new[] { summaryHit.Id }, await Ids(new SearchFilter(Period: SearchPeriod.Day)));
        Assert.DoesNotContain(titleHit.Id, await Ids(new SearchFilter(Period: SearchPeriod.Week))); // 20 days old
        Assert.Contains(titleHit.Id, await Ids(new SearchFilter(Period: SearchPeriod.Month)));
    }

    [Fact]
    public async Task ResultsPage_KeepsTheChosenFilters_InTheSortTabsAndIgnoresAnUnknownCategory()
    {
        var client = _factory.NewClient();
        var html = await client.GetStringAsync("/tim-kiem?q=gia&cat=thoi-su&time=week&kind=external");

        Assert.Contains("name=\"time\"", html);
        Assert.Contains("search-tabs__tab", html);
        Assert.Contains("cat=thoi-su", html);
        Assert.Contains("time=week", html);
        Assert.Contains("kind=external", html);

        var unknown = await client.GetAsync("/tim-kiem?q=gia&cat=khong-co-chuyen-muc-nay&time=bad&kind=bad&sort=bad");
        Assert.Equal(System.Net.HttpStatusCode.OK, unknown.StatusCode);
    }
}
