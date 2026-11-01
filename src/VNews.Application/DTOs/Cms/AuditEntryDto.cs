namespace VNews.Application.DTOs.Cms;

public sealed class AuditEntryDto
{
    public DateTime CreatedAt { get; init; }
    public string UserDisplayName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? Detail { get; init; }
}
