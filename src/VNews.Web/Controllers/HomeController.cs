using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Web.Models;
using VNews.Web.Testing;

namespace VNews.Web.Controllers;

public class HomeController : Controller
{
    private readonly IArticleQueryService _articleQueryService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IArticleQueryService articleQueryService, IWebHostEnvironment environment, ILogger<HomeController> logger)
    {
        _articleQueryService = articleQueryService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Visual-regression fixture: fixed titles/images/counts for pixel-comparable Playwright
        // captures. Gated to Development + explicit query flag; never used for real traffic and
        // never reads/writes SQL Server.
        if (_environment.IsDevelopment() && Request.Query["visualFixture"] == "1")
        {
            return View(VisualFixtureHomePageData.Build());
        }

        var data = await _articleQueryService.GetHomePageDataAsync(cancellationToken);
        return View(data);
    }

    [HttpGet("da-luu")]
    public IActionResult Saved() => View();

    [HttpGet("tin-moi-nhat")]
    public async Task<IActionResult> Latest(int page = 1, CancellationToken cancellationToken = default)
    {
        var articles = await _articleQueryService.GetLatestAsync(page, 12, cancellationToken);
        return View(articles);
    }

    public IActionResult PageNotFound(int? statusCode)
    {
        Response.StatusCode = statusCode is >= 400 and <= 599 ? statusCode.Value : 404;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        if (feature?.Error is { } exception)
        {
            var (status, _) = VNews.Web.Middleware.ApiExceptionHandler.Map(exception);
            Response.StatusCode = status;
            if (status >= 500)
            {
                _logger.LogError(exception, "Unhandled exception on {Path}. TraceId {TraceId}.", feature.Path, traceId);
            }
            else
            {
                _logger.LogWarning("Handled {ExceptionType} on {Path} -> {Status}. TraceId {TraceId}.", exception.GetType().Name, feature.Path, status, traceId);
            }
        }

        return View(new ErrorViewModel { RequestId = traceId });
    }
}
