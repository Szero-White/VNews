using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using VNews.Tests.Support;
using VNews.Web.Services;

namespace VNews.Tests.Unit;

/// <summary>Upload hardening: real files pass; spoofed, oversized and SVG uploads are refused and nothing is written.</summary>
public sealed class ImageUploadServiceTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "vnews-upload-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ListLogger<ImageUploadService> _log = new();
    private readonly ImageUploadService _service;

    public ImageUploadServiceTests()
    {
        Directory.CreateDirectory(_webRoot);
        _service = new ImageUploadService(new TestEnvironment(_webRoot), _log);
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    private static byte[] Jpeg(int extra = 64) => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.Concat(new byte[extra]).ToArray();
    private static byte[] Png(int extra = 64) => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.Concat(new byte[extra]).ToArray();
    private static byte[] WebP() => Encoding.ASCII.GetBytes("RIFF").Concat(new byte[4]).Concat(Encoding.ASCII.GetBytes("WEBP")).Concat(new byte[32]).ToArray();

    private Task<VNews.Application.Interfaces.ImageUploadResult> Upload(byte[] bytes, string fileName, string contentType, long? declaredLength = null)
        => _service.SaveAsync(new MemoryStream(bytes), fileName, contentType, declaredLength ?? bytes.Length);

    private string[] SavedFiles()
    {
        var dir = Path.Combine(_webRoot, "uploads", "articles");
        return Directory.Exists(dir) ? Directory.GetFiles(dir) : Array.Empty<string>();
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg", ".jpg")]
    [InlineData("photo.JPEG", "image/jpeg", ".jpg")]
    [InlineData("photo.png", "image/png", ".png")]
    [InlineData("photo.webp", "image/webp", ".webp")]
    public async Task ValidRasterImage_IsSavedUnderGeneratedName(string fileName, string contentType, string expectedExt)
    {
        var bytes = expectedExt == ".png" ? Png() : expectedExt == ".webp" ? WebP() : Jpeg();

        var result = await Upload(bytes, fileName, contentType);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.StartsWith("/uploads/articles/", result.Url);
        Assert.EndsWith(expectedExt, result.Url);
        Assert.DoesNotContain("photo", result.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Single(SavedFiles());
    }

    [Fact]
    public async Task Svg_IsRejected_EvenWhenHarmless()
    {
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><circle r=\"5\"/></svg>");

        var result = await Upload(svg, "logo.svg", "image/svg+xml");

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Fact]
    public async Task ScriptBearingSvgDisguisedAsPng_IsRejectedBySignature()
    {
        var evil = Encoding.UTF8.GetBytes("<svg onclick=\"alert(1)\" xmlns=\"http://www.w3.org/2000/svg\"/>");

        var result = await Upload(evil, "evil.png", "image/png");

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Fact]
    public async Task ExecutableRenamedToJpg_IsRejected()
    {
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }.Concat(new byte[100]).ToArray(); // "MZ" PE header

        var result = await Upload(exe, "invoice.jpg", "image/jpeg");

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Fact]
    public async Task JpegBytesWithPngExtension_IsRejected()
    {
        var result = await Upload(Jpeg(), "photo.png", "image/png");

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Fact]
    public async Task MimeTypeThatDoesNotMatchExtension_IsRejected()
    {
        var result = await Upload(Png(), "photo.png", "image/jpeg");

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Theory]
    [InlineData("shell.php")]
    [InlineData("photo.png.exe")]
    [InlineData("noextension")]
    [InlineData("")]
    public async Task DisallowedExtension_IsRejected(string fileName)
    {
        var result = await Upload(Png(), fileName, "image/png");

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Fact]
    public async Task PathTraversalInFileName_CannotEscapeUploadFolder()
    {
        var result = await Upload(Png(), "../../../evil.png", "image/png");

        Assert.True(result.Success);
        Assert.DoesNotContain("..", result.Url);
        var saved = Assert.Single(SavedFiles());
        Assert.StartsWith(Path.Combine(_webRoot, "uploads", "articles"), Path.GetFullPath(saved));
        Assert.False(File.Exists(Path.Combine(_webRoot, "evil.png")));
    }

    [Fact]
    public async Task DeclaredSizeTooLarge_IsRejected()
    {
        var result = await Upload(Png(), "big.png", "image/png", declaredLength: 4 * 1024 * 1024);

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Fact]
    public async Task StreamLargerThanDeclaredLength_IsRejected()
    {
        // Client claims 1 KB but actually sends > 3 MB: the real stream is bounded, not the claim.
        var oversized = Png(extra: 3 * 1024 * 1024 + 10);

        var result = await Upload(oversized, "liar.png", "image/png", declaredLength: 1024);

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Fact]
    public async Task EmptyFile_IsRejected()
    {
        var result = await Upload(Array.Empty<byte>(), "empty.png", "image/png");

        Assert.False(result.Success);
        Assert.Empty(SavedFiles());
    }

    [Fact]
    public async Task Rejection_IsLogged_WithoutTheClientFileName()
    {
        await Upload(Png(), "secret-name-<script>.php", "image/png");

        var warning = Assert.Single(_log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("extension", warning.Message);
        Assert.DoesNotContain("secret-name", warning.Message);
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
}
