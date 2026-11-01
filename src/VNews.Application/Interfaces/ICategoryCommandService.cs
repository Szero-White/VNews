namespace VNews.Application.Interfaces;

public interface ICategoryCommandService
{
    /// <summary>Hierarchy rules (checked here, not only in the form): one level deep, a parent must be top-level, a category cannot be its own parent, and a category that has children cannot become a child.</summary>
    Task<int> CreateAsync(string name, string slug, string? description, int displayOrder, int? parentId = null, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, string name, string slug, string? description, int displayOrder, int? parentId = null, CancellationToken cancellationToken = default);
}
