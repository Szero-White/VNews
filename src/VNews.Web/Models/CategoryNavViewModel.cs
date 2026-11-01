using VNews.Application.DTOs;

namespace VNews.Web.Models;

public sealed class CategoryNavViewModel
{
    public IReadOnlyList<CategoryDto> Categories { get; init; } = Array.Empty<CategoryDto>();
    public string? ActiveSlug { get; init; }
}
