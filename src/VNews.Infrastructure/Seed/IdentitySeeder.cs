using Microsoft.AspNetCore.Identity;
using VNews.Domain.Identity;

namespace VNews.Infrastructure.Seed;

/// <summary>
/// Seeds roles and DEMO-ONLY accounts for portfolio/interview demonstration.
/// Passwords below are intentionally simple and public in source — never reuse for production.
/// </summary>
public static class IdentitySeeder
{
    private const string DemoPassword = "Demo@12345";

    /// <summary>The public demo password; it must never become the password of a real administrator.</summary>
    public static bool IsPublicDemoPassword(string? password) => string.Equals(password, DemoPassword, StringComparison.Ordinal);

    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, bool seedDemoAccounts = false)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (!seedDemoAccounts) return;

        await EnsureUserAsync(userManager, "admin@vnews.local", "Quản trị viên demo", Roles.Admin);
        await EnsureUserAsync(userManager, "editor@vnews.local", "Biên tập viên demo", Roles.Editor);
        await EnsureUserAsync(userManager, "author@vnews.local", "Tác giả demo", Roles.Author);
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string displayName, string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, DemoPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

    public enum BootstrapAdminResult { NotConfigured, AdminAlreadyExists, Created, RejectedPublicPassword, Failed }

    /// <summary>
    /// The only way to get a first administrator outside the demo accounts: the operator supplies the e-mail and a
    /// password through configuration that is NOT in source control (environment variables, user-secrets, a secret store).
    /// It creates one Admin only when none exists yet (so it can never take over or reset an existing system), and it
    /// refuses the public demo password. The password is never logged.
    /// </summary>
    public static async Task<BootstrapAdminResult> EnsureBootstrapAdminAsync(UserManager<ApplicationUser> userManager, string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return BootstrapAdminResult.NotConfigured;
        if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Count > 0) return BootstrapAdminResult.AdminAlreadyExists;
        if (IsPublicDemoPassword(password)) return BootstrapAdminResult.RejectedPublicPassword;

        var user = new ApplicationUser { UserName = email.Trim(), Email = email.Trim(), DisplayName = "Quản trị viên", EmailConfirmed = true };
        if (!(await userManager.CreateAsync(user, password)).Succeeded) return BootstrapAdminResult.Failed;
        if (!(await userManager.AddToRoleAsync(user, Roles.Admin)).Succeeded)
        {
            await userManager.DeleteAsync(user); // never leave a privilege-less half-created account behind
            return BootstrapAdminResult.Failed;
        }
        return BootstrapAdminResult.Created;
    }
}
