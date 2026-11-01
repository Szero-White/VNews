using System.Linq.Expressions;
using VNews.Application.DTOs;
using VNews.Domain.Entities;

namespace VNews.Infrastructure.Common;

/// <summary>Query building blocks shared by the public read services, so "a published article summary" is defined in exactly one place.</summary>
internal static class ArticleProjections
{
    /// <summary>An expression (not a method) so EF translates it into the SQL projection.</summary>
    public static readonly Expression<Func<Article, ArticleSummaryDto>> ToSummary = a => new ArticleSummaryDto
    {
        Id = a.Id,
        Title = a.Title,
        Slug = a.Slug,
        Summary = a.Summary,
        CoverImageUrl = a.CoverImageUrl,
        AuthorName = a.AuthorName,
        PublishedAt = a.PublishedAt,
        CategoryName = a.Category.Name,
        CategorySlug = a.Category.Slug
    };
}
