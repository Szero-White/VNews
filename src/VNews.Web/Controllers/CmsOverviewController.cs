using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

/// <summary>System overview for administrators. Every figure is read from the database; nothing here is invented.</summary>
[Authorize(Roles = Roles.Admin)]
public sealed class CmsOverviewController : Controller
{
    private readonly IArticleCommandService _articles;
    private readonly ICategoryQueryService _categories;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VNewsDbContext _db;

    public CmsOverviewController(IArticleCommandService articles, ICategoryQueryService categories, UserManager<ApplicationUser> userManager, VNewsDbContext db)
    {
        _articles = articles;
        _categories = categories;
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var byRole = new Dictionary<string, int>();
        foreach (var role in Roles.All)
        {
            byRole[role] = (await _userManager.GetUsersInRoleAsync(role)).Count;
        }

        var recent = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Take(8)
            .Select(a => new AuditRowViewModel { CreatedAt = a.CreatedAt, UserDisplayName = a.UserDisplayName, Action = a.Action, EntityType = a.EntityType, EntityId = a.EntityId, Detail = a.Detail })
            .ToListAsync(cancellationToken);

        return View(new CmsOverviewViewModel
        {
            Stats = await _articles.GetCmsStatsAsync(null, cancellationToken),
            Categories = (await _categories.GetAllAsync(cancellationToken)).Count,
            Users = await _userManager.Users.CountAsync(cancellationToken),
            UsersByRole = byRole,
            PendingQueue = (await _articles.ListForCmsAsync("PendingReview", null, 1, 5, cancellationToken, oldestFirst: true)).Items,
            RecentAudit = recent,
        });
    }
}
