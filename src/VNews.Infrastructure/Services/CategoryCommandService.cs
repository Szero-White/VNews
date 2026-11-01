using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VNews.Application.Common;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public class CategoryCommandService : ICategoryCommandService
{
    private readonly VNewsDbContext _context;
    private readonly ILogger<CategoryCommandService> _logger;
    private readonly IAuditLogService _auditLog;

    public CategoryCommandService(VNewsDbContext context, ILogger<CategoryCommandService> logger, IAuditLogService auditLog)
    {
        _context = context;
        _logger = logger;
        _auditLog = auditLog;
    }

    public async Task DeleteAsync(int id, string actingUserId, string actingUserName, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException($"Không tìm thấy chuyên mục {id}.");

        var articles = await _context.Articles.CountAsync(a => a.CategoryId == id, cancellationToken);
        if (articles > 0)
        {
            throw new ArticleWorkflowException($"Chuyên mục còn {articles} bài viết (kể cả bài trong thùng rác). Hãy chuyển các bài sang chuyên mục khác trước khi xóa.");
        }

        if (await _context.Categories.AnyAsync(c => c.ParentId == id, cancellationToken))
        {
            throw new ArticleWorkflowException("Chuyên mục còn chuyên mục con. Hãy chuyển hoặc xóa các chuyên mục con trước.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(actingUserId, actingUserName, "DeleteCategory", "Category", id.ToString(), category.Name, cancellationToken);
        _logger.LogInformation("Category {CategoryId} deleted by user {UserId}.", id, actingUserId);
    }

    public async Task<int> CreateAsync(string name, string slug, string? description, int displayOrder, int? parentId = null, CancellationToken cancellationToken = default)
    {
        if (await _context.Categories.AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            throw new ArticleWorkflowException($"Slug '{slug}' đã tồn tại.");
        }

        await EnsureValidParentAsync(categoryId: null, parentId, cancellationToken);

        var category = new Category
        {
            Name = name.Trim(),
            Slug = slug.Trim(),
            Description = description,
            DisplayOrder = displayOrder,
            ParentId = parentId,
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Category {CategoryId} ({CategorySlug}) created.", category.Id, category.Slug);
        return category.Id;
    }

    public async Task UpdateAsync(int id, string name, string slug, string? description, int displayOrder, int? parentId = null, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new CategoryNotFoundException(id);

        if (await _context.Categories.AnyAsync(c => c.Slug == slug && c.Id != id, cancellationToken))
        {
            throw new ArticleWorkflowException($"Slug '{slug}' đã được dùng bởi chuyên mục khác.");
        }

        await EnsureValidParentAsync(id, parentId, cancellationToken);

        category.Name = name.Trim();
        category.Slug = slug.Trim();
        category.Description = description;
        category.DisplayOrder = displayOrder;
        category.ParentId = parentId;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Category {CategoryId} ({CategorySlug}) updated.", category.Id, category.Slug);
    }

    /// <summary>One level deep only: the parent exists and is top-level, is not the category itself, and a category that already has children cannot become a child.</summary>
    private async Task EnsureValidParentAsync(int? categoryId, int? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null) return;

        if (parentId == categoryId)
        {
            throw new ArticleWorkflowException("Một chuyên mục không thể là chuyên mục con của chính nó.");
        }

        var parent = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentId, cancellationToken)
            ?? throw new ArticleWorkflowException("Chuyên mục cha không tồn tại.");
        if (parent.ParentId is not null)
        {
            throw new ArticleWorkflowException("Chuyên mục cha phải là chuyên mục cấp cao nhất (chỉ hỗ trợ một cấp con).");
        }

        if (categoryId is int id && await _context.Categories.AnyAsync(c => c.ParentId == id, cancellationToken))
        {
            throw new ArticleWorkflowException("Chuyên mục này đang có chuyên mục con nên không thể trở thành chuyên mục con.");
        }
    }
}
