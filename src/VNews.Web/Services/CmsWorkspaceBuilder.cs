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
    private readonly ICategoryQueryService _categories;

    public CmsWorkspaceBuilder(IArticleCmsQueryService articles, ICategoryQueryService categories)
    {
        _articles = articles;
        _categories = categories;
    }

    /// <summary>What the list was asked to show. Anything the viewer may not use (an author picking another author) is ignored.</summary>
    public sealed record Filters(string? Status, string? Query, int? CategoryId, string? Author, DateTime? From, DateTime? To);

    public sealed record Viewer(string UserId, string DisplayName, bool IsPrivileged, string PrimaryRole);

    public async Task<CmsWorkspaceViewModel> BuildAsync(Viewer viewer, Filters filters, int page, CancellationToken cancellationToken)
    {
        // Authors always see only their own articles; only editors and admins may narrow the list to one author name.
        var attentionAuthor = viewer.IsPrivileged ? null : viewer.UserId;
        var authorName = viewer.IsPrivileged && !string.IsNullOrWhiteSpace(filters.Author) ? filters.Author.Trim() : null;
        var status = CmsStatus.All.Contains(filters.Status ?? string.Empty) ? filters.Status : null;
        var query = string.IsNullOrWhiteSpace(filters.Query) ? null : filters.Query.Trim()[..Math.Min(filters.Query.Trim().Length, 100)];
        var from = filters.From;
        var to = filters.To;
        if (from > to) (from, to) = (to, from); // dates entered the wrong way round still give the range the user meant

        var categories = await _categories.GetAllAsync(cancellationToken);
        var categoryId = filters.CategoryId is int picked && categories.Any(c => c.Id == picked) ? picked : (int?)null;
        var attention = await LoadAttentionAsync(viewer, attentionAuthor, cancellationToken);
        return new CmsWorkspaceViewModel
        {
            Stats = await _articles.GetCmsStatsAsync(attentionAuthor, cancellationToken),
            List = await _articles.ListForCmsAsync(status, attentionAuthor, page, PageSize, cancellationToken, query, oldestFirst: status == "PendingReview",
                categoryId: categoryId, updatedFrom: from, updatedTo: to, authorName: authorName),
            Attention = attention.Items,
            AttentionTitle = attention.Title,
            AttentionHint = attention.Hint,
            Status = status,
            Query = query,
            CategoryId = categoryId,
            AuthorName = authorName,
            From = from,
            To = to,
            Categories = categories,
            Authors = viewer.IsPrivileged ? await _articles.GetAuthorNamesAsync(cancellationToken) : Array.Empty<string>(),
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
