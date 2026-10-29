using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public class CategoryCommandService : ICategoryCommandService
{
    private readonly VNewsDbContext _context;

    public CategoryCommandService(VNewsDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateAsync(string name, string slug, string? description, int displayOrder, CancellationToken cancellationToken = default)
    {
        if (await _context.Categories.AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            throw new ArticleWorkflowException($"Slug '{slug}' đã tồn tại.");
        }

        var category = new Category
        {
            Name = name.Trim(),
            Slug = slug.Trim(),
            Description = description,
            DisplayOrder = displayOrder
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task UpdateAsync(int id, string name, string slug, string? description, int displayOrder, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ArticleNotFoundException(id);

        if (await _context.Categories.AnyAsync(c => c.Slug == slug && c.Id != id, cancellationToken))
        {
            throw new ArticleWorkflowException($"Slug '{slug}' đã được dùng bởi chuyên mục khác.");
        }

        category.Name = name.Trim();
        category.Slug = slug.Trim();
        category.Description = description;
        category.DisplayOrder = displayOrder;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
