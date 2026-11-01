namespace VNews.Application.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }

    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string? ParentSlug { get; set; }

    /// <summary>Sub-categories (only filled for a top-level category).</summary>
    public IReadOnlyList<CategoryDto> Children { get; set; } = Array.Empty<CategoryDto>();

    public bool IsChild => ParentId is not null;

    /// <summary>"Parent › Child" for a sub-category, the plain name otherwise (used where categories are listed flat, e.g. selects).</summary>
    public string FullName => ParentName is null ? Name : $"{ParentName} › {Name}";
}
