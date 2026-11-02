using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VNews.Application.Common;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public class ArticleTrashService : IArticleTrashService
{
    private readonly VNewsDbContext _context;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<ArticleTrashService> _logger;

    public ArticleTrashService(VNewsDbContext context, IAuditLogService auditLog, ILogger<ArticleTrashService> logger)
    {
        _context = context;
        _auditLog = auditLog;
        _logger = logger;
    }

    public Task MoveToTrashAsync(int articleId, byte[]? rowVersion, string actingUserId, string actingUserName, bool canManageAll, CancellationToken cancellationToken = default)
        => ChangeAsync(articleId, rowVersion, "Trash", cancellationToken, article =>
        {
            if (article.Status == ArticleStatus.Trashed)
            {
                throw new ArticleWorkflowException("Bài viết đã nằm trong thùng rác.");
            }

            // An author may only discard what has not been handed over for review or published.
            var ownDraft = article.AuthorUserId == actingUserId && article.Status is ArticleStatus.Draft or ArticleStatus.Rejected;
            Require(canManageAll || ownDraft, "Bạn chỉ có thể xóa bài nháp của mình. Bài đã gửi duyệt hoặc đã xuất bản do biên tập viên xử lý.");

            article.StatusBeforeDeletion = article.Status;
            article.Status = ArticleStatus.Trashed;
            article.DeletedAt = DateTime.UtcNow;
            article.DeletedByUserId = actingUserId;
        }, actingUserId, actingUserName);

    public Task RestoreAsync(int articleId, byte[]? rowVersion, string actingUserId, string actingUserName, bool canManageAll, CancellationToken cancellationToken = default)
        => ChangeAsync(articleId, rowVersion, "Restore", cancellationToken, article =>
        {
            if (article.Status != ArticleStatus.Trashed)
            {
                throw new ArticleWorkflowException("Bài viết không nằm trong thùng rác.");
            }

            Require(canManageAll || article.AuthorUserId == actingUserId, "Bạn chỉ có thể khôi phục bài viết của chính mình.");

            // Back to where it was; a Published article keeps its slug and PublishedAt, so its public address is unchanged.
            article.Status = article.StatusBeforeDeletion ?? ArticleStatus.Draft;
            article.StatusBeforeDeletion = null;
            article.DeletedAt = null;
            article.DeletedByUserId = null;
        }, actingUserId, actingUserName);

    public async Task PurgeAsync(int articleId, string actingUserId, string actingUserName, bool isAdmin, CancellationToken cancellationToken = default)
    {
        Require(isAdmin, "Chỉ quản trị viên mới được xóa vĩnh viễn bài viết.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var article = await _context.Articles.AsNoTracking().Where(a => a.Id == articleId).Select(a => new { a.Id, a.Title, a.Status }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArticleNotFoundException(articleId);
        if (article.Status != ArticleStatus.Trashed)
        {
            throw new ArticleWorkflowException("Chỉ bài viết trong thùng rác mới được xóa vĩnh viễn.");
        }

        // Replies first: a comment's parent link is restrictive, so the rows that point at others must go before the ones they point at.
        await _context.Comments.Where(c => c.ArticleId == articleId && c.ParentId != null).ExecuteDeleteAsync(cancellationToken);
        await _context.Comments.Where(c => c.ArticleId == articleId).ExecuteDeleteAsync(cancellationToken);
        // Tags links, gallery, saved marks and view records follow by cascade.
        await _context.Articles.Where(a => a.Id == articleId).ExecuteDeleteAsync(cancellationToken);
        await _auditLog.LogAsync(actingUserId, actingUserName, "Purge", "Article", articleId.ToString(), article.Title, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Article {ArticleId}: Purge by user {UserId}.", articleId, actingUserId);
    }

    private static void Require(bool allowed, string message)
    {
        if (!allowed)
        {
            throw new ArticleForbiddenException(message);
        }
    }

    /// <summary>Loads the article, applies the change and writes the audit entry in one transaction.</summary>
    private async Task ChangeAsync(int articleId, byte[]? rowVersion, string action, CancellationToken cancellationToken, Action<Article> apply, string actingUserId, string actingUserName)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken)
                ?? throw new ArticleNotFoundException(articleId);
            if (rowVersion is { Length: > 0 })
            {
                _context.Entry(article).Property(a => a.RowVersion).OriginalValue = rowVersion;
            }

            apply(article);
            article.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditLog.LogAsync(actingUserId, actingUserName, action, "Article", article.Id.ToString(), article.Title, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during {Action} on article {ArticleId}.", action, articleId);
            throw new ArticleConcurrencyException();
        }

        _logger.LogInformation("Article {ArticleId}: {Action} by user {UserId}.", articleId, action, actingUserId);
    }
}
