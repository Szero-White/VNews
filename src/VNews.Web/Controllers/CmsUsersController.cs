using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Web.Helpers;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

/// <summary>Administrators manage accounts' roles. The rules (own role, last administrator) are enforced by the service, not here.</summary>
[Authorize(Roles = Roles.Admin)]
public class CmsUsersController : Controller
{
    private const int PageSize = 15;
    private readonly IUserAdministrationService _users;

    public CmsUsersController(IUserAdministrationService users) => _users = users;

    public async Task<IActionResult> Index(string? q, string? role, int page = 1, CancellationToken cancellationToken = default)
    {
        role = Roles.All.Contains(role ?? string.Empty) ? role : null;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim()[..Math.Min(q.Trim().Length, 100)];

        return View(new UsersPageViewModel
        {
            Users = await _users.ListAsync(q, role, page, PageSize, cancellationToken),
            Query = q,
            Role = role,
            CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            UsersByRole = await _users.CountByRoleAsync(cancellationToken),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(string userId, string role, CancellationToken cancellationToken)
    {
        var (outcome, email) = await _users.SetRoleAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, userId, role, cancellationToken);
        if (outcome == SetRoleOutcome.UserNotFound) return NotFound();

        TempData[outcome == SetRoleOutcome.Changed ? "Message" : "Error"] = outcome switch
        {
            SetRoleOutcome.Changed => $"Đã gán vai trò {AppFormat.RoleLabel(role)} cho {email}.",
            SetRoleOutcome.CannotChangeOwnRole => "Bạn không thể tự thay đổi vai trò của chính mình.",
            SetRoleOutcome.LastAdministrator => "Hệ thống phải còn ít nhất một quản trị viên.",
            _ => "Vai trò không hợp lệ.",
        };
        return RedirectToAction(nameof(Index));
    }
}
