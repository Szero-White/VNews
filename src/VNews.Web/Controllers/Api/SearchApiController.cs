using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.Controllers.Api;

[ApiController]
[Route("api/v1/search")]
public class SearchApiController : ControllerBase
{
    private readonly IArticleQueryService _articleQueryService;

    public SearchApiController(IArticleQueryService articleQueryService)
    {
        _articleQueryService = articleQueryService;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return ValidationProblem(detail: "Tham số q (từ khoá tìm kiếm) không được để trống.");
        }

        if (page < 1 || pageSize < 1 || pageSize > 50)
        {
            return ValidationProblem(detail: "page phải >= 1 và pageSize trong khoảng 1-50.");
        }

        var result = await _articleQueryService.SearchAsync(q, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
