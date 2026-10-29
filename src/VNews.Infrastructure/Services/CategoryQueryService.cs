using Microsoft.EntityFrameworkCore;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public class CategoryQueryService : ICategoryQueryService
{
    private readonly VNewsDbContext _context;

    public CategoryQueryService(VNewsDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .Where(c => c.Slug == slug)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
