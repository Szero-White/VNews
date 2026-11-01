using VNews.Application.DTOs;

namespace VNews.Web.Models;

/// <summary>Article detail, same-category stories and most-viewed sidebar are loaded by the controller.</summary>
public sealed class ArticleDetailPageViewModel
{
    public required ArticleDetailDto Article { get; init; }
    public IReadOnlyList<ArticleSummaryDto> RelatedArticles { get; init; } = Array.Empty<ArticleSummaryDto>();
    public IReadOnlyList<ArticleSummaryDto> MostViewed { get; init; } = Array.Empty<ArticleSummaryDto>();
}
