using VNews.Application.DTOs;

namespace VNews.Web.Models;

/// <summary>Article detail, same-category stories and most-viewed sidebar are loaded by the controller.</summary>
public sealed class ArticleDetailPageViewModel
{
    public required ArticleDetailDto Article { get; init; }
    public IReadOnlyList<ArticleSummaryDto> RelatedArticles { get; init; } = Array.Empty<ArticleSummaryDto>();
    public ArticleNeighborsDto Neighbors { get; init; } = new();
    public CommentThreadDto Comments { get; init; } = new();
    public int ReadMinutes { get; init; }
    public bool IsSignedIn { get; init; }
    public string CurrentUserId { get; init; } = string.Empty;
    public bool CanModerate { get; init; }
    public string? CommentError { get; init; }
    public string? CommentMessage { get; init; }
    public string? CommentDraft { get; init; }
    public int? CommentDraftParent { get; init; }
    public IReadOnlyList<ArticleSummaryDto> MostViewed { get; init; } = Array.Empty<ArticleSummaryDto>();
}
