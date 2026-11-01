using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VNews.Domain.Identity;

namespace VNews.Tests.Support;

/// <summary>Helpers shared by the integration tests that need throw-away accounts or want to read a page as text.</summary>
public static class TestUsers
{
    public const string Password = "TestPassword123!";

    /// <summary>Creates an account with one role directly through Identity (no sign-up page involved) in the factory's isolated database.</summary>
    public static async Task<(string Email, string Id)> CreateUserAsync(this WebApplicationFactory<Program> factory, string role, string displayName = "Người dùng thử")
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = displayName, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await users.AddToRoleAsync(user, role);
        return (email, user.Id);
    }

    /// <summary>A new Reader, already signed in.</summary>
    public static async Task<HttpClient> LoginNewReaderAsync(this WebApplicationFactory<Program> factory, string displayName = "Bạn đọc thử")
    {
        var user = await factory.CreateUserAsync(Roles.Reader, displayName);
        return await factory.LoginCmsAsync(user.Email, Password);
    }

    /// <summary>The page as text: HTML entities decoded, so assertions can use the Vietnamese text as typed.</summary>
    public static async Task<string> GetDecodedAsync(this HttpClient client, string path) => WebUtility.HtmlDecode(await client.GetStringAsync(path));
}
