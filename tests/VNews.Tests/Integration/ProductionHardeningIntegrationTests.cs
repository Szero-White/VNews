using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Infrastructure.Services;
using VNews.Tests.Support;

namespace VNews.Tests.Integration;

/// <summary>Hardening that only applies outside Development.</summary>
public class ProductionHardeningIntegrationTests : IClassFixture<ProductionWebApplicationFactory>
{
    private readonly ProductionWebApplicationFactory _factory;

    public ProductionHardeningIntegrationTests(ProductionWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void Cookies_AreSecureOnly_InProduction()
    {
        var cookies = _factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always, cookies.Cookie.SecurePolicy);
        Assert.True(cookies.Cookie.HttpOnly);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Lax, cookies.Cookie.SameSite);
    }

    [Fact]
    public async Task ErrorPage_IsFriendlyVietnamese_WithTraceId_AndNoTechnicalDetail()
    {
        // Production forces Secure cookies, so requests must arrive over HTTPS (as they would behind TLS).
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/Home/Error");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Mã tra cứu lỗi", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Development", html);
        Assert.DoesNotContain("StackTrace", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", html);
    }

    [Fact]
    public async Task DemoAccounts_AreNotSeeded_InProduction()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync("admin@vnews.local"));
    }
}
