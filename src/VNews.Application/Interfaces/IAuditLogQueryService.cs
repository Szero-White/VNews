using VNews.Application.Common;
using VNews.Application.DTOs.Cms;

namespace VNews.Application.Interfaces;

/// <summary>Reads the activity log for administrators (writing is <see cref="IAuditLogService"/>).</summary>
public interface IAuditLogQueryService
{
    Task<IReadOnlyList<AuditEntryDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Newest first. A page past the end returns the last page.</summary>
    Task<PagedResult<AuditEntryDto>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The filtered, newest-first page of the log.</summary>
    Task<PagedResult<AuditEntryDto>> GetPageAsync(AuditFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The actions, object types and people that appear in the log.</summary>
    Task<AuditFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}
