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

/// <summary>Sign-in/registration POSTs are rate limited per client IP; normal page loads are not.</summary>
public class RateLimitingIntegrationTests : IClassFixture<RateLimitedWebApplicationFactory>
{
    private readonly RateLimitedWebApplicationFactory _factory;

    public RateLimitingIntegrationTests(RateLimitedWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task FourthSignInAttemptWithinTheWindow_Returns429WithRetryAfter()
    {
        var client = _factory.NewClient();
        var attempt = () => client.PostFormAsync("/tai-khoan/dang-nhap", "/tai-khoan/dang-nhap",
            new() { ["Email"] = "nobody@example.test", ["Password"] = "Wrong-Password-1" });

        for (var i = 1; i <= 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await attempt()).StatusCode);
        }

        var limited = await attempt();
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Contains("quá nhanh", await limited.Content.ReadAsStringAsync());
        Assert.Contains(_factory.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Rate limit hit"));

        // Reading pages is never limited, and the limit covers the CMS and registration forms too.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/tai-khoan/dang-nhap")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
    }
}
