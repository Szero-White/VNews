using VNews.Application.Common;
using VNews.Application.DTOs.Cms;

namespace VNews.Application.Interfaces;

/// <summary>Reads the activity log for administrators (writing is <see cref="IAuditLogService"/>).</summary>
public interface IAuditLogQueryService
{
    Task<IReadOnlyList<AuditEntryDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Newest first. A page past the end returns the last page.</summary>
    Task<PagedResult<AuditEntryDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
