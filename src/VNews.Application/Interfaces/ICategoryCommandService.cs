using VNews.Application.DTOs;

namespace VNews.Application.Interfaces;

public interface ICategoryCommandService
{
    Task<int> CreateAsync(string name, string slug, string? description, int displayOrder, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, string name, string slug, string? description, int displayOrder, CancellationToken cancellationToken = default);
}
