using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

/// <summary>Read side of the newsroom: what the CMS lists, counts and loads into the edit form. Never changes data.</summary>
public class ArticleCmsQueryService : IArticleCmsQueryService
{
    // Accent- and case-insensitive matching, same idea as the public search (đ/Đ are replaced on the column side).
    private const string SearchCollation = "Latin1_General_100_CI_AI";

    private readonly VNewsDbContext _context;

    public ArticleCmsQueryService(VNewsDbContext context)
    {
        _context = context;
    }

    public async Task<CmsStatsDto> GetCmsStatsAsync(string? authorUserId, CancellationToken cancellationToken = default)
    {
        var query = _context.Articles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(authorUserId))
        {
            query = query.Where(a => a.AuthorUserId == authorUserId);
        }

        var counts = await query.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        int Of(ArticleStatus status) => counts.FirstOrDefault(c => c.Key == status)?.Count ?? 0;
        return new CmsStatsDto
        {
            Draft = Of(ArticleStatus.Draft),
            PendingReview = Of(ArticleStatus.PendingReview),
            Rejected = Of(ArticleStatus.Rejected),
            Published = Of(ArticleStatus.Published),
            Trashed = Of(ArticleStatus.Trashed),
        };
    }

    public async Task<PagedResult<ArticleCmsListItemDto>> ListForCmsAsync(
        string? statusFilter, string? authorUserId, int page, int pageSize, CancellationToken cancellationToken = default,
        string? search = null, bool oldestFirst = false,
        int? categoryId = null, DateTime? updatedFrom = null, DateTime? updatedTo = null, string? authorName = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _context.Articles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(authorUserId))
        {
            query = query.Where(a => a.AuthorUserId == authorUserId);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<ArticleStatus>(statusFilter, true, out var status))
        {
            query = query.Where(a => a.Status == status);
        }
        else
        {
            query = query.Where(a => a.Status != ArticleStatus.Trashed); // the trash is only ever shown when asked for
        }

        if (!string.IsNullOrWhiteSpace(authorName))
        {
            query = query.Where(a => a.AuthorName == authorName);
        }

        if (categoryId is int category)
        {
            // a parent category also covers the stories of its sub-categories
            query = query.Where(a => a.CategoryId == category || a.Category.ParentId == category);
        }

        if (updatedFrom is DateTime from)
        {
            query = query.Where(a => (a.UpdatedAt ?? a.CreatedAt) >= from.Date);
        }

        if (updatedTo is DateTime to)
        {
            query = query.Where(a => (a.UpdatedAt ?? a.CreatedAt) < to.Date.AddDays(1)); // the whole last day
        }

        foreach (var token in SearchText.Tokenize(search))
        {
            var pattern = $"%{token}%";
            query = query.Where(a =>
                EF.Functions.Like(EF.Functions.Collate(a.Title, SearchCollation).Replace("đ", "d"), pattern)
                || EF.Functions.Like(EF.Functions.Collate(a.AuthorName, SearchCollation).Replace("đ", "d"), pattern)
                || EF.Functions.Like(EF.Functions.Collate(a.Category.Name, SearchCollation).Replace("đ", "d"), pattern)
                || EF.Functions.Like(EF.Functions.Collate(a.Summary, SearchCollation).Replace("đ", "d"), pattern));
        }

        var ordered = oldestFirst
            ? query.OrderBy(a => a.UpdatedAt ?? a.CreatedAt).ThenBy(a => a.Id)
            : query.OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt).ThenByDescending(a => a.Id);

        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ArticleCmsListItemDto
            {
                Id = a.Id,
                Title = a.Title,
                Slug = a.Slug,
                Status = a.Status.ToString(),
                CategoryName = a.Category.Name,
                AuthorName = a.AuthorName,
                AuthorUserId = a.AuthorUserId,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                PublishedAt = a.PublishedAt,
                ReviewNote = a.ReviewNote,
                CoverImageUrl = a.CoverImageUrl,
                DeletedAt = a.DeletedAt,
                StatusBeforeDeletion = a.StatusBeforeDeletion == null ? null : a.StatusBeforeDeletion.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ArticleCmsListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<IReadOnlyList<string>> GetAuthorNamesAsync(CancellationToken cancellationToken = default)
    {
        var names = await _context.Articles.AsNoTracking()
            .Where(a => a.Status != ArticleStatus.Trashed && a.AuthorName != "")
            .Select(a => a.AuthorName).Distinct().ToListAsync(cancellationToken);
        return names.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<ArticleEditDto?> GetForEditAsync(int articleId, CancellationToken cancellationToken = default)
    {
        var dto = await _context.Articles
            .AsNoTracking()
            .Where(a => a.Id == articleId)
            .Select(a => new ArticleEditDto
            {
                Id = a.Id,
                Title = a.Title,
                Slug = a.Slug,
                Summary = a.Summary,
                Body = a.Body,
                CoverImageUrl = a.CoverImageUrl,
                CoverCaption = a.CoverCaption,
                AuthorName = a.AuthorName,
                AuthorUserId = a.AuthorUserId,
                Status = a.Status.ToString(),
                ReviewNote = a.ReviewNote,
                CategoryId = a.CategoryId,
                RowVersion = a.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is not null)
        {
            dto.GalleryImages = await _context.ArticleImages.AsNoTracking()
                .Where(i => i.ArticleId == articleId).OrderBy(i => i.SortOrder).Select(i => i.Url).ToListAsync(cancellationToken);
        }

        return dto;
    }
}
