using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;
using VNews.Web.Models;

namespace VNews.Web.Controllers;

public class HomeController : Controller
{
    private readonly IArticleQueryService _articleQueryService;

    public HomeController(IArticleQueryService articleQueryService)
    {
        _articleQueryService = articleQueryService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
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
