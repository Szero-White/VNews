using Microsoft.EntityFrameworkCore;
using VNews.Application.DTOs;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public sealed class CommentService : ICommentService
{
    /// <summary>At most this many comments per user inside the window (a simple flood guard; the check runs in the database).</summary>
    private const int RateLimitCount = 5;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);

    private readonly VNewsDbContext _db;
    private readonly IAuditLogService _audit;

    public CommentService(VNewsDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<int?> GetPublishedArticleIdAsync(string slug, CancellationToken cancellationToken = default) =>
        await _db.Articles.AsNoTracking().Where(a => a.Slug == slug && a.Status == ArticleStatus.Published).Select(a => (int?)a.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<int> CountVisibleAsync(int articleId, CancellationToken cancellationToken = default) =>
        await _db.Comments.AsNoTracking().CountAsync(c => c.ArticleId == articleId && c.Status == CommentStatus.Visible, cancellationToken);

    public async Task<CommentThreadDto> GetThreadsAsync(int articleId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var all = _db.Comments.AsNoTracking().Where(c => c.ArticleId == articleId);

        // A top-level comment is listed when it is visible, or when it was removed but still has visible replies (shown as a placeholder).
        var threadsQuery = all.Where(c => c.ParentId == null
            && (c.Status == CommentStatus.Visible || all.Any(r => r.ParentId == c.Id && r.Status == CommentStatus.Visible)));

        var threadCount = await threadsQuery.CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(threadCount / (double)pageSize));
        page = Math.Clamp(page, 1, lastPage);

        var tops = await threadsQuery.OrderByDescending(c => c.CreatedAtUtc).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var topIds = tops.Select(t => t.Id).ToList();
        var replies = await all.Where(c => c.ParentId != null && topIds.Contains(c.ParentId.Value) && c.Status == CommentStatus.Visible)
            .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id).ToListAsync(cancellationToken);

        static CommentDto Map(Comment c, IReadOnlyList<CommentDto>? children = null) => new()
        {
            Id = c.Id,
            ParentId = c.ParentId,
            UserId = c.UserId,
            AuthorName = c.Status == CommentStatus.Visible ? c.AuthorName : string.Empty,
            Body = c.Status == CommentStatus.Visible ? c.Body : string.Empty,
            CreatedAtUtc = c.CreatedAtUtc,
            IsVisible = c.Status == CommentStatus.Visible,
            RemovedReason = c.Status switch
            {
                CommentStatus.Hidden => "Bình luận đã bị gỡ bởi ban biên tập.",
                CommentStatus.Deleted => "Bình luận đã được tác giả xóa.",
                _ => null,
            },
            Replies = children ?? Array.Empty<CommentDto>(),
        };

        var threads = tops.Select(t => Map(t, replies.Where(r => r.ParentId == t.Id).Select(r => Map(r)).ToList())).ToList();
        return new CommentThreadDto
        {
            TotalVisible = await CountVisibleAsync(articleId, cancellationToken),
            ThreadCount = threadCount,
            Page = page,
            PageSize = pageSize,
            Threads = threads,
        };
    }

    public async Task<CommentResult> AddAsync(int articleId, string userId, string authorName, string body, int? parentId, CancellationToken cancellationToken = default)
    {
        var text = Normalize(body);
        if (text.Length < ICommentService.MinLength) return CommentResult.Fail(CommentFailure.EmptyBody, "Vui lòng nhập nội dung bình luận (ít nhất 2 ký tự).");
        if (text.Length > ICommentService.MaxLength) return CommentResult.Fail(CommentFailure.TooLong, $"Bình luận tối đa {ICommentService.MaxLength} ký tự.");

        var published = await _db.Articles.AsNoTracking().AnyAsync(a => a.Id == articleId && a.Status == ArticleStatus.Published, cancellationToken);
        if (!published) return CommentResult.Fail(CommentFailure.ArticleNotFound, "Không tìm thấy bài viết.");

        if (parentId is int pid)
        {
            // Replies are one level deep and only to a live top-level comment of the same article.
            var parentOk = await _db.Comments.AsNoTracking().AnyAsync(c => c.Id == pid && c.ArticleId == articleId && c.ParentId == null && c.Status == CommentStatus.Visible, cancellationToken);
            if (!parentOk) return CommentResult.Fail(CommentFailure.ParentNotFound, "Không thể trả lời bình luận này.");
        }

        var now = DateTime.UtcNow;
        var windowStart = now - RateLimitWindow;
        var recent = await _db.Comments.AsNoTracking().CountAsync(c => c.UserId == userId && c.CreatedAtUtc > windowStart, cancellationToken);
        if (recent >= RateLimitCount) return CommentResult.Fail(CommentFailure.RateLimited, "Bạn bình luận hơi nhanh. Vui lòng thử lại sau ít phút.");

        var duplicateStart = now - DuplicateWindow;
        var duplicate = await _db.Comments.AsNoTracking().AnyAsync(c => c.UserId == userId && c.ArticleId == articleId && c.Body == text && c.CreatedAtUtc > duplicateStart, cancellationToken);
        if (duplicate) return CommentResult.Fail(CommentFailure.Duplicate, "Bạn vừa gửi bình luận giống hệt trên bài này.");

        var name = string.IsNullOrWhiteSpace(authorName) ? "Độc giả" : authorName.Trim();
        var comment = new Comment
        {
            ArticleId = articleId,
            UserId = userId,
            AuthorName = name.Length > 100 ? name[..100] : name,
            ParentId = parentId,
            Body = text,
            Status = CommentStatus.Visible,
            CreatedAtUtc = now,
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);
        return CommentResult.Ok(comment.Id);
    }

    public async Task<CommentResult> HideAsync(int commentId, string moderatorId, string moderatorName, CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null) return CommentResult.Fail(CommentFailure.NotFound, "Không tìm thấy bình luận.");
        if (comment.Status == CommentStatus.Visible)
        {
            comment.Status = CommentStatus.Hidden;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync(moderatorId, moderatorName, "HideComment", "Comment", comment.Id.ToString(), $"Gỡ bình luận của {comment.AuthorName}", cancellationToken);
        }
        return CommentResult.Ok(comment.Id);
    }

    public async Task<CommentResult> DeleteOwnAsync(int commentId, string userId, CancellationToken cancellationToken = default)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null) return CommentResult.Fail(CommentFailure.NotFound, "Không tìm thấy bình luận.");
        if (comment.UserId != userId) return CommentResult.Fail(CommentFailure.Forbidden, "Bạn chỉ có thể xóa bình luận của chính mình.");
        if (comment.Status == CommentStatus.Visible)
        {
            comment.Status = CommentStatus.Deleted;
            await _db.SaveChangesAsync(cancellationToken);
        }
        return CommentResult.Ok(comment.Id);
    }

    public async Task<string?> GetArticleSlugAsync(int commentId, CancellationToken cancellationToken = default) =>
        await _db.Comments.AsNoTracking().Where(c => c.Id == commentId).Select(c => c.Article.Slug).FirstOrDefaultAsync(cancellationToken);

    /// <summary>Plain text: no control characters, line endings unified, surrounding space trimmed, blank-line runs collapsed.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var chars = input.Replace("\r\n", "\n").Replace('\r', '\n').Where(ch => ch == '\n' || ch == '\t' || !char.IsControl(ch)).ToArray();
        var text = new string(chars).Trim();
        while (text.Contains("\n\n\n")) text = text.Replace("\n\n\n", "\n\n");
        return text;
    }
}
