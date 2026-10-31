using System.ComponentModel.DataAnnotations;

namespace VNews.Web.Models.Account;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Tên phải có từ 2 đến 80 ký tự.")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu cần ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
