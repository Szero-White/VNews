namespace VNews.Application.DTOs.Cms;

/// <summary>What the activity log is narrowed to. Dates are UTC instants (the page converts the days the user picked from Vietnam time).</summary>
public sealed record AuditFilter(string? Action = null, string? EntityType = null, string? User = null, string? Search = null, DateTime? FromUtc = null, DateTime? ToUtcExclusive = null)
{
    public bool IsEmpty => Action is null && EntityType is null && User is null && Search is null && FromUtc is null && ToUtcExclusive is null;
}

/// <summary>The values that exist in the log, for the filter boxes.</summary>
public sealed record AuditFilterOptions(IReadOnlyList<string> Actions, IReadOnlyList<string> EntityTypes, IReadOnlyList<string> Users);
