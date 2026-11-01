using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

public interface ICategoryQueryService
{
    /// <summary>Every category, flat: each parent followed by its own children (for the CMS, selects and the API).</summary>
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Top-level categories in display order, each with its sub-categories (navigation, footer).</summary>
    Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>One category with its parent (when a child) or its children (when a parent).</summary>
    Task<CategoryDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
