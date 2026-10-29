using Microsoft.AspNetCore.Mvc;
using VNews.Application.Interfaces;

namespace VNews.Web.Controllers.Api;

[ApiController]
[Route("api/v1/categories")]
public class CategoriesApiController : ControllerBase
{
    private readonly ICategoryQueryService _categoryQueryService;
    private readonly IArticleStoredProcedureService _articleStoredProcedureService;

    public CategoriesApiController(
        ICategoryQueryService categoryQueryService,
        IArticleStoredProcedureService articleStoredProcedureService)
    {
        _categoryQueryService = categoryQueryService;
        _articleStoredProcedureService = articleStoredProcedureService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _categoryQueryService.GetAllAsync(cancellationToken);
        return Ok(categories);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var category = await _categoryQueryService.GetBySlugAsync(slug, cancellationToken);
        if (category is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy chuyên mục", detail: $"Không có chuyên mục với slug '{slug}'.");
        }

        return Ok(category);
    }

    /// <summary>Paginated published articles in a category, fetched via dbo.sp_GetPublishedArticlesByCategory (SQL Server stored procedure).</summary>
    [HttpGet("{slug}/articles")]
    public async Task<IActionResult> GetArticlesByCategoryViaStoredProcedure(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 50)
        {
            return ValidationProblem(detail: "page phải >= 1 và pageSize trong khoảng 1-50.");
        }

        var category = await _categoryQueryService.GetBySlugAsync(slug, cancellationToken);
        if (category is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy chuyên mục", detail: $"Không có chuyên mục với slug '{slug}'.");
        }

        var result = await _articleStoredProcedureService.GetPublishedArticlesByCategoryAsync(slug, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
