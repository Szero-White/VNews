using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Domain.Identity;
using VNews.Web.Models.Cms;

namespace VNews.Web.Controllers;

/// <summary>Traffic and content statistics for editors and administrators. Every figure is counted from real readings in the database.</summary>
[Authorize(Roles = Roles.Admin + "," + Roles.Editor)]
public sealed class CmsAnalyticsController : Controller
{
    private static readonly int[] Ranges = { 7, 30, 90 };
    private readonly IAnalyticsService _analytics;

    public CmsAnalyticsController(IAnalyticsService analytics) => _analytics = analytics;

    [HttpGet]
    public async Task<IActionResult> Index(int days = 30, CancellationToken cancellationToken = default)
    {
        if (!Ranges.Contains(days)) days = 30;
        return View(new AnalyticsPageViewModel { Report = await _analytics.GetReportAsync(days, cancellationToken), Ranges = Ranges });
    }
}
