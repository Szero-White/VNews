using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.Controllers.Api;

[ApiController]
[Route("api/v1/stats")]
public class StatsApiController : ControllerBase
{
    private readonly IArticleStoredProcedureService _articleStoredProcedureService;

    public StatsApiController(IArticleStoredProcedureService articleStoredProcedureService)
    {
        _articleStoredProcedureService = articleStoredProcedureService;
    }

    /// <summary>Daily published-article counts for a date range, fetched via dbo.sp_GetDailyPublishStats (SQL Server stored procedure).</summary>
    [HttpGet("daily-publish")]
    public async Task<IActionResult> GetDailyPublishStats(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? toDate.AddDays(-30);

        if (fromDate > toDate)
        {
            return ValidationProblem(detail: "'from' phải nhỏ hơn hoặc bằng 'to'.");
        }

        var stats = await _articleStoredProcedureService.GetDailyPublishStatsAsync(fromDate, toDate, cancellationToken);
        return Ok(stats);
    }
}
