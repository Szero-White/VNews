using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using VNews.Application.Common;
using VNews.Application.DTOs;

namespace VNews.Tests.Integration;

/// <summary>
/// Hits the real VNews.Web app over HTTP against the real SQL Server LocalDB database
/// configured in appsettings.json. Requires SQL Server LocalDB to be available.
/// </summary>
public class ArticlesApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ArticlesApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCategories_ReturnsAtLeastEightSeededCategories()
    {
        var response = await _client.GetAsync("/api/v1/categories");
        response.EnsureSuccessStatusCode();

        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();

        Assert.NotNull(categories);
        Assert.True(categories!.Count >= 8);
        Assert.Contains(categories, c => c.Slug == "cong-nghe");
    }

    [Fact]
    public async Task GetArticleBySlug_UnknownSlug_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/articles/slug-khong-ton-tai-abc-xyz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetArticles_InvalidPageSize_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/articles?pageSize=999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_WithoutQuery_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetArticlesByCategory_ViaStoredProcedure_ReturnsOnlyThatCategory()
    {
        var response = await _client.GetAsync("/api/v1/categories/cong-nghe/articles?page=1&pageSize=5");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ArticleSummaryDto>>();

        Assert.NotNull(result);
        Assert.True(result!.Items.Count <= 5);
        Assert.All(result.Items, item => Assert.Equal("cong-nghe", item.CategorySlug));
    }

    [Fact]
    public async Task GetDailyPublishStats_ViaStoredProcedure_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/stats/daily-publish");
        response.EnsureSuccessStatusCode();

        var stats = await response.Content.ReadFromJsonAsync<List<DailyPublishStatDto>>();

        Assert.NotNull(stats);
    }
}
