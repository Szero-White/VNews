namespace VNews.Application.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string userId, string userDisplayName, string action, string entityType, string entityId, string? detail = null, CancellationToken cancellationToken = default);
}
