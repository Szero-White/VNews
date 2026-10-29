using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

public interface ICategoryQueryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CategoryDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
