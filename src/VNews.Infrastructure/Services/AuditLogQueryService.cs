using Microsoft.EntityFrameworkCore;
using VNews.Application.Common;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public sealed class AuditLogQueryService : IAuditLogQueryService
{
    private readonly VNewsDbContext _context;

    public AuditLogQueryService(VNewsDbContext context) => _context = context;

    private const string SearchCollation = "Latin1_General_CI_AI";

    private static readonly System.Linq.Expressions.Expression<Func<AuditLog, AuditEntryDto>> ToDto = a => new AuditEntryDto
    {
        CreatedAt = a.CreatedAt, UserDisplayName = a.UserDisplayName, Action = a.Action, EntityType = a.EntityType, EntityId = a.EntityId, Detail = a.Detail,
    };

    private IQueryable<AuditLog> Filtered(AuditFilter filter)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Action)) query = query.Where(a => a.Action == filter.Action);
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) query = query.Where(a => a.EntityType == filter.EntityType);
        if (!string.IsNullOrWhiteSpace(filter.User)) query = query.Where(a => a.UserDisplayName == filter.User);
        if (filter.FromUtc is DateTime from) query = query.Where(a => a.CreatedAt >= from);
        if (filter.ToUtcExclusive is DateTime to) query = query.Where(a => a.CreatedAt < to);

        // free text: every word must appear in the detail, the person or the object id (accent-insensitive)
        foreach (var token in SearchText.Tokenize(filter.Search))
        {
            var pattern = $"%{token}%";
            query = query.Where(a =>
                EF.Functions.Like(EF.Functions.Collate(a.Detail ?? "", SearchCollation).Replace("đ", "d"), pattern)
                || EF.Functions.Like(EF.Functions.Collate(a.UserDisplayName, SearchCollation).Replace("đ", "d"), pattern)
                || EF.Functions.Like(a.EntityId, pattern));
        }

        return query;
    }

    public async Task<IReadOnlyList<AuditEntryDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        await _context.AuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Take(Math.Clamp(count, 1, 100)).Select(ToDto).ToListAsync(cancellationToken);

    public Task<PagedResult<AuditEntryDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetPageAsync(new AuditFilter(), page, pageSize, cancellationToken);

    public async Task<PagedResult<AuditEntryDto>> GetPageAsync(AuditFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = Filtered(filter);
        var total = await query.CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, lastPage);

        var rows = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToListAsync(cancellationToken);
        return new PagedResult<AuditEntryDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<AuditFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var logs = _context.AuditLogs.AsNoTracking();
        return new AuditFilterOptions(
            await logs.Select(a => a.Action).Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            await logs.Select(a => a.EntityType).Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            await logs.Where(a => a.UserDisplayName != "").Select(a => a.UserDisplayName).Distinct().OrderBy(x => x).ToListAsync(cancellationToken));
    }
}
