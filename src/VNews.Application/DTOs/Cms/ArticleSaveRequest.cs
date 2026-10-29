using System.ComponentModel.DataAnnotations;

namespace VNews.Application.DTOs.Cms;

public class ArticleSaveRequest
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

    public string? CoverImageUrl { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn chuyên mục")]
    public int CategoryId { get; set; }
}
