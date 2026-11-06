using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using VNews.Application.Interfaces;
using VNews.Tests.Support;
using VNews.Web.Services;

namespace VNews.Tests.Unit;

/// <summary>Cloudinary integration without the network: settings, address mapping, and the upload path with a fake picture store.</summary>
public sealed class CloudinaryImageDeliveryTests : IDisposable
{
    private const string Id = "0123456789abcdef0123456789abcdef";
    private const string Id2 = "fedcba9876543210fedcba9876543210";

    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "vnews-cloudinary-tests-" + Guid.NewGuid().ToString("N"));

    public CloudinaryImageDeliveryTests() => Directory.CreateDirectory(_webRoot);

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    private static CloudinaryOptions Options(params (string Key, string? Value)[] values) =>
        CloudinaryOptions.From(new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>("Cloudinary:" + v.Key, v.Value))).Build());

    private static ImageDelivery Delivery(params (string Key, string? Value)[] values) => new(Options(values));

    // ---------- settings ----------

    [Fact]
    public void NothingConfigured_MeansOff_AndKeepsTheLocalBehaviour()
    {
        var options = Options();
        Assert.False(options.CanDeliver);
        Assert.False(options.CanUpload);
        Assert.False(new ImageDelivery(options).Enabled);
    }

    [Fact]
    public void CloudNameAlone_ShowsPictures_ButCannotUpload_AndKeyAndSecretTurnUploadOn()
    {
        var show = Options(("CloudName", "my-cloud"));
        Assert.True(show.CanDeliver);
        Assert.False(show.CanUpload);

        var full = Options(("CloudName", "my-cloud"), ("ApiKey", "k"), ("ApiSecret", "s"));
        Assert.True(full.CanUpload);

        Assert.False(Options(("ApiKey", "k"), ("ApiSecret", "s")).CanUpload); // no cloud: nothing works
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("a/b")]
    [InlineData("evil.com/x")]
    [InlineData("cloud?x=1")]
    public void ACloudNameThatCouldChangeTheAddress_IsRefused(string cloud) => Assert.False(Options(("CloudName", cloud)).CanDeliver);

    [Fact]
    public void UnsafeFolderOrTransform_FallBackToTheDefaults_AndAnEmptyTransformMeansTheOriginal()
    {
        var odd = Options(("CloudName", "c"), ("Folder", "../etc"), ("DeliveryTransform", "w_100/../x"));
        Assert.Equal(CloudinaryOptions.DefaultFolder, odd.Folder);
        Assert.Equal(CloudinaryOptions.DefaultTransform, odd.DeliveryTransform);

        Assert.Equal("", Options(("CloudName", "c"), ("DeliveryTransform", "")).DeliveryTransform);
        Assert.Equal("shop/pictures", Options(("CloudName", "c"), ("Folder", "/shop/pictures/")).Folder);
    }

    [Fact]
    public void TheCredentialsNeverAppearInTheTextForm()
    {
        var text = Options(("CloudName", "my-cloud"), ("ApiKey", "KEY123456"), ("ApiSecret", "SECRET-VALUE")).ToString();
        Assert.DoesNotContain("KEY123456", text);
        Assert.DoesNotContain("SECRET-VALUE", text);
        Assert.Contains("my-cloud", text);
    }

    // ---------- address mapping ----------

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".webp")]
    public void AStoredAddress_BecomesTheCloudinaryUrl_WithTheSameIdAndNoExtension(string extension)
    {
        var url = Delivery(("CloudName", "my-cloud")).Resolve($"/uploads/articles/{Id}{extension}");
        Assert.Equal($"https://res.cloudinary.com/my-cloud/image/upload/f_auto,q_auto/vnews/articles/{Id}", url);
    }

    [Fact]
    public void AnEmptyTransform_GivesTheOriginalPicture()
    {
        var url = Delivery(("CloudName", "my-cloud"), ("DeliveryTransform", ""), ("Folder", "news")).Resolve($"/uploads/articles/{Id}.png");
        Assert.Equal($"https://res.cloudinary.com/my-cloud/image/upload/news/{Id}", url);
    }

    [Theory]
    [InlineData("https://cdn.example.com/x.jpg")]
    [InlineData("/images/demo/fallback.svg")]
    [InlineData("/uploads/articles/ABCDEF0123456789ABCDEF0123456789.png")]       // not the generated shape (upper case)
    [InlineData("/uploads/articles/0123.png")]                                      // too short
    [InlineData("/uploads/articles/0123456789abcdef0123456789abcdef.gif")]         // not an accepted type
    [InlineData("/uploads/articles/../0123456789abcdef0123456789abcdef.png")]
    [InlineData("//evil.example/uploads/articles/0123456789abcdef0123456789abcdef.png")]
    public void ThingsThatAreNotOurStoredPictures_AreLeftAlone(string value) =>
        Assert.Equal(value, Delivery(("CloudName", "my-cloud")).Resolve(value));

    [Fact]
    public void WhenOff_EveryAddressIsReturnedUnchanged_AndEmptyStaysEmpty()
    {
        var off = Delivery();
        Assert.Equal($"/uploads/articles/{Id}.png", off.Resolve($"/uploads/articles/{Id}.png"));
        Assert.Null(off.Resolve(null));
        Assert.Equal("", off.Resolve(""));
        Assert.False(off.TryResolve($"/uploads/articles/{Id}.png", out _));
    }

    [Fact]
    public void ArticleHtml_GetsItsOwnPicturesRewritten_AndNothingElse()
    {
        var html = $"<p>See /uploads/articles/{Id}.png in text? no: <img src=\"/uploads/articles/{Id}.png\" alt=\"a\">" +
                   $"<figure><img src='/uploads/articles/{Id2}.jpg'></figure>" +
                   $"<img src=\"https://other.example/uploads/articles/{Id}.png\"></p>";
        var result = Delivery(("CloudName", "my-cloud")).RewriteHtml(html);

        Assert.Contains($"src=\"https://res.cloudinary.com/my-cloud/image/upload/f_auto,q_auto/vnews/articles/{Id}\"", result);
        Assert.Contains($"src='https://res.cloudinary.com/my-cloud/image/upload/f_auto,q_auto/vnews/articles/{Id2}'", result);
        Assert.Contains($"src=\"https://other.example/uploads/articles/{Id}.png\"", result); // a foreign host is not ours to change
        Assert.Contains($"See /uploads/articles/{Id}.png in text", result);                    // plain text is not an address
        Assert.DoesNotContain("src=\"/uploads/articles/", result);
    }

    [Fact]
    public void HtmlWithoutPictures_AndWhenOff_IsReturnedAsIs()
    {
        const string html = "<p>Không có ảnh.</p>";
        Assert.Same(html, Delivery(("CloudName", "my-cloud")).RewriteHtml(html));
        var withPicture = $"<img src=\"/uploads/articles/{Id}.png\">";
        Assert.Same(withPicture, Delivery().RewriteHtml(withPicture));
        Assert.Equal("", Delivery(("CloudName", "my-cloud")).RewriteHtml(null));
    }

    // ---------- upload path ----------

    private sealed class FakeStore : IExternalImageStore
    {
        public List<(string Name, byte[] Content)> Stored { get; } = new();
        public Exception? Failure { get; set; }

        public Task StoreAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
        {
            if (Failure is not null) throw Failure;
            Stored.Add((fileName, content));
            return Task.CompletedTask;
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public TestEnvironment(string webRoot) => WebRootPath = webRoot;
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "VNews.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }

    private ImageUploadService Service(IExternalImageStore? store, out UploadStorage storage)
    {
        storage = new UploadStorage(new TestEnvironment(_webRoot), new ConfigurationBuilder().Build());
        return new ImageUploadService(storage, new ListLogger<ImageUploadService>(), store);
    }

    private static byte[] Png() => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(new byte[64]).ToArray();

    [Fact]
    public async Task WithAnExternalStore_TheValidatedPictureGoesThere_NotToTheLocalDisk_AndTheStoredAddressKeepsItsShape()
    {
        var store = new FakeStore();
        var service = Service(store, out var storage);
        var bytes = Png();

        var result = await service.SaveAsync(new MemoryStream(bytes), "photo.png", "image/png", bytes.Length);

        Assert.True(result.Success);
        var single = Assert.Single(store.Stored);
        Assert.Equal(bytes, single.Content);
        Assert.Matches("^[0-9a-f]{32}\\.png$", single.Name);
        Assert.Equal("/uploads/articles/" + single.Name, result.Url);
        Assert.Empty(Directory.GetFiles(storage.ArticlesPath));
    }

    [Fact]
    public async Task WhenTheExternalStoreFails_TheUploadFails_WithNoAddressAndNothingOnDisk()
    {
        var store = new FakeStore { Failure = new InvalidOperationException("boom: secret-detail") };
        var service = Service(store, out var storage);
        var bytes = Png();

        var result = await service.SaveAsync(new MemoryStream(bytes), "photo.png", "image/png", bytes.Length);

        Assert.False(result.Success);
        Assert.Null(result.Url);
        Assert.DoesNotContain("secret-detail", result.ErrorMessage); // the editor sees a friendly message, not the exception text
        Assert.Empty(Directory.GetFiles(storage.ArticlesPath));
    }

    [Fact]
    public async Task TheExternalStoreIsNeverReached_ByAFileTheValidationRefuses()
    {
        var store = new FakeStore();
        var service = Service(store, out _);

        var svg = Encoding.UTF8.GetBytes("<svg onload=alert(1)/>");
        Assert.False((await service.SaveAsync(new MemoryStream(svg), "x.svg", "image/svg+xml", svg.Length)).Success);
        var fake = Encoding.UTF8.GetBytes("not a png");
        Assert.False((await service.SaveAsync(new MemoryStream(fake), "x.png", "image/png", fake.Length)).Success);

        Assert.Empty(store.Stored);
    }

    [Fact]
    public async Task WithoutAnExternalStore_ThePictureIsStillWrittenLocally()
    {
        var service = Service(null, out var storage);
        var bytes = Png();

        var result = await service.SaveAsync(new MemoryStream(bytes), "photo.png", "image/png", bytes.Length);

        Assert.True(result.Success);
        var file = Assert.Single(Directory.GetFiles(storage.ArticlesPath));
        Assert.Equal(bytes, File.ReadAllBytes(file));
        Assert.Equal("/uploads/articles/" + Path.GetFileName(file), result.Url);
    }
}
