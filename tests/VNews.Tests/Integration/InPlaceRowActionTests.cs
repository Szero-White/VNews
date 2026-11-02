using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>Delete / restore from the article list answer JSON when asked with fetch (the page stays), and keep the ordinary redirect otherwise.</summary>
public sealed class InPlaceRowActionTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public InPlaceRowActionTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    private async Task<int> NewPublishedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var article = new Article
        {
            Title = "Bài xóa tại chỗ " + Guid.NewGuid().ToString("N")[..6], Slug = "xoa-tai-cho-" + Guid.NewGuid().ToString("N")[..8], Summary = "s", Body = "<p>b</p>",
            AuthorName = "A", Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow, CategoryId = await db.Categories.Select(c => c.Id).FirstAsync(),
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article.Id;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> fields, bool fetch)
    {
        var token = await HttpHelpers.TokenFromAsync(await client.GetAsync("/cms/CmsArticles"));
        fields["__RequestVerificationToken"] = token;
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
        if (fetch)
        {
            request.Headers.Add("X-Requested-With", "fetch");
            request.Headers.Add("Accept", "application/json");
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Trash_WithFetch_AnswersJsonWithFreshFigures_AndRestoreBringsItBack()
    {
        var id = await NewPublishedAsync();
        using var client = await _factory.LoginCmsAsync("admin@vnews.local");

        var trashed = await PostAsync(client, $"/cms/CmsTrash/Move/{id}", new(), fetch: true);
        Assert.Equal(HttpStatusCode.OK, trashed.StatusCode);
        Assert.StartsWith("application/json", trashed.Content.Headers.ContentType!.MediaType);
        using (var json = JsonDocument.Parse(await trashed.Content.ReadAsStringAsync()))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(id, json.RootElement.GetProperty("ids")[0].GetInt32());
            Assert.True(json.RootElement.GetProperty("stats").GetProperty("trashed").GetInt32() >= 1);
        }

        var again = await PostAsync(client, $"/cms/CmsTrash/Move/{id}", new(), fetch: true); // already in the trash: a clear refusal, still JSON
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        using (var json = JsonDocument.Parse(await again.Content.ReadAsStringAsync())) Assert.False(json.RootElement.GetProperty("ok").GetBoolean());

        var restored = await PostAsync(client, "/cms/CmsTrash/RestoreMany", new() { ["ids"] = id.ToString() }, fetch: true);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        using var restoredJson = JsonDocument.Parse(await restored.Content.ReadAsStringAsync());
        Assert.Equal(id, restoredJson.RootElement.GetProperty("ids")[0].GetInt32());

        using var scope = _factory.Services.CreateScope();
        Assert.Equal(ArticleStatus.Published, (await scope.ServiceProvider.GetRequiredService<VNewsDbContext>().Articles.AsNoTracking().SingleAsync(a => a.Id == id)).Status);
    }

    [Fact]
    public async Task Trash_WithoutFetch_StillRedirects()
    {
        var id = await NewPublishedAsync();
        using var client = await _factory.LoginCmsAsync("admin@vnews.local");
        var response = await PostAsync(client, $"/cms/CmsTrash/Move/{id}", new(), fetch: false);
        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.OK); // client follows or not; never JSON
        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
