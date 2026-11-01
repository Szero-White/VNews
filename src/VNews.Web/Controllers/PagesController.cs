using Microsoft.AspNetCore.Mvc;

namespace VNews.Web.Controllers;

/// <summary>Static informational pages linked from the site footer.</summary>
public class PagesController : Controller
{
    [HttpGet("gioi-thieu")]
    public IActionResult About() => View();

    [HttpGet("dieu-khoan-su-dung")]
    public IActionResult Terms() => View();

    [HttpGet("chinh-sach-bao-mat")]
    public IActionResult Privacy() => View();
}
