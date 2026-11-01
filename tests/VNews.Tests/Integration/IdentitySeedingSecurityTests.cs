using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Identity;
using VNews.Infrastructure.Seed;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>Production: no public-password accounts, and the only first administrator comes from secret configuration.</summary>
public sealed class ProductionBootstrapFactory : IsolatedDbWebApplicationFactory
{
    protected override string EnvironmentName => "Production";
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration => base.ExtraConfiguration.Concat(new[]
    {
        new KeyValuePair<string, string?>("Bootstrap:AdminEmail", "owner@example.test"),
        new KeyValuePair<string, string?>("Bootstrap:AdminPassword", "Str0ng-Operator-Secret!"),
    });
}

public sealed class ProductionPublicPasswordBootstrapFactory : IsolatedDbWebApplicationFactory
{
    protected override string EnvironmentName => "Production";
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration => base.ExtraConfiguration.Concat(new[]
    {
        new KeyValuePair<string, string?>("Bootstrap:AdminEmail", "owner@example.test"),
        new KeyValuePair<string, string?>("Bootstrap:AdminPassword", "Demo@12345"),
    });
}

public class ProductionBootstrapAdminTests : IClassFixture<ProductionBootstrapFactory>
{
    private readonly ProductionBootstrapFactory _factory;
    public ProductionBootstrapAdminTests(ProductionBootstrapFactory factory) => _factory = factory;

    [Fact]
    public async Task Production_HasNoDemoAccounts_AndExactlyTheConfiguredAdministrator()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var email in new[] { "admin@vnews.local", "editor@vnews.local", "author@vnews.local" })
            Assert.Null(await users.FindByEmailAsync(email));
        Assert.Empty(users.Users.Where(u => u.Email!.EndsWith("@demo.vnews.local")));

        var admins = await users.GetUsersInRoleAsync(Roles.Admin);
        var admin = Assert.Single(admins);
        Assert.Equal("owner@example.test", admin.Email);
        Assert.True(await users.CheckPasswordAsync(admin, "Str0ng-Operator-Secret!"));
        Assert.False(await users.CheckPasswordAsync(admin, "Demo@12345")); // never the public password
    }

    [Fact]
    public async Task TheConfiguredAdministrator_CanSignIn_AndReachesUserAdministration()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var response = await client.PostFormAsync("/tai-khoan/dang-nhap", "/tai-khoan/dang-nhap",
            new() { ["Email"] = "owner@example.test", ["Password"] = "Str0ng-Operator-Secret!" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/CmsOverview", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/cms/CmsOverview")).StatusCode);
    }

    [Fact]
    public async Task BootstrapNeverRunsAgain_OnceAnAdministratorExists()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await IdentitySeeder.EnsureBootstrapAdminAsync(users, "second@example.test", "Another-Str0ng-One!");

        Assert.Equal(IdentitySeeder.BootstrapAdminResult.AdminAlreadyExists, result);
        Assert.Null(await users.FindByEmailAsync("second@example.test"));
    }
}

public class ProductionPublicPasswordBootstrapTests : IClassFixture<ProductionPublicPasswordBootstrapFactory>
{
    private readonly ProductionPublicPasswordBootstrapFactory _factory;
    public ProductionPublicPasswordBootstrapTests(ProductionPublicPasswordBootstrapFactory factory) => _factory = factory;

    [Fact]
    public async Task TheConfiguredPublicDemoPassword_IsRefused_SoNoAdministratorIsCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Null(await users.FindByEmailAsync("owner@example.test"));
        Assert.Empty(await users.GetUsersInRoleAsync(Roles.Admin));
        Assert.Contains(_factory.Logs.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error && e.Message.Contains("public demo password"));
        Assert.DoesNotContain(_factory.Logs.Entries, e => e.Message.Contains("Demo@12345") || e.State.Contains("Demo@12345"));
    }
}

public class IdentitySeedingRulesTests : IClassFixture<IsolatedDbWebApplicationFactory>
{
    private readonly IsolatedDbWebApplicationFactory _factory;
    public IdentitySeedingRulesTests(IsolatedDbWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(null, "Str0ng-Operator-Secret!")]
    [InlineData("owner@example.test", null)]
    [InlineData("", "Str0ng-Operator-Secret!")]
    [InlineData("owner@example.test", "   ")]
    public async Task WithoutBothValues_NothingIsCreated(string? email, string? password)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(IdentitySeeder.BootstrapAdminResult.NotConfigured, await IdentitySeeder.EnsureBootstrapAdminAsync(users, email, password));
    }

    [Fact]
    public void ThePublicDemoPassword_IsRecognised_AndNothingElseIs()
    {
        Assert.True(IdentitySeeder.IsPublicDemoPassword("Demo@12345"));
        Assert.False(IdentitySeeder.IsPublicDemoPassword("demo@12345"));
        Assert.False(IdentitySeeder.IsPublicDemoPassword(null));
    }

    [Fact]
    public async Task InDevelopment_TheLongStandingDemoAccountsStillExist_WithAdminAmongThem()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.NotNull(await users.FindByEmailAsync("admin@vnews.local"));
        Assert.Equal(IdentitySeeder.BootstrapAdminResult.AdminAlreadyExists, await IdentitySeeder.EnsureBootstrapAdminAsync(users, "x@example.test", "Another-Str0ng-One!"));
    }
}
