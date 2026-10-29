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

    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

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
}
