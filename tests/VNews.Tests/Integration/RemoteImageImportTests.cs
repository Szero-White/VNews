using System.Net;
using System.Net.Sockets;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Tests.Support;
using VNews.Web.Services;

namespace VNews.Tests.Integration;

/// <summary>Keeping a pasted picture: the address policy (no private network), the fetcher's checks, and the staff-only endpoint.</summary>
public sealed class RemoteImageImportTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;

    public RemoteImageImportTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("93.184.216.34", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("192.168.1.10", false)]
    [InlineData("169.254.169.254", false)]   // cloud metadata
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]  // IPv4 inside IPv6
    [InlineData("::ffff:10.0.0.5", false)]
    public void AddressPolicy_AllowsOnlyPublicAddresses(string address, bool expected)
        => Assert.Equal(expected, RemoteAddressPolicy.IsPublic(IPAddress.Parse(address)));

    [Theory]
    [InlineData("https://example.test/a.jpg", true)]
    [InlineData("http://example.test/a.png?x=1", true)]
    [InlineData("ftp://example.test/a.jpg", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://user:pass@example.test/a.jpg", false)]
    [InlineData("/relative/a.jpg", false)]
    [InlineData("", false)]
    public void Url_OnlyPublicWebAddressesWithoutCredentialsAreAccepted(string url, bool expected)
        => Assert.Equal(expected, RemoteImageFetcher.IsAcceptableUrl(url, out _));

    [Fact]
    public async Task Fetcher_StoresAPicture_AndRefusesWrongTypesHugeFilesAndErrors()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
        using var server = LocalServer.Start(path => path switch
        {
            "/ok.png" => ("image/png", png),
            "/page.html" => ("text/html", System.Text.Encoding.UTF8.GetBytes("<html></html>")),
            "/big.jpg" => ("image/jpeg", new byte[ImageUploadLimits.MaxBytes + 10]),
            _ => (null, Array.Empty<byte>()),
        });
        var uploads = new RecordingUploads();
        var fetcher = new RemoteImageFetcher(new HttpClient(), uploads, Microsoft.Extensions.Logging.Abstractions.NullLogger<RemoteImageFetcher>.Instance);

        var ok = await fetcher.ImportAsync(server.Url("/ok.png"));
        Assert.True(ok.Success);
        Assert.Equal(png, uploads.LastBytes);
        Assert.Equal("image/png", uploads.LastContentType);
        Assert.EndsWith(".png", uploads.LastFileName);

        Assert.False((await fetcher.ImportAsync(server.Url("/page.html"))).Success);   // not an image
        Assert.False((await fetcher.ImportAsync(server.Url("/big.jpg"))).Success);     // over the size limit
        Assert.False((await fetcher.ImportAsync(server.Url("/missing.png"))).Success); // 404
        Assert.False((await fetcher.ImportAsync("file:///etc/passwd")).Success);
        Assert.Equal(1, uploads.Saved); // only the valid picture reached the upload service
    }

    [Fact]
    public async Task Fetcher_WithTheSafeHandler_RefusesTheLoopbackMachine()
    {
        using var server = LocalServer.Start(_ => ("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        var uploads = new RecordingUploads();
        var fetcher = new RemoteImageFetcher(new HttpClient(RemoteAddressPolicy.CreateHandler()), uploads, Microsoft.Extensions.Logging.Abstractions.NullLogger<RemoteImageFetcher>.Instance);

        Assert.False((await fetcher.ImportAsync(server.Url("/a.png"))).Success);
        Assert.Equal(0, uploads.Saved);
        Assert.Equal(0, server.Requests); // the connection was never made
    }

    [Fact]
    public async Task Endpoint_IsStaffOnly_NeedsAToken_AndNeverReachesThePrivateNetwork()
    {
        var anonymous = _factory.NewClient();
        Assert.NotEqual(HttpStatusCode.OK, (await anonymous.PostAsync("/cms/CmsImages/Import", new FormUrlEncodedContent(new Dictionary<string, string> { ["url"] = "https://example.test/a.png" }))).StatusCode);

        var reader = await _factory.LoginCmsAsync((await _factory.CreateUserAsync(Roles.Reader)).Email, TestUsers.Password);
        Assert.NotEqual(HttpStatusCode.OK, (await reader.PostAsync("/cms/CmsImages/Import", new FormUrlEncodedContent(new Dictionary<string, string> { ["url"] = "https://example.test/a.png" }))).StatusCode);

        var author = await _factory.LoginCmsAsync("author@vnews.local");
        var noToken = await author.PostAsync("/cms/CmsImages/Import", new FormUrlEncodedContent(new Dictionary<string, string> { ["url"] = "https://example.test/a.png" }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode); // antiforgery

        var token = await HttpHelpers.TokenFromAsync(await author.GetAsync("/cms/CmsArticles/Create"));
        async Task<HttpStatusCode> Import(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/cms/CmsImages/Import") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["url"] = url }) };
            request.Headers.Add("RequestVerificationToken", token);
            return (await author.SendAsync(request)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.BadRequest, await Import("not a url"));
        Assert.Equal(HttpStatusCode.BadRequest, await Import("file:///c:/windows/win.ini"));
        Assert.Equal(HttpStatusCode.BadRequest, await Import("http://127.0.0.1:5/a.png"));          // loopback
        Assert.Equal(HttpStatusCode.BadRequest, await Import("http://169.254.169.254/latest/meta")); // cloud metadata
        Assert.Equal(HttpStatusCode.BadRequest, await Import("http://[::1]/a.png"));
    }

    private sealed class RecordingUploads : IImageUploadService
    {
        public int Saved { get; private set; }
        public byte[] LastBytes { get; private set; } = Array.Empty<byte>();
        public string LastContentType { get; private set; } = string.Empty;
        public string LastFileName { get; private set; } = string.Empty;

        public Task<ImageUploadResult> SaveAsync(Stream content, string originalFileName, string contentType, long length, CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            Saved++;
            LastBytes = copy.ToArray();
            LastContentType = contentType;
            LastFileName = originalFileName;
            return Task.FromResult(new ImageUploadResult { Success = true, Url = "/uploads/articles/test.png" });
        }
    }

    /// <summary>A tiny web server on the loopback interface; the handler returns (content type, body), or a null type for 404.</summary>
    private sealed class LocalServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly int _port;
        public int Requests { get; private set; }

        private LocalServer(Func<string, (string? Type, byte[] Body)> handler)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            _port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _ = Task.Run(async () =>
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context;
                    try { context = await _listener.GetContextAsync(); } catch { return; }
                    Requests++;
                    var (type, body) = handler(context.Request.Url!.AbsolutePath);
                    if (type is null) context.Response.StatusCode = 404;
                    else { context.Response.ContentType = type; context.Response.OutputStream.Write(body); }
                    context.Response.Close();
                }
            });
        }

        public static LocalServer Start(Func<string, (string? Type, byte[] Body)> handler) => new(handler);
        public string Url(string path) => $"http://127.0.0.1:{_port}{path}";
        public void Dispose() => _listener.Close();
    }
}
