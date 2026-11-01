using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VNews.Application.Common;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public sealed class UserAdministrationService : IUserAdministrationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VNewsDbContext _context;
    private readonly ILogger<UserAdministrationService> _logger;

    public UserAdministrationService(UserManager<ApplicationUser> userManager, VNewsDbContext context, ILogger<UserAdministrationService> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<UserListItemDto>> ListAsync(string? search, string? role, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Email!.Contains(search) || u.DisplayName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(role) && Roles.All.Contains(role))
        {
            // One subquery instead of loading every member of the role into memory.
            query = query.Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id && _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)));
        }

        var total = await query.CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, lastPage);

        var users = await query.OrderBy(u => u.Email).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToListAsync(cancellationToken);

        // The roles of the whole page in ONE query (not one query per user).
        var ids = users.Select(u => u.Id).ToList();
        var roleRows = await (from ur in _context.UserRoles.AsNoTracking()
                              join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                              where ids.Contains(ur.UserId)
                              select new { ur.UserId, RoleName = r.Name! })
            .ToListAsync(cancellationToken);
        var rolesByUser = roleRows.ToLookup(x => x.UserId, x => x.RoleName);

        var items = users.Select(u => new UserListItemDto
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            DisplayName = u.DisplayName,
            Roles = rolesByUser[u.Id].ToList(),
        }).ToList();
        return new PagedResult<UserListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default) => await _userManager.Users.CountAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, int>> CountByRoleAsync(CancellationToken cancellationToken = default)
    {
        var counts = await (from ur in _context.UserRoles.AsNoTracking()
                            join r in _context.Roles.AsNoTracking() on ur.RoleId equals r.Id
                            group ur by r.Name into g
                            select new { Role = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return Roles.All.ToDictionary(r => r, r => counts.FirstOrDefault(c => c.Role == r)?.Count ?? 0);
    }

    public async Task<(SetRoleOutcome Outcome, string? Email)> SetRoleAsync(string actingUserId, string targetUserId, string role, CancellationToken cancellationToken = default)
    {
        if (!Roles.All.Contains(role)) return (SetRoleOutcome.InvalidRole, null);

        var user = await _userManager.FindByIdAsync(targetUserId);
        if (user is null) return (SetRoleOutcome.UserNotFound, null);

        // An administrator cannot change their own role, and the last administrator can never be demoted: the system can always be administered.
        if (user.Id == actingUserId) return (SetRoleOutcome.CannotChangeOwnRole, user.Email);

        var current = await _userManager.GetRolesAsync(user);
        if (current.Contains(Roles.Admin) && role != Roles.Admin && (await _userManager.GetUsersInRoleAsync(Roles.Admin)).Count <= 1)
        {
            return (SetRoleOutcome.LastAdministrator, user.Email);
        }

        await _userManager.RemoveFromRolesAsync(user, current);
        await _userManager.AddToRoleAsync(user, role);
        await _userManager.UpdateSecurityStampAsync(user); // signs the user's existing cookies out so the old role cannot linger
        _logger.LogInformation("Admin {AdminId} changed roles of user {TargetUserId} from [{OldRoles}] to {NewRole}.", actingUserId, user.Id, string.Join(",", current), role);
        return (SetRoleOutcome.Changed, user.Email);
    }
}
