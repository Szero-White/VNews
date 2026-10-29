using Microsoft.AspNetCore.Identity;

namespace VNews.Domain.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
