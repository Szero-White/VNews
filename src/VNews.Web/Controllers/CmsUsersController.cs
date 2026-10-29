using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VNews.Domain.Identity;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class CmsUsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CmsUsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.ToList();
        var items = new List<UserListItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                Roles = roles.ToList()
            });
        }

        ViewBag.AllRoles = Roles.All;
        return View(items);
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

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);

        TempData["Message"] = $"Đã gán vai trò {role} cho {user.Email}.";
        return RedirectToAction(nameof(Index));
    }
}
