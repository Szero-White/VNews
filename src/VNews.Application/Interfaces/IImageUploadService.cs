namespace VNews.Application.Interfaces;

public class ImageUploadResult
{
    public bool Success { get; init; }
    public string? Url { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IImageUploadService
{
    /// <summary>
    /// Validates and saves an uploaded image under a safe, generated filename.
    /// Raster images only (JPEG/PNG/WebP; SVG is rejected). Enforces a size limit and requires extension, MIME type and actual image bytes
    /// (magic-byte sniff) to agree, preventing disguised files and path traversal.
    /// </summary>
    Task<ImageUploadResult> SaveAsync(Stream content, string originalFileName, string contentType, long length, CancellationToken cancellationToken = default);
}
