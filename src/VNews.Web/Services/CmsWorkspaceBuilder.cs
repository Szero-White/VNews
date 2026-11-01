using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Web.Helpers;
using VNews.Web.Models.Cms;

namespace VNews.Web.Services;

/// <summary>
/// Arranges the newsroom home for one user: figures, the "needs attention" block, and the filtered list. Authors only ever see their own
/// articles (the author filter is applied to every query here); editors and admins see all.
/// </summary>
public sealed class CmsWorkspaceBuilder
{
    private const int PageSize = 15;
    private const int AttentionCount = 5;

    private readonly IArticleCmsQueryService _articles;

    public CmsWorkspaceBuilder(IArticleCmsQueryService articles) => _articles = articles;

    public sealed record Viewer(string UserId, string DisplayName, bool IsPrivileged, string PrimaryRole);

    public async Task<CmsWorkspaceViewModel> BuildAsync(Viewer viewer, string? status, string? query, int page, CancellationToken cancellationToken)
    {
        var authorFilter = viewer.IsPrivileged ? null : viewer.UserId;
        status = CmsStatus.All.Contains(status ?? string.Empty) ? status : null;
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim()[..Math.Min(query.Trim().Length, 100)];

        var attention = await LoadAttentionAsync(viewer, authorFilter, cancellationToken);
        return new CmsWorkspaceViewModel
        {
            Stats = await _articles.GetCmsStatsAsync(authorFilter, cancellationToken),
            List = await _articles.ListForCmsAsync(status, authorFilter, page, PageSize, cancellationToken, query, oldestFirst: status == "PendingReview"),
            Attention = attention.Items,
            AttentionTitle = attention.Title,
            AttentionHint = attention.Hint,
            Status = status,
            Query = query,
            IsPrivileged = viewer.IsPrivileged,
            CurrentUserId = viewer.UserId,
            DisplayName = viewer.DisplayName,
            RoleLabel = AppFormat.RoleLabel(viewer.PrimaryRole),
        };
    }

    /// <summary>What needs the user's attention: editors/admins the review queue (oldest first); authors the articles sent back, else their drafts.</summary>
    private async Task<(IReadOnlyList<ArticleCmsListItemDto> Items, string Title, string Hint)> LoadAttentionAsync(Viewer viewer, string? authorFilter, CancellationToken cancellationToken)
    {
        if (viewer.IsPrivileged)
        {
            var queue = (await _articles.ListForCmsAsync("PendingReview", null, 1, AttentionCount, cancellationToken, oldestFirst: true)).Items;
            return (queue, "Bài cần xử lý", "Bài đang chờ duyệt, cũ nhất ở trên cùng.");
        }

        var returned = (await _articles.ListForCmsAsync("Rejected", authorFilter, 1, AttentionCount, cancellationToken)).Items;
        if (returned.Count > 0) return (returned, "Cần chỉnh sửa", "Bài được biên tập viên trả lại kèm lý do.");

        var drafts = (await _articles.ListForCmsAsync("Draft", authorFilter, 1, AttentionCount, cancellationToken)).Items;
        return (drafts, "Bản nháp đang viết", "Hoàn thiện rồi gửi duyệt khi sẵn sàng.");
    }
}
