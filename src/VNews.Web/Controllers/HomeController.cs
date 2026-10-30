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

    public HomeController(IArticleQueryService articleQueryService, IWebHostEnvironment environment)
    {
        _articleQueryService = articleQueryService;
        _environment = environment;
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
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
