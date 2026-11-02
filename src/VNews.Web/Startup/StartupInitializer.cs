using Microsoft.AspNetCore.Identity;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Infrastructure.Seed;

namespace VNews.Web.Startup;

public static class StartupInitializer
{
    /// <summary>
    /// Applies the database schema, seeds roles, seeds sample content ONLY in Development (or when Seed:SampleContent=true) and, for a real
    /// deployment, creates the first administrator when none exists.
    /// NOTE: DbInitializer migrates the database it is pointed at; never run a new build against a database you have not approved a migration for.
    /// </summary>
    public static async Task InitializeAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VNewsDbContext>();
        var seedSampleContent = app.Configuration.GetValue("Seed:SampleContent", app.Environment.IsDevelopment());
        await DbInitializer.SeedAsync(context, seedSampleContent);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await IdentitySeeder.SeedAsync(roleManager, userManager, seedDemoAccounts: app.Environment.IsDevelopment());

        // First administrator for a real deployment: only from secret configuration (Bootstrap:AdminEmail / Bootstrap:AdminPassword,
        // e.g. environment variables or user-secrets), only when no Admin exists, never with the public demo password.
        var bootstrap = await IdentitySeeder.EnsureBootstrapAdminAsync(userManager, app.Configuration["Bootstrap:AdminEmail"], app.Configuration["Bootstrap:AdminPassword"]);
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("VNews.Bootstrap");
        if (bootstrap == IdentitySeeder.BootstrapAdminResult.Created) log.LogWarning("Bootstrap administrator created from configuration. Remove Bootstrap:AdminPassword from configuration now.");
        else if (bootstrap == IdentitySeeder.BootstrapAdminResult.RejectedPublicPassword) log.LogError("Bootstrap administrator refused: the configured password is the public demo password.");
        else if (bootstrap == IdentitySeeder.BootstrapAdminResult.Failed) log.LogError("Bootstrap administrator could not be created (check the e-mail and the password policy).");
    }
}
