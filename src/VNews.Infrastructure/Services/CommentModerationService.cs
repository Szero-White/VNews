using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.DTOs;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public sealed class CommentModerationService : ICommentModerationService
{
    private readonly VNewsDbContext _db;
    private readonly IAuditLogService _audit;

    public CommentModerationService(VNewsDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<CommentAdminItemDto>> ListAsync(string? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.Comments.AsNoTracking().AsQueryable();
        if (Enum.TryParse<CommentStatus>(status, true, out var parsed))
        {
            query = query.Where(c => c.Status == parsed);
        }

        foreach (var token in SearchText.Tokenize(search))
        {
            var pattern = $"%{token}%";
            query = query.Where(c => EF.Functions.Like(c.Body, pattern) || EF.Functions.Like(c.AuthorName, pattern) || EF.Functions.Like(c.Article.Title, pattern));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CommentAdminItemDto
            {
                Id = c.Id,
                ArticleId = c.ArticleId,
                ArticleTitle = c.Article.Title,
                ArticleSlug = c.Article.Slug,
                ArticleIsPublic = c.Article.Status == ArticleStatus.Published,
                AuthorName = c.AuthorName,
                Body = c.Body,
                IsReply = c.ParentId != null,
                Status = c.Status.ToString(),
                CreatedAtUtc = c.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CommentAdminItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<CommentResult> RestoreAsync(int commentId, string moderatorId, string moderatorName, CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null) return CommentResult.Fail(CommentFailure.NotFound, "Không tìm thấy bình luận.");
        if (comment.Status == CommentStatus.Deleted) return CommentResult.Fail(CommentFailure.Forbidden, "Bình luận do chính tác giả xóa nên không thể khôi phục.");

        if (comment.Status == CommentStatus.Hidden)
        {
            comment.Status = CommentStatus.Visible;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(moderatorId, moderatorName, "RestoreComment", "Comment", comment.Id.ToString(), $"Khôi phục bình luận của {comment.AuthorName}", cancellationToken);
        }

        return CommentResult.Ok(comment.Id);
    }
}
