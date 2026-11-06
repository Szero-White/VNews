using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace VNews.Web.Demo;

/// <summary>
/// "DemoAuth" configuration. Everything defaults to OFF/safe: with no configuration at all the one-click demo
/// sign-in does not exist.
/// </summary>
public sealed class DemoAuthOptions
{
    public const string SectionName = "DemoAuth";

    /// <summary>Master switch. Must be explicitly set to true, and still only works in Development.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The demo sign-in only works against a database whose name starts with one of these prefixes, so it can
    /// never be pointed at the real development database (VNewsDb) by mistake. Case-insensitive.
    /// </summary>
    public string[] AllowedDatabasePrefixes { get; set; } = { "VNewsDemo", "VNewsE2E", "VNewsTests_" };

    /// <summary>
    /// Opt-in for a PUBLIC portfolio site (for example the Somee deployment): the demo sign-in then also works outside Development and for
    /// requests that are not from this machine. Needs Enabled=true AND an allowed demo database name. Default false: nothing changes.
    /// </summary>
    public bool AllowPublicHost { get; set; }
}

public enum DemoAuthDecision
{
    Available,
    NotDevelopment,
    Disabled,
    DatabaseNotAllowed,
    NotLoopback,
    NotLocalHost,
    ForwardedRequest,
}

/// <summary>
/// The single server-side decision "may the demo sign-in run for this request?". Every condition must hold:
/// Development environment, DemoAuth:Enabled=true, an allowed (non-production-like) database, and a request that
/// really comes from this machine (loopback address, localhost host name, no proxy headers). It is checked on every
/// demo request by the server; hiding the buttons is only a convenience on top of it.
/// </summary>
public sealed class DemoAuthGate
{
    private readonly DemoAuthOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly string? _databaseName;

    public DemoAuthGate(IOptions<DemoAuthOptions> options, IHostEnvironment environment, IConfiguration configuration)
    {
        _options = options.Value;
        _environment = environment;
        _databaseName = ReadDatabaseName(configuration.GetConnectionString("DefaultConnection"));
    }

    /// <summary>The database name the application is connected to (for diagnostics and tests).</summary>
    public string? DatabaseName => _databaseName;

    public bool IsAvailable(HttpContext context) => Evaluate(context) == DemoAuthDecision.Available;

    public DemoAuthDecision Evaluate(HttpContext context)
    {
        // Order matters only for the reported reason; every branch denies.
        if (!_environment.IsDevelopment() && !_options.AllowPublicHost) return DemoAuthDecision.NotDevelopment;
        if (!_options.Enabled) return DemoAuthDecision.Disabled;
        if (!IsAllowedDatabase(_databaseName, _options.AllowedDatabasePrefixes)) return DemoAuthDecision.DatabaseNotAllowed;
        if (_options.AllowPublicHost) return DemoAuthDecision.Available; // public demo site: any visitor, but only on a demo-named database

        var remote = context.Connection.RemoteIpAddress;
        if (remote is null || !IPAddress.IsLoopback(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote)) return DemoAuthDecision.NotLoopback;

        // A reverse proxy on the same machine would make every client look like loopback; its headers give it away.
        if (context.Request.Headers.ContainsKey("X-Forwarded-For") || context.Request.Headers.ContainsKey("Forwarded")
            || context.Request.Headers.ContainsKey("X-Forwarded-Host")) return DemoAuthDecision.ForwardedRequest;

        var host = context.Request.Host.Host;
        if (!(string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "[::1]" || host == "::1"))
            return DemoAuthDecision.NotLocalHost;

        return DemoAuthDecision.Available;
    }

    public static bool IsAllowedDatabase(string? databaseName, IEnumerable<string> allowedPrefixes) =>
        !string.IsNullOrWhiteSpace(databaseName)
        && allowedPrefixes.Any(prefix => !string.IsNullOrWhiteSpace(prefix) && databaseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string? ReadDatabaseName(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try { return new SqlConnectionStringBuilder(connectionString).InitialCatalog; }
        catch (ArgumentException) { return null; }
    }
}
