using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.Controllers.Api;

[ApiController]
[Route("api/v1/articles")]
public class ArticlesApiController : ControllerBase
{
    private readonly IArticleQueryService _articleQueryService;

    public ArticlesApiController(IArticleQueryService articleQueryService)
    {
        _articleQueryService = articleQueryService;
    }

    /// <summary>Lists published articles, optionally filtered by category slug, ordered by newest first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 50)
        {
            return ValidationProblem(detail: "page phải >= 1 và pageSize trong khoảng 1-50.");
        }

        var result = string.IsNullOrWhiteSpace(category)
            ? await _articleQueryService.GetLatestAsync(page, pageSize, cancellationToken)
            : await _articleQueryService.GetByCategoryAsync(category, page, pageSize, cancellationToken);

        return Ok(result);
    }

    /// <summary>Gets one published article by slug. Never returns unpublished content.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return ValidationProblem(detail: "slug không được để trống.");
        }

        var article = await _articleQueryService.GetBySlugAsync(slug, cancellationToken);
        if (article is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy bài viết", detail: $"Không có bài viết đã xuất bản với slug '{slug}'.");
        }

        return Ok(article);
    }
}
