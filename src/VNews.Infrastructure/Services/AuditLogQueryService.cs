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

    private IQueryable<AuditEntryDto> NewestFirst() =>
        _context.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new AuditEntryDto { CreatedAt = a.CreatedAt, UserDisplayName = a.UserDisplayName, Action = a.Action, EntityType = a.EntityType, EntityId = a.EntityId, Detail = a.Detail });

    public async Task<IReadOnlyList<AuditEntryDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        await NewestFirst().Take(Math.Clamp(count, 1, 100)).ToListAsync(cancellationToken);

    public async Task<PagedResult<AuditEntryDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await _context.AuditLogs.CountAsync(cancellationToken);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, lastPage);

        var rows = await NewestFirst().Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AuditEntryDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = total };
    }
}
