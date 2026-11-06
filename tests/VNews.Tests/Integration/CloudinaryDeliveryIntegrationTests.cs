using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;
using VNews.Web.Services;

namespace VNews.Tests.Integration;

/// <summary>The site with Cloudinary configured (cloud name only): pages print Cloudinary URLs and old addresses redirect there.</summary>
public class CloudinaryDeliveryWebApplicationFactory : IsolatedDbWebApplicationFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration =>
        base.ExtraConfiguration.Append(new KeyValuePair<string, string?>("Cloudinary:CloudName", "vnews-test-cloud"));
}

/// <summary>Same, plus a fake picture store in place of the real Cloudinary upload, so no test ever reaches the network.</summary>
public sealed class CloudinaryUploadWebApplicationFactory : CloudinaryDeliveryWebApplicationFactory
{
    public List<(string Name, byte[] Content)> Stored { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddSingleton<IExternalImageStore>(new RecordingStore(Stored)));
    }

    private sealed class RecordingStore : IExternalImageStore
    {
        private readonly List<(string Name, byte[] Content)> _stored;
        public RecordingStore(List<(string Name, byte[] Content)> stored) => _stored = stored;

        public Task StoreAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            _stored.Add((fileName, content));
            return Task.CompletedTask;
        }
    }
}

public class CloudinaryDeliveryIntegrationTests :
    IClassFixture<CloudinaryDeliveryWebApplicationFactory>, IClassFixture<CloudinaryUploadWebApplicationFactory>, IClassFixture<IsolatedDbWebApplicationFactory>
{
    private const string CoverId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string BodyId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string GalleryId = "cccccccccccccccccccccccccccccccc";
    private const string Base = "https://res.cloudinary.com/vnews-test-cloud/image/upload/f_auto,q_auto/vnews/articles/";

    private readonly CloudinaryDeliveryWebApplicationFactory _cloud;
    private readonly CloudinaryUploadWebApplicationFactory _upload;
    private readonly IsolatedDbWebApplicationFactory _local;

    public CloudinaryDeliveryIntegrationTests(CloudinaryDeliveryWebApplicationFactory cloud, CloudinaryUploadWebApplicationFactory upload, IsolatedDbWebApplicationFactory local)
    {
        _cloud = cloud;
        _upload = upload;
        _local = local;
    }

    private static async Task<Article> AddArticleAsync(IsolatedDbWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var key = Guid.NewGuid().ToString("N");
        var article = new Article
        {
            Title = "Bài có ảnh " + key, Slug = "bai-co-anh-" + key, Summary = "Tóm tắt.",
            Body = $"<p>Chữ.</p><figure><img src=\"/uploads/articles/{BodyId}.png\" alt=\"\"><figcaption>Chú thích</figcaption></figure>",
            AuthorName = "Tác giả thử", Status = ArticleStatus.Published, PublishedAt = DateTime.UtcNow.AddMinutes(-5),
            CoverImageUrl = $"/uploads/articles/{CoverId}.png",
            CategoryId = await db.Categories.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync(),
        };
        article.Images.Add(new ArticleImage { Url = $"/uploads/articles/{GalleryId}.jpg", SortOrder = 0 });
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article;
    }

    [Fact]
    public async Task AnOldAddress_RedirectsToCloudinary_WithTheSameId_AndIsCachedByTheBrowser()
    {
        var response = await _cloud.NewClient().GetAsync($"/uploads/articles/{CoverId}.png");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(Base + CoverId, response.Headers.Location!.ToString());
        Assert.Contains("max-age=86400", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData("/uploads/articles/not-a-generated-name.png")]
    [InlineData("/uploads/articles/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.png")]
    [InlineData("/uploads/articles/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.gif")]
    [InlineData("/uploads/articles/..%2f..%2fappsettings.json")]
    public async Task OnlyAddressesOfTheGeneratedShape_AreRedirected_NothingElseIsAnOpenRedirect(string path)
    {
        var response = await _cloud.NewClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task APictureThatExistsOnTheServerDisk_IsStillServedFromThere()
    {
        var storage = _cloud.Services.GetRequiredService<UploadStorage>();
        const string id = "dddddddddddddddddddddddddddddddd";
        await File.WriteAllBytesAsync(Path.Combine(storage.ArticlesPath, id + ".png"), new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 });
        try
        {
            var response = await _cloud.NewClient().GetAsync($"/uploads/articles/{id}.png");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            File.Delete(Path.Combine(storage.ArticlesPath, id + ".png"));
        }
    }

    [Fact]
    public async Task TheArticlePage_PrintsCloudinaryUrls_ForCover_Body_AndGallery()
    {
        var article = await AddArticleAsync(_cloud);

        var html = await _cloud.NewClient().GetStringAsync($"/tin/{article.Slug}");

        Assert.Contains($"src=\"{Base}{CoverId}\"", html);
        Assert.Contains($"src=\"{Base}{BodyId}\"", html);
        Assert.Contains($"src=\"{Base}{GalleryId}\"", html);
        Assert.Contains($"href=\"{Base}{GalleryId}\"", html);
        Assert.DoesNotContain("src=\"/uploads/articles/", html);
        Assert.DoesNotContain("href=\"/uploads/articles/", html);
        // The bookmark button keeps the stored address on purpose (bookmarks.js only accepts that shape); it is shown through the redirect.
        Assert.Contains($"data-news-image=\"/uploads/articles/{CoverId}.png\"", html);
    }

    [Fact]
    public async Task ListsAndCards_PrintTheCloudinaryCover_Too()
    {
        var article = await AddArticleAsync(_cloud);

        var key = article.Title.Split(' ').Last();

        var html = await _cloud.NewClient().GetStringAsync($"/tim-kiem?q={key}");

        Assert.Contains(article.Slug, html);
        Assert.Contains($"{Base}{CoverId}", html);
        Assert.DoesNotContain($"/uploads/articles/{CoverId}", html);
    }

    [Fact]
    public async Task WithoutCloudinary_NothingChanges_PagesKeepTheLocalAddress_AndAnUnknownPictureIsNotFound()
    {
        var article = await AddArticleAsync(_local);
        var client = _local.NewClient();

        var html = await client.GetStringAsync($"/tin/{article.Slug}");

        Assert.Contains($"src=\"/uploads/articles/{CoverId}.png\"", html);
        Assert.Contains($"/uploads/articles/{BodyId}.png", html);
        Assert.DoesNotContain("res.cloudinary.com", html);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/uploads/articles/{CoverId}.png")).StatusCode);
    }

    [Fact]
    public async Task ACmsUpload_GoesToTheStore_AndTheReturnedAddressKeepsTheStoredShape_ThenRedirectsToCloudinary()
    {
        var author = await _upload.LoginCmsAsync("author@vnews.local");
        var token = await HttpHelpers.TokenFromAsync(await author.GetAsync("/cms/CmsArticles/Create"));
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(new byte[200]).ToArray();

        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "anh.png");
        var request = new HttpRequestMessage(HttpMethod.Post, "/cms/CmsArticles/UploadCoverImage") { Content = content };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await author.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var url = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("url").GetString()!;
        Assert.Matches("^/uploads/articles/[0-9a-f]{32}\\.png$", url);
        var stored = Assert.Single(_upload.Stored);
        Assert.Equal(png, stored.Content);
        Assert.Equal(url, "/uploads/articles/" + stored.Name);

        // The editor's preview of that address works at once: it is redirected to the picture on Cloudinary.
        var preview = await author.GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, preview.StatusCode);
        Assert.Equal(Base + Path.GetFileNameWithoutExtension(stored.Name), preview.Headers.Location!.ToString());
    }
}
