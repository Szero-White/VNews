namespace VNews.Web.Models.Cms;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
