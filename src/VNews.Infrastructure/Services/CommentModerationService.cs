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
    // Accent- and case-insensitive: the keyword is folded the same way (SearchText), and đ is folded by hand because the collation does not.
    private const string SearchCollation = "Latin1_General_100_CI_AI";

    private readonly VNewsDbContext _db;
    private readonly IAuditLogService _audit;

    public CommentModerationService(VNewsDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<CommentAdminItemDto>> ListAsync(string? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default,
        DateTime? fromUtc = null, DateTime? toUtcExclusive = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _db.Comments.AsNoTracking().AsQueryable();
        if (Enum.TryParse<CommentStatus>(status, true, out var parsed))
        {
            query = query.Where(c => c.Status == parsed);
        }

        if (fromUtc is DateTime from) query = query.Where(c => c.CreatedAtUtc >= from);
        if (toUtcExclusive is DateTime to) query = query.Where(c => c.CreatedAtUtc < to);

        foreach (var token in SearchText.Tokenize(search))
        {
            var pattern = $"%{token}%";
            query = query.Where(c => EF.Functions.Like(EF.Functions.Collate(c.Body, SearchCollation).Replace("đ", "d"), pattern)
                || EF.Functions.Like(EF.Functions.Collate(c.AuthorName, SearchCollation).Replace("đ", "d"), pattern)
                || EF.Functions.Like(EF.Functions.Collate(c.Article.Title, SearchCollation).Replace("đ", "d"), pattern));
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

    public async Task<IReadOnlyDictionary<string, int>> CountsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Comments.AsNoTracking().GroupBy(c => c.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        int Of(CommentStatus status) => rows.FirstOrDefault(r => r.Status == status)?.Count ?? 0;
        return new Dictionary<string, int>
        {
            ["Visible"] = Of(CommentStatus.Visible), ["Hidden"] = Of(CommentStatus.Hidden), ["Deleted"] = Of(CommentStatus.Deleted),
        };
    }

    public async Task<IReadOnlyDictionary<int, string>> StatesAsync(IReadOnlyCollection<int> commentIds, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Comments.AsNoTracking().Where(c => commentIds.Contains(c.Id)).Select(c => new { c.Id, c.Status }).ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Id, r => r.Status.ToString());
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
