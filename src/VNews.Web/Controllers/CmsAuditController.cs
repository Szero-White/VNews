using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

/// <summary>Read-only activity log for administrators (who did what to which article, when), with filters for when it grows long.</summary>
[Authorize(Roles = Roles.Admin)]
public sealed class CmsAuditController : Controller
{
    private const int PageSize = 25;
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);
    private readonly IAuditLogQueryService _audit;

    public CmsAuditController(IAuditLogQueryService audit) => _audit = audit;

    public async Task<IActionResult> Index([FromQuery(Name = "op")] string? operation, string? entity, // not named "action": MVC would bind the route's own action value ("Index") to it
         string? user, string? q,
        [FromQuery(Name = "from")] DateTime? from, [FromQuery(Name = "to")] DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        if (from > to) (from, to) = (to, from); // days entered the wrong way round still give the range meant

        // The pickers are days in Vietnam time; the log stores UTC. "to" covers the whole last day.
        var filter = new AuditFilter(
            Clean(operation), Clean(entity), Clean(user), Clean(q, 100),
            from is DateTime f ? DateTime.SpecifyKind(f.Date - Vietnam, DateTimeKind.Utc) : null,
            to is DateTime t ? DateTime.SpecifyKind(t.Date.AddDays(1) - Vietnam, DateTimeKind.Utc) : null);

        return View(new AuditPageViewModel
        {
            Page = await _audit.GetPageAsync(filter, page, PageSize, cancellationToken),
            Options = await _audit.GetFilterOptionsAsync(cancellationToken),
            Action = filter.Action, Entity = filter.EntityType, User = filter.User, Query = filter.Search, From = from?.Date, To = to?.Date,
        });
    }

    private static string? Clean(string? value, int max = 80) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
