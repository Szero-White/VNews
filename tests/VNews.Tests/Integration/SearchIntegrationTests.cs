using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>Vietnamese search against the real SQL Server (isolated test database).</summary>
public class SearchIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public SearchIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    // A marker made of hex digits keeps each test's rows apart from the seeded demo articles and from other tests.
    private static string NewMarker() => "mk" + Guid.NewGuid().ToString("N");

    private async Task<Article> AddAsync(string title, string summary, ArticleStatus status = ArticleStatus.Published, DateTime? publishedUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var article = new Article
        {
            Title = title,
            Slug = "tim-kiem-" + Guid.NewGuid().ToString("N"),
            Summary = summary,
            Body = "<p>Nội dung kiểm thử tìm kiếm.</p>",
            AuthorName = "Tác giả",
            Status = status,
            PublishedAt = status == ArticleStatus.Published ? publishedUtc ?? DateTime.UtcNow.AddMinutes(-5) : null,
            CategoryId = await db.Categories.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync()
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article;
    }

    private async Task<(int Total, List<int> Ids)> SearchApiAsync(string query, int page = 1, int pageSize = 50)
    {
        var response = await _factory.NewClient().GetAsync($"/api/v1/search?q={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = json.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
        return (json.RootElement.GetProperty("totalCount").GetInt32(), ids);
    }

    [Theory]
    [InlineData("giá vàng")]
    [InlineData("gia vang")]
    [InlineData("GIÁ VÀNG")]
    [InlineData("  Gia   Vàng  ")]
    [InlineData("giá, vàng!")]
    public async Task AccentedUnaccentedUpperCaseAndExtraSpaces_MatchTheSameArticles(string typed)
    {
        var marker = NewMarker();
        var inTitle = await AddAsync($"Giá vàng trong nước tăng mạnh {marker}", "Tóm tắt thị trường.");
        var inSummary = await AddAsync($"Thị trường hôm nay {marker}", "Giá vàng đi ngang trong phiên sáng.");
        await AddAsync($"Giá vàng nháp {marker}", "Bản nháp.", ArticleStatus.Draft);
        await AddAsync($"Giá vàng chờ duyệt {marker}", "Chờ duyệt.", ArticleStatus.PendingReview);
        await AddAsync($"Giá vàng bị từ chối {marker}", "Từ chối.", ArticleStatus.Rejected);
        await AddAsync($"Giá dầu giảm {marker}", "Không liên quan tới kim loại.");

        var (total, ids) = await SearchApiAsync($"{typed} {marker}");

        Assert.Equal(2, total); // published only; the article without both words is excluded
        Assert.Equal(new[] { inTitle.Id, inSummary.Id }.Order(), ids.Order());
    }

    [Fact]
    public async Task LetterD_MatchesWithAndWithoutStroke()
    {
        var marker = NewMarker();
        var article = await AddAsync($"Đường cao tốc mới thông xe {marker}", "Đoạn đường dài 50 km.");

        foreach (var typed in new[] { "đường cao tốc", "duong cao toc", "ĐƯỜNG", "Duong" })
        {
            var (_, ids) = await SearchApiAsync($"{typed} {marker}");
            Assert.Contains(article.Id, ids);
        }
    }

    [Fact]
    public async Task AllKeywordsMustMatch_AndPhraseInTitleRanksBeforeSummaryOnlyMatch()
    {
        var marker = NewMarker();
        var summaryOnlyNewer = await AddAsync($"Bản tin thị trường {marker}", "Vàng tăng, giá cũng tăng.", publishedUtc: DateTime.UtcNow.AddMinutes(-1));
        var titleOlder = await AddAsync($"{marker} giá vàng lập đỉnh mới", "Tóm tắt.", publishedUtc: DateTime.UtcNow.AddDays(-2));
        await AddAsync($"Chỉ có vàng {marker}", "Không có từ còn lại.");

        var (total, ids) = await SearchApiAsync($"{marker} giá vàng");

        Assert.Equal(2, total);
        Assert.Equal(new[] { titleOlder.Id, summaryOnlyNewer.Id }, ids);
    }

    [Fact]
    public async Task Pagination_IsExact_WithoutDuplicates_AndPastTheEndIsEmpty()
    {
        var marker = NewMarker();
        var created = new List<int>();
        for (var i = 0; i < 25; i++)
        {
            // Same publish time for several rows: the order must still be total (no repeats across pages).
            created.Add((await AddAsync($"Bản tin số {i} {marker}", "Tóm tắt phân trang.", publishedUtc: DateTime.UtcNow.AddHours(-(i / 5)))).Id);
        }

        var seen = new List<int>();
        foreach (var page in new[] { 1, 2, 3 })
        {
            var (total, ids) = await SearchApiAsync($"ban tin so {marker}", page, 10);
            Assert.Equal(25, total);
            Assert.Equal(page < 3 ? 10 : 5, ids.Count);
            seen.AddRange(ids);
        }
        Assert.Equal(25, seen.Distinct().Count());
        Assert.Equal(created.Order(), seen.Order());

        var (_, pastEnd) = await SearchApiAsync($"ban tin so {marker}", 4, 10);
        Assert.Empty(pastEnd);
    }

    [Fact]
    public async Task ResultsPage_PastTheLastPage_ShowsTheLastPage_NotNoResults()
    {
        var marker = NewMarker();
        for (var i = 0; i < 14; i++) await AddAsync($"Tin hiển thị {i} {marker}", "Tóm tắt.");

        var response = await _factory.NewClient().GetAsync($"/tim-kiem?q={marker}&page=99");

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Tìm thấy <strong>14</strong> kết quả", html);
        Assert.DoesNotContain("Không tìm thấy bài viết", html);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("[a-z]")]
    [InlineData("' OR 1=1 --")]
    [InlineData("'; DROP TABLE Articles; --")]
    [InlineData("!!!")]
    public async Task SpecialCharacters_NeverMatchEverything_NeverBreak_AndDoNotTouchData(string typed)
    {
        var marker = NewMarker();
        await AddAsync($"Bài thường {marker}", "Tóm tắt.");

        var response = await _factory.NewClient().GetAsync($"/tim-kiem?q={Uri.EscapeDataString(typed)}");
        var (_, apiIds) = await SearchApiAsync(typed + " " + marker + "zzz"); // keyword that matches nothing

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(apiIds);
        using var scope = _factory.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AnyAsync(a => a.Title.EndsWith(marker)));
    }

    [Fact]
    public async Task VeryLongQuery_IsHandled()
    {
        var response = await _factory.NewClient().GetAsync($"/tim-kiem?q={new string('a', 5000)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var api = await _factory.NewClient().GetAsync($"/api/v1/search?q={string.Join("%20", Enumerable.Repeat("gia", 400))}");
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);
    }

    [Fact]
    public async Task ResultsPage_EncodesTheQuery_AndShowsAMessageWhenEmpty()
    {
        var response = await _factory.NewClient().GetAsync("/tim-kiem?q=" + Uri.EscapeDataString("<script>alert(1)</script>"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("Không tìm thấy bài viết", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task ResultsPage_ListsMatchingPublishedArticle_ForUnaccentedQuery()
    {
        var marker = NewMarker();
        var article = await AddAsync($"Thể thao cuối tuần {marker}", "Tóm tắt.");

        var html = WebUtility.HtmlDecode(await _factory.NewClient().GetStringAsync($"/tim-kiem?q=the+thao+{marker}"));

        Assert.Contains($"/tin/{article.Slug}", html);
        Assert.Contains("Tìm thấy <strong>1</strong> kết quả", html);
    }
}
