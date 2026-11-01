using System.ComponentModel.DataAnnotations;
using VNews.Application.Common;

namespace VNews.Web.Models.Account;

public sealed record SavedArticleRow(int ArticleId, string Slug, string Title, string? CoverImageUrl, string CategoryName, DateTime? PublishedAt, DateTime SavedAtUtc);

/// <summary>Everything the account page shows, read from Identity and the database (nothing invented).</summary>
public sealed class ProfileViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public bool IsStaff { get; init; }

    /// <summary>A demo account: it is signed in through the guarded demo endpoint and its profile is read-only.</summary>
    public bool IsDemo { get; init; }
    public int SavedCount { get; init; }
    public IReadOnlyList<SavedArticleRow> RecentSaved { get; init; } = Array.Empty<SavedArticleRow>();
}

public sealed class UpdateProfileModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Tên phải có từ 2 đến 80 ký tự.")]
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class SavedArticlesViewModel
{
    public required PagedResult<SavedArticleRow> Items { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
