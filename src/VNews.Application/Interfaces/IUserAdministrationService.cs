using VNews.Application.Common;
using VNews.Application.DTOs.Cms;

namespace VNews.Application.Interfaces;

/// <summary>Administrator operations on accounts. The safeguards live here, not in the controller, so no entry point can skip them.</summary>
public interface IUserAdministrationService
{
    Task<PagedResult<UserListItemDto>> ListAsync(string? search, string? role, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Accounts per role (every known role is present, possibly with 0).</summary>
    Task<IReadOnlyDictionary<string, int>> CountByRoleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the user exactly one role. Refused when the admin targets themself (an administrator cannot change their own role) and when it would
    /// leave the system without an administrator. A changed role also rotates the security stamp so existing sign-ins stop at once.
    /// </summary>
    /// <summary>
    /// Locks (cannot sign in, existing sessions end) or unlocks an account. Accounts are never deleted. You cannot lock yourself,
    /// and the last active administrator can never be locked.
    /// </summary>
    Task<(SetLockOutcome Outcome, string? Email)> SetLockedAsync(string actingUserId, string actingUserName, string targetUserId, bool locked, CancellationToken cancellationToken = default);

    Task<(SetRoleOutcome Outcome, string? Email)> SetRoleAsync(string actingUserId, string targetUserId, string role, CancellationToken cancellationToken = default);
}
