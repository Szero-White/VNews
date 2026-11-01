using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

/// <summary>System overview for administrators. Every figure is read from the database; nothing here is invented.</summary>
[Authorize(Roles = Roles.Admin)]
public sealed class CmsOverviewController : Controller
{
    private readonly IArticleCmsQueryService _articles;
    private readonly ICategoryQueryService _categories;
    private readonly IUserAdministrationService _users;
    private readonly IAuditLogQueryService _audit;

    public CmsOverviewController(IArticleCmsQueryService articles, ICategoryQueryService categories, IUserAdministrationService users, IAuditLogQueryService audit)
    {
        _articles = articles;
        _categories = categories;
        _users = users;
        _audit = audit;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new CmsOverviewViewModel
        {
            Stats = await _articles.GetCmsStatsAsync(null, cancellationToken),
            Categories = (await _categories.GetAllAsync(cancellationToken)).Count,
            Users = await _users.CountAsync(cancellationToken),
            UsersByRole = await _users.CountByRoleAsync(cancellationToken),
            PendingQueue = (await _articles.ListForCmsAsync("PendingReview", null, 1, 5, cancellationToken, oldestFirst: true)).Items,
            RecentAudit = await _audit.GetRecentAsync(8, cancellationToken),
        });
}
