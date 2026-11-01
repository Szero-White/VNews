using VNews.Application.DTOs;

namespace VNews.Web.Models;

public sealed class CategoryNavViewModel
{
    /// <summary>Top-level categories, each with its sub-categories.</summary>
    public IReadOnlyList<CategoryDto> Categories { get; init; } = Array.Empty<CategoryDto>();
    public string? ActiveSlug { get; init; }

    public bool IsActive(CategoryDto c) => string.Equals(c.Slug, ActiveSlug, StringComparison.OrdinalIgnoreCase);

    /// <summary>A parent whose sub-category is the page being read (highlighted as the section, not as the page).</summary>
    public bool IsActiveSection(CategoryDto c) => !IsActive(c) && c.Children.Any(IsActive);
}
