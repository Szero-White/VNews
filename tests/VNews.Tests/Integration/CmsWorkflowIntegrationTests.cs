using VNews.Tests.Support;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Tests.Integration;

/// <summary>
/// Exercises the real CMS login + draft workflow over HTTP against a unique, isolated LocalDB test database. No mocking of auth or antiforgery.
/// </summary>
public class CmsWorkflowIntegrationTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public CmsWorkflowIntegrationTests(IsolatedDbWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly Regex TokenRegex = new(
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var match = TokenRegex.Match(html);
        Assert.True(match.Success, "Antiforgery token not found in response HTML.");
        return match.Groups[1].Value;
    }

    private async Task<HttpClient> LoginAsAsync(string email, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var loginPage = await client.GetAsync("/tai-khoan/dang-nhap");
        var token = await ExtractAntiforgeryTokenAsync(loginPage);

        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };

        var response = await client.PostAsync("/tai-khoan/dang-nhap", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return client;
    }

    [Fact]
    public async Task AnonymousUser_AccessingCms_IsRedirectedToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/cms");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // One shared sign-in page, carrying a local return URL back to the newsroom.
        Assert.Contains("/tai-khoan/dang-nhap", response.Headers.Location!.ToString());
        Assert.Contains("returnurl=%2fcms", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithWrongPassword_DoesNotAuthenticate()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var loginPage = await client.GetAsync("/tai-khoan/dang-nhap");
        var token = await ExtractAntiforgeryTokenAsync(loginPage);

        var form = new Dictionary<string, string>
        {
            ["Email"] = "author@vnews.local",
            ["Password"] = "wrong-password",
            ["__RequestVerificationToken"] = token
        };

        var response = await client.PostAsync("/tai-khoan/dang-nhap", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        // The exact Vietnamese message differs between a failed password and a locked-out account.
        // Require a visible server-side validation error instead of matching one text literal.
        Assert.Contains("validation-summary-errors", html);

        // Most importantly, the failed attempt must not create an authenticated CMS session.
        var cmsResponse = await client.GetAsync("/cms");
        Assert.Equal(HttpStatusCode.Redirect, cmsResponse.StatusCode);
        Assert.Contains("/tai-khoan/dang-nhap", cmsResponse.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Author_CanLoginAndCreateDraft_OwnedByThemselves()
    {
        var client = await LoginAsAsync("author@vnews.local", "Demo@12345");

        var createPage = await client.GetAsync("/cms/CmsArticles/Create");
        Assert.Equal(HttpStatusCode.OK, createPage.StatusCode);
        var token = await ExtractAntiforgeryTokenAsync(createPage);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var categoryId = await context.Categories.Select(c => c.Id).FirstAsync();

        var title = $"Bai test tu dong {Guid.NewGuid():N}";
        var form = new Dictionary<string, string>
        {
            ["Title"] = title,
            ["Summary"] = "Tom tat bai viet kiem thu tu dong hoa cho CMS workflow.",
            ["Body"] = "<p>Noi dung kiem thu tu dong hoa cho quy trinh CMS.</p>",
            ["CategoryId"] = categoryId.ToString(),
            ["__RequestVerificationToken"] = token
        };

        var createResponse = await client.PostAsync("/cms/CmsArticles/Create", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Contains("/cms/CmsArticles/Edit/", createResponse.Headers.Location!.ToString());

        var created = await context.Articles.FirstOrDefaultAsync(a => a.Title == title);
        Assert.NotNull(created);
        Assert.Equal(ArticleStatus.Draft, created!.Status);
        Assert.False(string.IsNullOrEmpty(created.AuthorUserId));
    }

    [Fact]
    public async Task Author_CannotPublish_EvenWithDirectPost()
    {
        var client = await LoginAsAsync("author@vnews.local", "Demo@12345");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var categoryId = await context.Categories.Select(c => c.Id).FirstAsync();

        var article = new Article
        {
            Title = $"Bai cho duyet {Guid.NewGuid():N}",
            Slug = $"bai-cho-duyet-{Guid.NewGuid():N}",
            Summary = "Tom tat bai cho kiem thu quyen han.",
            Body = "<p>Noi dung.</p>",
            Status = ArticleStatus.PendingReview,
            CategoryId = categoryId,
            AuthorName = "Tac gia demo",
            CreatedAt = DateTime.UtcNow
        };
        context.Articles.Add(article);
        await context.SaveChangesAsync();

        var cmsPage = await client.GetAsync("/cms");
        var token = await ExtractAntiforgeryTokenAsync(cmsPage);

        var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = token };
        var response = await client.PostAsync($"/cms/CmsArticles/Publish/{article.Id}", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("khong-co-quyen", response.Headers.Location!.ToString());

        var reloaded = await context.Articles.AsNoTracking().FirstAsync(a => a.Id == article.Id);
        Assert.Equal(ArticleStatus.PendingReview, reloaded.Status);
    }
}
