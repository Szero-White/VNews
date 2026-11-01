using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;

namespace VNews.Web.Controllers;

/// <summary>Read-only activity log for administrators (who did what to which article, when).</summary>
[Authorize(Roles = Roles.Admin)]
public sealed class CmsAuditController : Controller
{
    private const int PageSize = 25;
    private readonly IAuditLogQueryService _audit;

    public CmsAuditController(IAuditLogQueryService audit) => _audit = audit;

    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default) =>
        View(await _audit.GetPageAsync(page, PageSize, cancellationToken));
}
