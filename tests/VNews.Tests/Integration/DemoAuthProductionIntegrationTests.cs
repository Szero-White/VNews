using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Tests.Support;
using VNews.Web.Demo;

namespace VNews.Tests.Integration;

/// <summary>Production: refused even when the configuration wrongly says Enabled=true.</summary>
public class DemoAuthProductionIntegrationTests : IClassFixture<DemoAuthProductionWebApplicationFactory>
{
    private readonly DemoAuthProductionWebApplicationFactory _factory;

    public DemoAuthProductionIntegrationTests(DemoAuthProductionWebApplicationFactory factory) => _factory = factory;

    // Production forces Secure cookies, so requests must arrive over HTTPS (as behind TLS).
    private HttpClient Https() => _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    [Theory]
    [InlineData("reader")]
    [InlineData("author")]
    [InlineData("editor")]
    [InlineData("admin")]
    public async Task EveryRole_IsRefused_InProduction_EvenWithEnabledTrue(string role)
    {
        var client = Https();
        var response = await DemoAuthIntegrationTests.PostDemoAsync(client, role);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(HttpHelpers.SetsAuthCookie(response));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/cms")).StatusCode);
    }

    [Fact]
    public async Task NoDemoOffer_IsRendered_InProduction()
    {
        var client = Https();
        var html = await client.GetStringAsync("/tai-khoan/dang-nhap");
        Assert.DoesNotContain("Trải nghiệm sản phẩm", html);
        Assert.DoesNotContain("demo-card", html);
        Assert.DoesNotContain("data-demo-login", await client.GetStringAsync("/")); // the stylesheet is shared, the offer markup is not
    }

    [Fact]
    public async Task NoDemoAccounts_AreCreated_InProduction()
    {
        await DemoAuthIntegrationTests.PostDemoAsync(Https(), "admin");

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Empty(users.Users.Where(u => u.Email!.EndsWith("@demo.vnews.local")));
    }
}

/// <summary>A public portfolio site on a demo-named database that opted in with DemoAuth:AllowPublicHost (the Somee setup).</summary>
public sealed class DemoAuthPublicHostWebApplicationFactory : IsolatedDbWebApplicationFactory
{
    protected override string EnvironmentName => "Production";

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration =>
        base.ExtraConfiguration
            .Append(new KeyValuePair<string, string?>("DemoAuth:Enabled", "true"))
            .Append(new KeyValuePair<string, string?>("DemoAuth:AllowPublicHost", "true"));
}

public class DemoAuthPublicHostIntegrationTests : IClassFixture<DemoAuthPublicHostWebApplicationFactory>
{
    private readonly DemoAuthPublicHostWebApplicationFactory _factory;

    public DemoAuthPublicHostIntegrationTests(DemoAuthPublicHostWebApplicationFactory factory) => _factory = factory;

    private HttpClient Https() => _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://vnews-szero.somee.com") });

    [Fact]
    public async Task TheFourButtons_AreShown_ToAnyVisitor()
    {
        var html = await Https().GetStringAsync("/tai-khoan/dang-nhap");
        foreach (var persona in DemoPersonas.All) Assert.Contains($"data-demo-role=\"{persona.Key}\"", html);
    }

    [Theory]
    [InlineData("reader", "/")]
    [InlineData("author", "/cms")]
    [InlineData("editor", "/cms")]
    [InlineData("admin", "/cms")]
    public async Task EveryRole_SignsIn_WithItsOwnRoleAndLimits(string role, string destinationStart)
    {
        var client = Https();
        var response = await DemoAuthIntegrationTests.PostDemoAsync(client, role);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(destinationStart, response.Headers.Location!.OriginalString);
        Assert.True(HttpHelpers.SetsAuthCookie(response));

        var cms = (await client.GetAsync("/cms")).StatusCode;
        var users = (await client.GetAsync("/cms/CmsUsers")).StatusCode;
        if (role == "reader") Assert.Equal(HttpStatusCode.Redirect, cms);
        else Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/cms/CmsArticles")).StatusCode);
        Assert.Equal(role == "admin" ? HttpStatusCode.OK : HttpStatusCode.Redirect, users);
    }
}
