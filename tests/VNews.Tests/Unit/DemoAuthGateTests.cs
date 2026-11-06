using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VNews.Domain.Identity;
using VNews.Web.Demo;

namespace VNews.Tests.Unit;

public class DemoAuthGateTests
{
    private sealed class FakeEnvironment : IHostEnvironment
    {
        public FakeEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "VNews.Web";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static DemoAuthGate Gate(bool enabled = true, string environment = "Development", string? database = "VNewsDemo", string[]? prefixes = null)
    {
        var options = new DemoAuthOptions { Enabled = enabled };
        if (prefixes is not null) options.AllowedDatabasePrefixes = prefixes;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = database is null ? null : $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;",
        }).Build();
        return new DemoAuthGate(Options.Create(options), new FakeEnvironment(environment), config);
    }

    private static HttpContext Request(string? remote = "127.0.0.1", string host = "localhost", params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remote is null ? null : IPAddress.Parse(remote);
        context.Request.Host = new HostString(host);
        foreach (var (name, value) in headers) context.Request.Headers[name] = value;
        return context;
    }

    [Fact]
    public void WithNoConfigurationAtAll_TheDemoIsOff() =>
        Assert.Equal(DemoAuthDecision.Disabled, Gate(enabled: new DemoAuthOptions().Enabled).Evaluate(Request()));

    [Fact]
    public void DefaultOptions_AreSafe()
    {
        var options = new DemoAuthOptions();
        Assert.False(options.Enabled);
        Assert.DoesNotContain(options.AllowedDatabasePrefixes, p => "VNewsDb".StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AllConditionsMet_IsAvailable() => Assert.Equal(DemoAuthDecision.Available, Gate().Evaluate(Request()));

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("")]
    public void AnyNonDevelopmentEnvironment_IsRefused_EvenWhenEnabled(string environment) =>
        Assert.Equal(DemoAuthDecision.NotDevelopment, Gate(environment: environment).Evaluate(Request()));

    [Theory]
    [InlineData("VNewsDb")]
    [InlineData("vnewsdb")]
    [InlineData("Production")]
    [InlineData("master")]
    [InlineData("")]
    [InlineData(null)]
    public void TheRealOrAnyUnlistedDatabase_IsRefused(string? database) =>
        Assert.Equal(DemoAuthDecision.DatabaseNotAllowed, Gate(database: database).Evaluate(Request()));

    [Theory]
    [InlineData("VNewsDemo")]
    [InlineData("VNewsDemo_2")]
    [InlineData("VNewsE2E")]
    [InlineData("VNewsTests_0123abcd")]
    [InlineData("vnewsdemo")]
    public void DemoAndTestDatabases_AreAllowed(string database) =>
        Assert.Equal(DemoAuthDecision.Available, Gate(database: database).Evaluate(Request()));

    [Fact]
    public void ThePrefixList_IsConfigurable_ButStillMustMatch()
    {
        Assert.Equal(DemoAuthDecision.Available, Gate(database: "MyDemoDb", prefixes: new[] { "MyDemo" }).Evaluate(Request()));
        Assert.Equal(DemoAuthDecision.DatabaseNotAllowed, Gate(database: "VNewsDb", prefixes: new[] { "MyDemo" }).Evaluate(Request()));
        Assert.Equal(DemoAuthDecision.DatabaseNotAllowed, Gate(database: "VNewsDb", prefixes: Array.Empty<string>()).Evaluate(Request()));
        Assert.Equal(DemoAuthDecision.DatabaseNotAllowed, Gate(database: "VNewsDb", prefixes: new[] { "" }).Evaluate(Request()));
    }

    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("10.0.0.5")]
    [InlineData("203.0.113.9")]
    [InlineData("2001:db8::1")]
    [InlineData(null)]
    public void RemoteOrUnknownClients_AreRefused(string? remote) =>
        Assert.Equal(DemoAuthDecision.NotLoopback, Gate().Evaluate(Request(remote)));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.5.5.5")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public void LoopbackClients_AreAccepted(string remote) =>
        Assert.Equal(DemoAuthDecision.Available, Gate().Evaluate(Request(remote)));

    [Theory]
    [InlineData("X-Forwarded-For", "203.0.113.9")]
    [InlineData("Forwarded", "for=203.0.113.9")]
    [InlineData("X-Forwarded-Host", "demo.example.com")]
    public void ProxiedRequests_AreRefused_BecauseTheyLookLocalButAreNot(string header, string value) =>
        Assert.Equal(DemoAuthDecision.ForwardedRequest, Gate().Evaluate(Request(headers: (header, value))));

    [Theory]
    [InlineData("demo.example.com")]
    [InlineData("localhost.evil.example")]
    [InlineData("192.168.1.20")]
    public void NonLocalHostNames_AreRefused(string host) =>
        Assert.Equal(DemoAuthDecision.NotLocalHost, Gate().Evaluate(Request(host: host)));

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("127.0.0.1")]
    public void LocalHostNames_AreAccepted(string host) =>
        Assert.Equal(DemoAuthDecision.Available, Gate().Evaluate(Request(host: host)));

    private static DemoAuthGate PublicGate(bool enabled = true, string environment = "Production", string? database = "vnewsdemo", bool allowPublic = true)
    {
        var options = new DemoAuthOptions { Enabled = enabled, AllowPublicHost = allowPublic };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = $"Data Source=sql.example.net;Initial Catalog={database};User ID=u;Password=p;",
        }).Build();
        return new DemoAuthGate(Options.Create(options), new FakeEnvironment(environment), config);
    }

    [Fact]
    public void PublicHost_OptIn_AllowsAnyVisitor_OnADemoDatabase() =>
        Assert.Equal(DemoAuthDecision.Available, PublicGate().Evaluate(Request(remote: "203.0.113.9", host: "vnews-szero.somee.com", ("X-Forwarded-For", "1.2.3.4"))));

    [Fact]
    public void PublicHost_StillNeedsEnabledTrue() =>
        Assert.Equal(DemoAuthDecision.Disabled, PublicGate(enabled: false).Evaluate(Request(remote: "203.0.113.9", host: "vnews-szero.somee.com")));

    [Theory]
    [InlineData("VNewsDb")]
    [InlineData("production")]
    public void PublicHost_NeverOpens_OnANonDemoDatabase(string database) =>
        Assert.Equal(DemoAuthDecision.DatabaseNotAllowed, PublicGate(database: database).Evaluate(Request(remote: "203.0.113.9", host: "x.example")));

    [Fact]
    public void WithoutTheOptIn_ProductionIsStillRefused() =>
        Assert.Equal(DemoAuthDecision.NotDevelopment, PublicGate(allowPublic: false).Evaluate(Request()));

    [Fact]
    public void ExposesTheDatabaseNameItChecked() => Assert.Equal("VNewsDemo", Gate(database: "VNewsDemo").DatabaseName);

    [Fact]
    public void Personas_MapOneToOne_ToTheRealRoles_AndDedicatedAccounts()
    {
        Assert.Equal(Roles.All.OrderBy(r => r), DemoPersonas.All.Select(p => p.Role).OrderBy(r => r));
        Assert.Equal(DemoPersonas.All.Count, DemoPersonas.All.Select(p => p.Key).Distinct().Count());
        Assert.Equal(DemoPersonas.All.Count, DemoPersonas.All.Select(p => p.Email).Distinct().Count());
        Assert.All(DemoPersonas.All, p =>
        {
            Assert.EndsWith(DemoPersonas.EmailDomain, p.Email);
            Assert.False(string.IsNullOrWhiteSpace(p.Title));
            Assert.DoesNotContain("Demo", p.Title); // the card shows the role name
            Assert.Equal("Vào demo", p.ButtonLabel);
            Assert.Equal($"Vào demo {p.Title}", p.AriaLabel);
        });
        Assert.NotNull(DemoPersonas.Find("ADMIN"));
        Assert.Null(DemoPersonas.Find("root"));
        Assert.Null(DemoPersonas.Find(null));
    }
}
