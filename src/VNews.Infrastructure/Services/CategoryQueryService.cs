using Microsoft.EntityFrameworkCore;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
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
        var tree = await GetTreeAsync(cancellationToken);
        return tree.SelectMany(parent => new[] { parent }.Concat(parent.Children)).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        // One query: the hierarchy is assembled in memory (a few dozen rows at most).
        var rows = await _context.Categories.AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var byParent = rows.Where(c => c.ParentId != null).ToLookup(c => c.ParentId!.Value);
        var parents = rows.Where(c => c.ParentId == null).ToDictionary(c => c.Id);
        return parents.Values
            .Select(p => ToDto(p, null, byParent[p.Id].Select(child => ToDto(child, p, null)).ToList()))
            .ToList();
    }

    public async Task<CategoryDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.AsNoTracking().Include(c => c.Parent).FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
        if (category is null) return null;

        var children = category.ParentId is null
            ? await _context.Categories.AsNoTracking().Where(c => c.ParentId == category.Id).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToListAsync(cancellationToken)
            : new List<Category>();
        return ToDto(category, category.Parent, children.Select(c => ToDto(c, category, null)).ToList());
    }

    private static CategoryDto ToDto(Category c, Category? parent, IReadOnlyList<CategoryDto>? children) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Slug = c.Slug,
        Description = c.Description,
        DisplayOrder = c.DisplayOrder,
        ParentId = c.ParentId,
        ParentName = parent?.Name,
        ParentSlug = parent?.Slug,
        Children = children ?? Array.Empty<CategoryDto>(),
    };
}
