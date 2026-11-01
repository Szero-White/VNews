using System.ComponentModel.DataAnnotations;

namespace VNews.Web.Models.Cms;

public class CategorySaveViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên chuyên mục không được để trống")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug không được để trống")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug chỉ gồm chữ thường, số và dấu gạch ngang")]
    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Optional parent: makes this a sub-category (one level deep; validated by the service).</summary>
    public int? ParentId { get; set; }
}
