using System.ComponentModel.DataAnnotations;

namespace VNews.Application.DTOs.Cms;

public class ArticleSaveRequest : IValidatableObject
{
    [Required(ErrorMessage = "Tiêu đề không được để trống")]
    [StringLength(300, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5-300 ký tự")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tóm tắt không được để trống")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Tóm tắt phải từ 10-500 ký tự")]
    public string Summary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nội dung không được để trống")]
    [MinLength(20, ErrorMessage = "Nội dung quá ngắn")]
    public string Body { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Đường dẫn ảnh bìa tối đa 500 ký tự")]
    public string? CoverImageUrl { get; set; }

    /// <summary>Optional line under the cover picture; empty means the default "Ảnh minh họa". Ignored when there is no cover.</summary>
    [StringLength(200, ErrorMessage = "Chú thích ảnh bìa tối đa 200 ký tự")]
    public string? CoverCaption { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn chuyên mục")]
    public int CategoryId { get; set; }

    /// <summary>Extra pictures shown below the text, in order (0..MaxGalleryImages). Each is an upload address of this site.</summary>
    public List<string> GalleryImages { get; set; } = new();

    public const int MaxGalleryImages = 20;

    /// <summary>Only pictures this site stored itself: /uploads/articles/{32 hex}.{jpg|png|webp}. Nothing else is shown from the gallery.</summary>
    public static bool IsGalleryImageUrl(string? value) =>
        value is not null && System.Text.RegularExpressions.Regex.IsMatch(value, @"^/uploads/articles/[0-9a-f]{32}\.(jpg|png|webp)$");

    /// <summary>
    /// The cover image must be an uploaded/local image or an absolute http(s) URL. Blocks
    /// javascript:, data:, protocol-relative and path-traversal values from reaching the page.
    /// </summary>
    public static bool IsAcceptableCoverImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        if (value.StartsWith('/'))
        {
            return !value.StartsWith("//", StringComparison.Ordinal)
                && !value.Contains('\\')
                && !value.Contains("..", StringComparison.Ordinal);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Empty slots (an "add picture" box left unused) are dropped quietly; anything else must be a picture of this site.
        GalleryImages = GalleryImages.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList();
        if (GalleryImages.Count > MaxGalleryImages)
        {
            yield return new ValidationResult($"Mỗi bài tối đa {MaxGalleryImages} ảnh trong bài.", new[] { nameof(GalleryImages) });
        }
        else if (GalleryImages.Any(u => !IsGalleryImageUrl(u)))
        {
            yield return new ValidationResult("Ảnh trong bài phải là ảnh đã tải lên của hệ thống.", new[] { nameof(GalleryImages) });
        }

        if (!IsAcceptableCoverImageUrl(CoverImageUrl))
        {
            yield return new ValidationResult(
                "Ảnh bìa phải là đường dẫn nội bộ (/uploads/...) hoặc địa chỉ http(s) hợp lệ.",
                new[] { nameof(CoverImageUrl) });
        }
    }
}
