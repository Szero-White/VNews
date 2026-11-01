namespace VNews.Application.Interfaces;

public interface ICategoryCommandService
{
    /// <summary>Hierarchy rules (checked here, not only in the form): one level deep, a parent must be top-level, a category cannot be its own parent, and a category that has children cannot become a child.</summary>
    Task<int> CreateAsync(string name, string slug, string? description, int displayOrder, int? parentId = null, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, string name, string slug, string? description, int displayOrder, int? parentId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a category only when nothing depends on it: no articles (of any status, trash included) and no sub-categories.
    /// Otherwise throws <see cref="Common.ArticleWorkflowException"/> with the reason, so no article is ever orphaned.
    /// </summary>
    Task DeleteAsync(int id, string actingUserId, string actingUserName, CancellationToken cancellationToken = default);
}
