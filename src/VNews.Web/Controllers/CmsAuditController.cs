using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Domain.Identity;
using VNews.Infrastructure.Persistence;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

/// <summary>Read-only activity log for administrators (who did what to which article, when).</summary>
[Authorize(Roles = Roles.Admin)]
public sealed class CmsAuditController : Controller
{
    private const int PageSize = 25;
    private readonly VNewsDbContext _db;

    public CmsAuditController(VNewsDbContext db) => _db = db;

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        var total = await _db.AuditLogs.CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(page, lastPage);

        var rows = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(a => new AuditRowViewModel { CreatedAt = a.CreatedAt, UserDisplayName = a.UserDisplayName, Action = a.Action, EntityType = a.EntityType, EntityId = a.EntityId, Detail = a.Detail })
            .ToListAsync(cancellationToken);

        return View(new PagedResult<AuditRowViewModel> { Items = rows, Page = page, PageSize = PageSize, TotalCount = total });
    }
}
