using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>The footer is built from real data and every link it shows leads to a working page.</summary>
public class FooterAndPagesIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public FooterAndPagesIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private static string FooterHtml(string page)
    {
        var start = page.IndexOf("<footer class=\"site-footer\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "site footer not found");
        return page[start..(page.IndexOf("</footer>", start, StringComparison.Ordinal) + 9)];
    }

    [Fact]
    public async Task Footer_ListsEveryCategoryFromTheDatabase_WithWorkingLinks()
    {
        var client = _factory.NewClient();
        using var scope = _factory.Services.CreateScope();
        var categories = await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Categories.AsNoTracking().ToListAsync();
        Assert.True(categories.Count >= 10, "seed data should provide 10 categories");

        var footer = FooterHtml(await (await client.GetAsync("/")).Content.ReadAsStringAsync());

        foreach (var category in categories)
        {
            Assert.Contains($"href=\"/chuyen-muc/{category.Slug}\"", footer);
            Assert.Contains(category.Name, WebUtility.HtmlDecode(footer));
        }
    }

    [Fact]
    public async Task EveryFooterLink_ResolvesToAWorkingPage()
    {
        var client = _factory.NewClient();
        var footer = FooterHtml(await (await client.GetAsync("/")).Content.ReadAsStringAsync());

        var hrefs = Regex.Matches(footer, "href=\"([^\"]+)\"").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Distinct().ToList();
        Assert.True(hrefs.Count >= 16, $"expected a full footer, found {hrefs.Count} links");

        foreach (var href in hrefs)
        {
            Assert.StartsWith("/", href); // internal routes only: no placeholder "#" or external/fake targets
            Assert.DoesNotContain("#", href);
            var response = await client.GetAsync(href);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{href} returned {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task Footer_DoesNotInventLicencesContactDetailsAppStoresOrRss()
    {
        var client = _factory.NewClient();
        var text = WebUtility.HtmlDecode(FooterHtml(await (await client.GetAsync("/")).Content.ReadAsStringAsync()));

        foreach (var forbidden in new[] { "Số giấy phép", "Giấy phép hoạt động", "Giấy phép xuất bản", "Tổng biên tập", "App Store", "Google Play", "Tải ứng dụng", "RSS", "Hotline", "@" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotMatch(@"\b0\d{2,3}[ .]?\d{3}[ .]?\d{3,4}\b", text); // no phone numbers
    }

    [Theory]
    [InlineData("/gioi-thieu", "Giới thiệu VNews")]
    [InlineData("/dieu-khoan-su-dung", "Điều khoản sử dụng")]
    [InlineData("/chinh-sach-bao-mat", "Chính sách bảo mật")]
    public async Task InformationPages_Render_WithTheirOwnHeading(string path, string heading)
    {
        var response = await _factory.NewClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"<h1 class=\"static-page__title\">{heading}</h1>", html);
        Assert.Contains("static-pages.css", html);
    }
}
