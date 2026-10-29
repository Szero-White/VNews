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
    /// Enforces allow-listed extensions/content-types, a size limit, and verifies actual image bytes
    /// (magic-byte sniff) to prevent disguised executables or path traversal.
    /// </summary>
    Task<ImageUploadResult> SaveAsync(Stream content, string originalFileName, string contentType, long length, CancellationToken cancellationToken = default);
}
