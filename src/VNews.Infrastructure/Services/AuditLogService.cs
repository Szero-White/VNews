using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly VNewsDbContext _context;

    public AuditLogService(VNewsDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string userId, string userDisplayName, string action, string entityType, string entityId, string? detail = null, CancellationToken cancellationToken = default)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserDisplayName = userDisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
