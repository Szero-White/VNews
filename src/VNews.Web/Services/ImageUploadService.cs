using VNews.Application.Interfaces;

namespace VNews.Web.Services;

public class ImageUploadService : IImageUploadService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/svg+xml"
    };
    private const long MaxBytes = 3 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public ImageUploadService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<ImageUploadResult> SaveAsync(Stream content, string originalFileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0 || length > MaxBytes)
        {
            return new ImageUploadResult { Success = false, ErrorMessage = "Ảnh phải nhỏ hơn 3MB." };
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return new ImageUploadResult { Success = false, ErrorMessage = "Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp, .svg." };
        }

        if (!AllowedContentTypes.Contains(contentType))
        {
            return new ImageUploadResult { Success = false, ErrorMessage = "Loại nội dung tệp không hợp lệ." };
        }

        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        if (!IsValidImageSignature(bytes, extension))
        {
            return new ImageUploadResult { Success = false, ErrorMessage = "Nội dung tệp không khớp với định dạng ảnh khai báo." };
        }

        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "articles");
        Directory.CreateDirectory(uploadsRoot);

        var safeFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsRoot, safeFileName);

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

        return new ImageUploadResult { Success = true, Url = $"/uploads/articles/{safeFileName}" };
    }

    private static bool IsValidImageSignature(byte[] bytes, string extension)
    {
        if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            // SVG is text-based; reject anything containing <script to reduce XSS risk via uploaded SVG.
            var text = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 4096));
            return text.Contains("<svg", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("<script", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("onload=", StringComparison.OrdinalIgnoreCase);
        }

        if (bytes.Length < 8)
        {
            return false;
        }

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return true;
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return true;
        }

        // WEBP: "RIFF"...."WEBP"
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return true;
        }

        return false;
    }
}
