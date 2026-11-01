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
