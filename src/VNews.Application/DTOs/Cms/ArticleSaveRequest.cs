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

    [Required(ErrorMessage = "Vui lòng chọn chuyên mục")]
    public int CategoryId { get; set; }

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
        if (!IsAcceptableCoverImageUrl(CoverImageUrl))
        {
            yield return new ValidationResult(
                "Ảnh bìa phải là đường dẫn nội bộ (/uploads/...) hoặc địa chỉ http(s) hợp lệ.",
                new[] { nameof(CoverImageUrl) });
        }
    }
}
