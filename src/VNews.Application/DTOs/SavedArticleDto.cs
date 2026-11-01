namespace VNews.Application.DTOs;

/// <summary>An article a reader saved (only published articles are ever listed).</summary>
public sealed record SavedArticleDto(int ArticleId, string Slug, string Title, string? CoverImageUrl, string CategoryName, DateTime? PublishedAt, DateTime SavedAtUtc);
