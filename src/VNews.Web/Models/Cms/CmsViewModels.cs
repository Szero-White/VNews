using VNews.Application.Common;
using VNews.Application.DTOs.Cms;

namespace VNews.Web.Models.Cms;

/// <summary>The newsroom home for Authors, Editors and Admins: figures, what needs attention, and the filtered list.</summary>
public sealed class CmsWorkspaceViewModel
{
    public required CmsStatsDto Stats { get; init; }
    public required PagedResult<ArticleCmsListItemDto> List { get; init; }

    /// <summary>Articles that need the user's attention now (the review queue for editors, returned/draft articles for authors).</summary>
    public IReadOnlyList<ArticleCmsListItemDto> Attention { get; init; } = Array.Empty<ArticleCmsListItemDto>();
    public string AttentionTitle { get; init; } = string.Empty;
    public string AttentionHint { get; init; } = string.Empty;

    public string? Status { get; init; }
    public string? Query { get; init; }
    public bool IsPrivileged { get; init; }
    public string CurrentUserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;

    /// <summary>True on the plain landing view: no filter, no search, first page.</summary>
    public bool IsDashboard => string.IsNullOrEmpty(Status) && string.IsNullOrEmpty(Query) && List.Page == 1;
}

public sealed class AuditRowViewModel
{
    public DateTime CreatedAt { get; init; }
    public string UserDisplayName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string? Detail { get; init; }
}

public sealed class CmsOverviewViewModel
{
    public required CmsStatsDto Stats { get; init; }
    public int Categories { get; init; }
    public int Users { get; init; }
    public IReadOnlyDictionary<string, int> UsersByRole { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<ArticleCmsListItemDto> PendingQueue { get; init; } = Array.Empty<ArticleCmsListItemDto>();
    public IReadOnlyList<AuditRowViewModel> RecentAudit { get; init; } = Array.Empty<AuditRowViewModel>();
}

public sealed class UsersPageViewModel
{
    public required PagedResult<UserListItemViewModel> Users { get; init; }
    public string? Query { get; init; }
    public string? Role { get; init; }
    public string CurrentUserId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, int> UsersByRole { get; init; } = new Dictionary<string, int>();
}

/// <summary>Input of the shared pager: where the links go and which query values (filters) they must keep.</summary>
public sealed class AppPagerModel
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public required string Controller { get; init; }
    public string Action { get; init; } = "Index";
    public IReadOnlyDictionary<string, string?> Query { get; init; } = new Dictionary<string, string?>();
    public string Noun { get; init; } = "mục";
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
