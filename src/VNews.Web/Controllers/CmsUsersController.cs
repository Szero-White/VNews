using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Domain.Identity;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class CmsUsersController : Controller
{
    private const int PageSize = 15;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CmsUsersController> _logger;

    public CmsUsersController(UserManager<ApplicationUser> userManager, ILogger<CmsUsersController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? q, string? role, int page = 1, CancellationToken cancellationToken = default)
    {
        role = Roles.All.Contains(role ?? string.Empty) ? role : null;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim()[..Math.Min(q.Trim().Length, 100)];
        page = page < 1 ? 1 : page;

        var query = _userManager.Users.AsNoTracking().AsQueryable();
        if (q is not null)
        {
            query = query.Where(u => u.Email!.Contains(q) || u.DisplayName.Contains(q));
        }
        if (role is not null)
        {
            var inRole = (await _userManager.GetUsersInRoleAsync(role)).Select(u => u.Id).ToList();
            query = query.Where(u => inRole.Contains(u.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, lastPage);
        var users = await query.OrderBy(u => u.Email).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);

        var items = new List<UserListItemViewModel>();
        foreach (var user in users)
        {
            items.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                Roles = (await _userManager.GetRolesAsync(user)).ToList()
            });
        }

        var byRole = new Dictionary<string, int>();
        foreach (var r in Roles.All) byRole[r] = (await _userManager.GetUsersInRoleAsync(r)).Count;

        return View(new UsersPageViewModel
        {
            Users = new PagedResult<UserListItemViewModel> { Items = items, Page = page, PageSize = PageSize, TotalCount = total },
            Query = q,
            Role = role,
            CurrentUserId = _userManager.GetUserId(User) ?? string.Empty,
            UsersByRole = byRole,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string userId, string role)
    {
        if (!Roles.All.Contains(role))
        {
            TempData["Error"] = "Vai trò không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        // Safeguards: an administrator cannot change their own role, and the last administrator can never be demoted,
        // so the system can always be administered.
        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["Error"] = "Bạn không thể tự thay đổi vai trò của chính mình.";
            return RedirectToAction(nameof(Index));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Contains(Roles.Admin) && role != Roles.Admin && (await _userManager.GetUsersInRoleAsync(Roles.Admin)).Count <= 1)
        {
            TempData["Error"] = "Hệ thống phải còn ít nhất một quản trị viên.";
            return RedirectToAction(nameof(Index));
        }

        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);
        await _userManager.UpdateSecurityStampAsync(user); // signs the user's existing cookies out so the old role cannot linger
        _logger.LogInformation("Admin {AdminId} changed roles of user {TargetUserId} from [{OldRoles}] to {NewRole}.", _userManager.GetUserId(User), user.Id, string.Join(",", currentRoles), role);
        TempData["Message"] = $"Đã gán vai trò {VNews.Web.Helpers.AppFormat.RoleLabel(role)} cho {user.Email}.";
        return RedirectToAction(nameof(Index));
    }
}
