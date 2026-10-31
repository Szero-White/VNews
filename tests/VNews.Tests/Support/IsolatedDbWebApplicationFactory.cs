using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace VNews.Tests.Support;

/// <summary>
/// Each test fixture owns a unique, disposable LocalDB database.
/// NEVER use or mutate the developer's VNewsDb database from tests.
/// </summary>
public sealed class IsolatedDbWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "VNewsTests_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
            });
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
