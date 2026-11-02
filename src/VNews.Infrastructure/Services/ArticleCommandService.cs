using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VNews.Application.Common;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Common;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public class ArticleCommandService : IArticleCommandService
{
    private readonly VNewsDbContext _context;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<ArticleCommandService> _logger;

    public ArticleCommandService(VNewsDbContext context, IAuditLogService auditLog, ILogger<ArticleCommandService> logger)
    {
        _context = context;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <summary>
    /// Runs a state change and its audit-log write inside ONE database transaction. If anything
    /// throws (including the audit write) the transaction is never committed, so SQL Server rolls
    /// the whole change back when the transaction is disposed. A RowVersion conflict is translated
    /// into <see cref="ArticleConcurrencyException"/>.
    /// </summary>
    private async Task<T> InTransactionAsync<T>(string operation, int? articleId, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await work();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict during {Operation} on article {ArticleId}; transaction rolled back.", operation, articleId);
            throw new ArticleConcurrencyException();
        }
        catch (Exception ex) when (ex is not (EntityNotFoundException or ArticleWorkflowException or ArticleForbiddenException or ArticleConcurrencyException or OperationCanceledException))
        {
            _logger.LogError(ex, "{Operation} on article {ArticleId} failed; transaction rolled back, no partial change was saved.", operation, articleId);
            throw;
        }
    }

    private Task<int> InTransactionAsync(string operation, int? articleId, Func<Task> work, CancellationToken cancellationToken)
        => InTransactionAsync<int>(operation, articleId, async () => { await work(); return 0; }, cancellationToken);

    private void EnsureCanModify(Article article, string actingUserId, bool canManageAll, string operation)
    {
        if (canManageAll || (article.AuthorUserId is not null && article.AuthorUserId == actingUserId))
        {
            return;
        }

        _logger.LogWarning("Forbidden {Operation}: user {UserId} tried to modify article {ArticleId} owned by another author.", operation, actingUserId, article.Id);
        throw new ArticleForbiddenException("Bạn chỉ có thể thao tác trên bài viết của chính mình.");
    }

    /// <summary>
    /// Who may edit what: drafts and returned articles by their author (or an editor/admin); published articles only by an editor/admin
    /// (an author cannot change what is already public, even their own); articles waiting for review are locked.
    /// </summary>
    private void EnsureCanEdit(Article article, string actingUserId, bool canManageAll)
    {
        switch (article.Status)
        {
            case ArticleStatus.Draft or ArticleStatus.Rejected:
                EnsureCanModify(article, actingUserId, canManageAll, "Update");
                return;
            case ArticleStatus.Published when canManageAll:
                return;
            case ArticleStatus.Published:
                _logger.LogWarning("Forbidden UpdatePublished: user {UserId} tried to edit published article {ArticleId}.", actingUserId, article.Id);
                throw new ArticleForbiddenException("Chỉ biên tập viên hoặc quản trị viên mới được sửa bài đã xuất bản.");
            case ArticleStatus.Trashed:
                throw new ArticleWorkflowException("Bài viết đang ở thùng rác. Hãy khôi phục trước khi chỉnh sửa.");
            default:
                throw new ArticleWorkflowException("Bài viết đang chờ duyệt nên không thể chỉnh sửa.");
        }
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        if (!await _context.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new ArticleWorkflowException("Chuyên mục đã chọn không tồn tại.");
        }
    }

    /// <summary>A caption only makes sense under a picture; empty text means "use the default".</summary>
    private static string? CleanCaption(ArticleSaveRequest request) =>
        string.IsNullOrWhiteSpace(request.CoverImageUrl) || string.IsNullOrWhiteSpace(request.CoverCaption) ? null : request.CoverCaption.Trim();

    private static List<ArticleImage> ToImages(IEnumerable<string> urls) =>
        urls.Select((url, index) => new ArticleImage { Url = url, SortOrder = index }).ToList();

    /// <summary>The submitted list replaces the stored one (same order, same pictures = nothing to write).</summary>
    private async Task ReplaceImagesAsync(Article article, IReadOnlyList<string> urls, CancellationToken cancellationToken)
    {
        var current = await _context.ArticleImages.Where(i => i.ArticleId == article.Id).OrderBy(i => i.SortOrder).ToListAsync(cancellationToken);
        if (current.Select(i => i.Url).SequenceEqual(urls)) return;

        _context.ArticleImages.RemoveRange(current);
        foreach (var image in ToImages(urls))
        {
            image.ArticleId = article.Id;
            _context.ArticleImages.Add(image);
        }
    }

    public async Task<int> CreateDraftAsync(ArticleSaveRequest request, string authorUserId, string authorDisplayName, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        var id = await InTransactionAsync("CreateDraft", null, async () =>
        {
            var slug = await GenerateUniqueSlugAsync(request.Title, cancellationToken);

            var article = new Article
            {
                Title = request.Title.Trim(),
                Slug = slug,
                Summary = request.Summary.Trim(),
                Body = ArticleHtmlSanitizer.Sanitize(ArticleBodyFormatter.ToHtml(request.Body)),
                CoverImageUrl = request.CoverImageUrl,
                CoverCaption = CleanCaption(request),
                Images = ToImages(request.GalleryImages),
                CategoryId = request.CategoryId,
                AuthorName = authorDisplayName,
                AuthorUserId = authorUserId,
                Status = ArticleStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            _context.Articles.Add(article);
            await _context.SaveChangesAsync(cancellationToken);

            await _auditLog.LogAsync(authorUserId, authorDisplayName, "Create", "Article", article.Id.ToString(), $"Tạo bản nháp '{article.Title}'", cancellationToken);
            return article.Id;
        }, cancellationToken);

        _logger.LogInformation("Article {ArticleId} draft created by user {UserId}.", id, authorUserId);
        return id;
    }

    public async Task UpdateAsync(int articleId, ArticleSaveRequest request, byte[] rowVersion, string actingUserId, bool canManageAll = false, CancellationToken cancellationToken = default, string? actingUserName = null)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        await InTransactionAsync("UpdateDraft", articleId, async () =>
        {
            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken)
                ?? throw new ArticleNotFoundException(articleId);

            EnsureCanEdit(article, actingUserId, canManageAll);
            var wasPublished = article.Status == ArticleStatus.Published;
            var categoryChanged = article.CategoryId != request.CategoryId;
            var coverChanged = !string.Equals(article.CoverImageUrl ?? string.Empty, request.CoverImageUrl ?? string.Empty, StringComparison.Ordinal);

            _context.Entry(article).Property(a => a.RowVersion).OriginalValue = rowVersion;

            article.Title = request.Title.Trim();
            article.Summary = request.Summary.Trim();
            article.Body = ArticleHtmlSanitizer.Sanitize(ArticleBodyFormatter.ToHtml(request.Body));
            article.CoverImageUrl = request.CoverImageUrl;
            article.CoverCaption = CleanCaption(request);
            await ReplaceImagesAsync(article, request.GalleryImages, cancellationToken);
            article.CategoryId = request.CategoryId;
            article.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            // Slug, status and PublishedAt are deliberately untouched: a published article keeps its public address and publish time.
            var detail = wasPublished
                ? $"Sửa bài đã xuất bản '{article.Title}'" + (categoryChanged ? " (đổi chuyên mục)" : "") + (coverChanged ? " (đổi ảnh bìa)" : "")
                : $"Cập nhật '{article.Title}'";
            await _auditLog.LogAsync(actingUserId, actingUserName ?? article.AuthorName, wasPublished ? "UpdatePublished" : "Update", "Article", article.Id.ToString(), detail, cancellationToken);
        }, cancellationToken);

        _logger.LogInformation("Article {ArticleId} updated by user {UserId}.", articleId, actingUserId);
    }

    public async Task SubmitForReviewAsync(int articleId, string actingUserId, bool canManageAll = false, CancellationToken cancellationToken = default)
    {
        await InTransactionAsync("SubmitForReview", articleId, async () =>
        {
            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken)
                ?? throw new ArticleNotFoundException(articleId);

            EnsureCanModify(article, actingUserId, canManageAll, "SubmitForReview");

            if (article.Status is not ArticleStatus.Draft and not ArticleStatus.Rejected)
            {
                throw new ArticleWorkflowException("Chỉ có thể gửi duyệt bài viết ở trạng thái Nháp hoặc Bị từ chối.");
            }

            article.Status = ArticleStatus.PendingReview;
            article.UpdatedAt = DateTime.UtcNow;
            article.ReviewNote = null;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditLog.LogAsync(actingUserId, article.AuthorName, "SubmitForReview", "Article", article.Id.ToString(), article.Title, cancellationToken);
        }, cancellationToken);

        _logger.LogInformation("Article {ArticleId} submitted for review by user {UserId}.", articleId, actingUserId);
    }

    public async Task PublishAsync(int articleId, string editorUserId, string editorDisplayName, CancellationToken cancellationToken = default)
    {
        await InTransactionAsync("Publish", articleId, async () =>
        {
            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken)
                ?? throw new ArticleNotFoundException(articleId);

            if (article.Status != ArticleStatus.PendingReview)
            {
                throw new ArticleWorkflowException("Chỉ có thể xuất bản bài viết đang chờ duyệt.");
            }

            article.Status = ArticleStatus.Published;
            article.PublishedAt = DateTime.UtcNow;
            article.UpdatedAt = DateTime.UtcNow;
            article.ReviewedByUserId = editorUserId;
            article.ReviewNote = null;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditLog.LogAsync(editorUserId, editorDisplayName, "Publish", "Article", article.Id.ToString(), article.Title, cancellationToken);
        }, cancellationToken);

        _logger.LogInformation("Article {ArticleId} published by user {UserId}.", articleId, editorUserId);
    }

    public async Task RejectAsync(int articleId, string editorUserId, string editorDisplayName, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArticleWorkflowException("Vui lòng nhập lý do từ chối.");
        }

        reason = reason.Trim();
        if (reason.Length > 1000)
        {
            throw new ArticleWorkflowException("Lý do từ chối tối đa 1000 ký tự.");
        }

        await InTransactionAsync("Reject", articleId, async () =>
        {
            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken)
                ?? throw new ArticleNotFoundException(articleId);

            if (article.Status != ArticleStatus.PendingReview)
            {
                throw new ArticleWorkflowException("Chỉ có thể từ chối bài viết đang chờ duyệt.");
            }

            article.Status = ArticleStatus.Rejected;
            article.UpdatedAt = DateTime.UtcNow;
            article.ReviewedByUserId = editorUserId;
            article.ReviewNote = reason;

            await _context.SaveChangesAsync(cancellationToken);
            await _auditLog.LogAsync(editorUserId, editorDisplayName, "Reject", "Article", article.Id.ToString(), reason, cancellationToken);
        }, cancellationToken);

        _logger.LogInformation("Article {ArticleId} rejected by user {UserId}.", articleId, editorUserId);
    }

    private async Task<string> GenerateUniqueSlugAsync(string title, CancellationToken cancellationToken)
    {
        var baseSlug = SlugHelper.Slugify(title);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "bai-viet";
        }

        var slug = baseSlug;
        var suffix = 1;

        while (await _context.Articles.AnyAsync(a => a.Slug == slug, cancellationToken))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }

        return slug;
    }
}
