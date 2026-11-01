using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VNews.Application.Interfaces;
using VNews.Infrastructure.Services;

namespace VNews.Tests.Support;

/// <summary>
/// Each test fixture owns a unique, disposable LocalDB database.
/// NEVER use or mutate the developer's VNewsDb database from tests.
/// Also exposes the captured application log and a switch that makes the audit-log write fail.
/// </summary>
public class IsolatedDbWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "VNewsTests_" + Guid.NewGuid().ToString("N");

    public LogSink Logs { get; } = new();
    public AuditFailureSwitch AuditFailure { get; } = new();

    protected virtual string EnvironmentName => "Development";

    // Default: a high ceiling so the many sign-ins made by unrelated tests (all from one fake IP)
    // do not trip the limiter. RateLimitedWebApplicationFactory overrides this with the real, tight limit.
    protected virtual IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration => new[]
    {
        new KeyValuePair<string, string?>("RateLimiting:Auth:PermitLimit", "1000"),
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        // UseSetting feeds the host configuration that WebApplication.CreateBuilder reads BEFORE
        // Program.cs runs. ConfigureAppConfiguration is applied too late for values Program/
        // AddInfrastructure read eagerly (connection string, rate-limit options, environment),
        // which silently pointed the "isolated" tests at the developer's VNewsDb.
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
        foreach (var pair in ExtraConfiguration) builder.UseSetting(pair.Key, pair.Value);

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureServices(services =>
        {
            services.AddTransient<Microsoft.AspNetCore.Hosting.IStartupFilter, TestRemoteAddressStartupFilter>();
            services.AddSingleton(AuditFailure);
            services.AddScoped<AuditLogService>();
            services.AddScoped<IAuditLogService, SwitchableAuditLogService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        // The name is generated here (never provided by a user or read from configuration).
        // This cleanup cannot address VNewsDb or any other existing developer database.
        using var conn = new SqlConnection("Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True");
        conn.Open();
        using var command = conn.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN " +
                              $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                              $"DROP DATABASE [{_databaseName}]; END";
        command.ExecuteNonQuery();
    }
}

/// <summary>Same app, but with a tight sign-in rate limit (3 POSTs per minute).</summary>
public sealed class RateLimitedWebApplicationFactory : IsolatedDbWebApplicationFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration => new[]
    {
        new KeyValuePair<string, string?>("RateLimiting:Auth:PermitLimit", "3"),
        new KeyValuePair<string, string?>("RateLimiting:Auth:WindowSeconds", "60"),
    };
}

/// <summary>Runs the app as Production to verify production-only hardening (Secure cookies, error page).</summary>
public sealed class ProductionWebApplicationFactory : IsolatedDbWebApplicationFactory
{
    protected override string EnvironmentName => "Production";
}

/// <summary>The one-click demo sign-in switched ON (Development, DemoAuth:Enabled=true, isolated test database).</summary>
public class DemoAuthWebApplicationFactory : IsolatedDbWebApplicationFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraConfiguration =>
        base.ExtraConfiguration.Append(new KeyValuePair<string, string?>("DemoAuth:Enabled", "true"));
}

/// <summary>Production with DemoAuth:Enabled=true on purpose: the environment gate alone must refuse it.</summary>
public sealed class DemoAuthProductionWebApplicationFactory : DemoAuthWebApplicationFactory
{
    protected override string EnvironmentName => "Production";
}
