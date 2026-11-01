using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using VNews.Domain.Identity;

namespace VNews.Web.Demo;

/// <summary>One demo persona: the public key used in the form, the real role it maps to, and its dedicated account.</summary>
public sealed record DemoPersona(string Key, string Role, string Email, string DisplayName, string Description, string Title)
{
    /// <summary>Same short text on all four buttons (so none wraps); the aria-label tells them apart.</summary>
    public string ButtonLabel => "Vào demo";

    public string AriaLabel => $"Vào demo {Title}";
}

/// <summary>
/// The fixed, server-side list of demo personas. The browser only ever sends one of these keys: it can never choose
/// an account, a user id or a role. Accounts live under a reserved ".local" domain and are NOT the real users.
/// </summary>
public static class DemoPersonas
{
    public const string ClaimType = "vnews:demo";
    public const string EmailDomain = "@demo.vnews.local";

    public static readonly IReadOnlyList<DemoPersona> All = new[]
    {
        new DemoPersona("reader", Roles.Reader, "demo.reader" + EmailDomain, "Độc giả demo", "Đọc tin, lưu bài viết, quản lý tài khoản.", "Độc giả"),
        new DemoPersona("author", Roles.Author, "demo.author" + EmailDomain, "Tác giả demo", "Viết bài, lưu bản nháp, gửi duyệt.", "Tác giả"),
        new DemoPersona("editor", Roles.Editor, "demo.editor" + EmailDomain, "Biên tập viên demo", "Xem, duyệt hoặc từ chối bài viết theo quyền.", "Biên tập viên"),
        new DemoPersona("admin", Roles.Admin, "demo.admin" + EmailDomain, "Quản trị viên demo", "Quản lý chuyên mục và người dùng trong phạm vi được phép.", "Quản trị viên"),
    };

    public static DemoPersona? Find(string? key) =>
        All.FirstOrDefault(p => string.Equals(p.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Creates (and repairs) the dedicated demo accounts. They get a long random password that is never stored, shown or
/// logged, so nobody can sign in to them with a password: the only way in is the guarded demo endpoint.
/// </summary>
public sealed class DemoAccountService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ILogger<DemoAccountService> _logger;

    public DemoAccountService(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, ILogger<DemoAccountService> logger)
    {
        _users = users;
        _roles = roles;
        _logger = logger;
    }

    public async Task<ApplicationUser?> EnsureAsync(DemoPersona persona)
    {
        if (!await _roles.RoleExistsAsync(persona.Role))
        {
            await _roles.CreateAsync(new IdentityRole(persona.Role));
        }

        var user = await _users.FindByEmailAsync(persona.Email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = persona.Email, Email = persona.Email, DisplayName = persona.DisplayName, EmailConfirmed = true };
            var created = await _users.CreateAsync(user, GenerateUnguessablePassword());
            if (!created.Succeeded)
            {
                _logger.LogError("Could not create demo account {Email}: {Errors}", persona.Email, string.Join("; ", created.Errors.Select(e => e.Code)));
                return null;
            }
            _logger.LogInformation("Created demo account {Email}.", persona.Email);
        }

        // Demo accounts are owned by the server: exactly the persona's role, nothing else, whatever happened before.
        var current = await _users.GetRolesAsync(user);
        var stale = current.Where(r => r != persona.Role).ToArray();
        if (stale.Length > 0) await _users.RemoveFromRolesAsync(user, stale);
        if (!current.Contains(persona.Role)) await _users.AddToRoleAsync(user, persona.Role);

        if (await _users.IsLockedOutAsync(user)) await _users.SetLockoutEndDateAsync(user, null);
        return user;
    }

    /// <summary>Random, policy-compliant (upper, lower, digit, symbol, 40 chars) and discarded right after creation.</summary>
    private static string GenerateUnguessablePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(30);
        return "Aa1!" + Convert.ToBase64String(bytes).Replace('+', 'x').Replace('/', 'y').TrimEnd('=');
    }
}
