using VNews.Application.DTOs;
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

    /// <summary>The other filters of the list: category, author (editors/admins only) and the range of the last-change date.</summary>
    public int? CategoryId { get; init; }
    public string? AuthorName { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public IReadOnlyList<CategoryDto> Categories { get; init; } = Array.Empty<CategoryDto>();
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();

    public bool HasFilter => Status is not null || Query is not null || CategoryId is not null || AuthorName is not null || From is not null || To is not null;
    public bool IsPrivileged { get; init; }
    public string CurrentUserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string RoleLabel { get; init; } = string.Empty;

    /// <summary>True on the plain landing view: no filter, no search, first page.</summary>
    public bool IsDashboard => !HasFilter && List.Page == 1;
}

/// <summary>The activity log page: the filtered page of entries, the values the filter boxes offer, and what is currently chosen.</summary>
public sealed class AuditPageViewModel
{
    public required PagedResult<AuditEntryDto> Page { get; init; }
    public required AuditFilterOptions Options { get; init; }
    public string? Action { get; init; }
    public string? Entity { get; init; }
    public string? User { get; init; }
    public string? Query { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }

    public bool HasFilter => Action is not null || Entity is not null || User is not null || Query is not null || From is not null || To is not null;
}

public sealed class AnalyticsPageViewModel
{
    public required AnalyticsReport Report { get; init; }
    public IReadOnlyList<int> Ranges { get; init; } = Array.Empty<int>();
}

public sealed class CmsOverviewViewModel
{
    public required CmsStatsDto Stats { get; init; }
    public int Categories { get; init; }
    public int Users { get; init; }
    public IReadOnlyDictionary<string, int> UsersByRole { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<ArticleCmsListItemDto> PendingQueue { get; init; } = Array.Empty<ArticleCmsListItemDto>();
    public IReadOnlyList<AuditEntryDto> RecentAudit { get; init; } = Array.Empty<AuditEntryDto>();
}

public sealed class UsersPageViewModel
{
    public required PagedResult<UserListItemDto> Users { get; init; }
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

/// <summary>Input of the shared Publish / Reject buttons (list row and edit page), so the confirmation texts exist in one place.</summary>
public sealed record ReviewActionsModel(int Id, string Title, bool Small);

/// <summary>Input of the shared Move-to-trash / Restore buttons. RowVersion (base64) is optional and only sent from the edit page.</summary>
public sealed record TrashActionsModel(int Id, string Title, bool Small, bool CanTrash, bool CanRestore, string? RowVersion = null, bool InPlace = false);
