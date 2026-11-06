using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>A real deployment shows the database it was given: it never invents sample content, it still migrates, and its keys can be kept.</summary>
public class ProductionStartupTests : IClassFixture<ProductionAsDeployedWebApplicationFactory>
{
    private readonly ProductionAsDeployedWebApplicationFactory _factory;

    public ProductionStartupTests(ProductionAsDeployedWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Production_OnAnEmptyDatabase_MigratesButSeedsNoCategoriesOrArticles_AndKeepsTheRoles()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await db.Categories.CountAsync());
        Assert.Equal(0, await db.Articles.CountAsync());
        Assert.Equal(0, await db.Comments.CountAsync());
        Assert.True(await db.Roles.AnyAsync(r => r.Name == "Admin")); // roles are structure, not content
        Assert.Equal(0, await db.Users.CountAsync());                 // no demo accounts either
    }

    [Fact]
    public async Task Production_OnAnEmptyDatabase_StillRendersThePublicPages()
    {
        // Production requires HTTPS cookies, so the request arrives as HTTPS (as it does behind TLS).
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        foreach (var path in new[] { "/", "/tin-moi-nhat", "/tim-kiem?q=gia", "/tai-khoan/dang-nhap", "/api/v1/articles" })
        {
            var response = await client.GetAsync(path);
            Assert.True((int)response.StatusCode < 500, $"{path} answered {(int)response.StatusCode} on an empty database");
        }
    }
}

public class DataProtectionKeysPathTests
{
    private sealed class Factory : IsolatedDbWebApplicationFactory
    {
        private readonly string _path;
        public Factory(string path) => _path = path;
        protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration =>
            base.ExtraConfiguration.Append(new KeyValuePair<string, string?>("DataProtection:KeysPath", _path));
    }

    [Fact]
    public void ARelativeKeysPath_IsTakenFromTheSitesFolder_NotFromTheWorkingDirectory()
    {
        var relative = Path.Combine("App_Data", "keys-test-" + Guid.NewGuid().ToString("N"));
        using var factory = new Factory(relative);
        var contentRoot = factory.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().ContentRootPath;
        var expected = Path.Combine(contentRoot, relative);
        try
        {
            var protector = factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");
            Assert.Equal("hello", protector.Unprotect(protector.Protect("hello")));
            Assert.NotEmpty(Directory.GetFiles(expected, "key-*.xml"));
        }
        finally
        {
            if (Directory.Exists(expected)) Directory.Delete(expected, recursive: true);
            var parent = Path.Combine(contentRoot, "App_Data");
            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
        }
    }

    [Fact]
    public void KeysPath_KeepsTheKeysInTheConfiguredFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vnews-keys-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new Factory(folder);
            var protector = factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");
            Assert.Equal("hello", protector.Unprotect(protector.Protect("hello")));
            Assert.NotEmpty(Directory.GetFiles(folder, "key-*.xml"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
