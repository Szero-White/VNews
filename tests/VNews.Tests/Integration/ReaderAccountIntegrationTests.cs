using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

public sealed class ReaderAccountIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public ReaderAccountIntegrationTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Reader_CanRegisterBookmarkAndCannotEnterCms()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var registerPage = await client.GetAsync("/tai-khoan/dang-ky");
        Assert.Equal(HttpStatusCode.OK, registerPage.StatusCode);
        var formToken = ExtractToken(await registerPage.Content.ReadAsStringAsync(), "__RequestVerificationToken");
        var email = $"reader-{Guid.NewGuid():N}@example.test";
        using var registerResponse = await client.PostAsync("/tai-khoan/dang-ky", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["DisplayName"] = "Người đọc thử nghiệm",
            ["Email"] = email,
            ["Password"] = "TestPassword123!",
            ["ConfirmPassword"] = "TestPassword123!",
            ["__RequestVerificationToken"] = formToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

        var profile = await client.GetAsync("/tai-khoan/ho-so");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains(email, await profile.Content.ReadAsStringAsync());

        var cmsResponse = await client.GetAsync("/cms");
        Assert.Equal(HttpStatusCode.Redirect, cmsResponse.StatusCode);
        Assert.Contains("khong-co-quyen", cmsResponse.Headers.Location!.ToString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var articleId = await db.Articles.AsNoTracking().Where(a => a.Status == ArticleStatus.Published)
            .Select(a => a.Id).FirstAsync();

        var home = await client.GetAsync("/");
        var antiforgeryToken = ExtractToken(await home.Content.ReadAsStringAsync(), "vnews-csrf-token");
        using var post = new HttpRequestMessage(HttpMethod.Post, $"/api/me/bookmarks/{articleId}");
        post.Headers.Add("RequestVerificationToken", antiforgeryToken);
        var save = await client.SendAsync(post);
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

        var saved = await client.GetAsync("/api/me/bookmarks");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains($"\"id\":{articleId}", await saved.Content.ReadAsStringAsync());

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/me/bookmarks/{articleId}");
        delete.Headers.Add("RequestVerificationToken", antiforgeryToken);
        var remove = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotAccessAccountBookmarks()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/me/bookmarks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ExtractToken(string html, string name)
    {
        var pattern = name == "__RequestVerificationToken"
            ? "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\""
            : "name=\\\"vnews-csrf-token\\\"[^>]*content=\\\"([^\\\"]+)\\\"";
        var match = Regex.Match(html, pattern);
        Assert.True(match.Success, $"Missing antiforgery token {name}");
        return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
