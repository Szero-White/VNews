namespace VNews.Application.DTOs.Cms;

public sealed class UserListItemDto
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public bool IsLocked { get; init; }
}

public enum SetLockOutcome
{
    Changed,
    UserNotFound,
    CannotLockSelf,
    LastAdministrator,
}

public enum SetRoleOutcome
{
    Changed,
    InvalidRole,
    UserNotFound,
    CannotChangeOwnRole,
    LastAdministrator,
}
