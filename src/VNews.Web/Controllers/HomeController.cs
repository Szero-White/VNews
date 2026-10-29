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

    public IActionResult PageNotFound()
    {
        Response.StatusCode = 404;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
