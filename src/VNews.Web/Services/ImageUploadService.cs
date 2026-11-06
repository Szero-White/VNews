using VNews.Application.Interfaces;

namespace VNews.Web.Services;

/// <summary>
/// Raster images only (JPEG, PNG, WebP). SVG is intentionally NOT accepted: it is an XML document
/// that can carry script, and uploads are served from the site's own origin.
/// Every upload must agree on three independent signals — file extension, declared MIME type and
/// the real leading bytes — and is stored under a generated name whose extension comes from the
/// detected format, never from the client.
/// </summary>
public class ImageUploadService : IImageUploadService
{
    private const long MaxBytes = ImageUploadLimits.MaxBytes;

    private enum ImageFormat { Jpeg, Png, WebP }

    private static readonly Dictionary<string, (ImageFormat Format, string ContentType)> ByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = (ImageFormat.Jpeg, "image/jpeg"),
            [".jpeg"] = (ImageFormat.Jpeg, "image/jpeg"),
            [".png"] = (ImageFormat.Png, "image/png"),
            [".webp"] = (ImageFormat.WebP, "image/webp"),
        };

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly UploadStorage _storage;
    private readonly ILogger<ImageUploadService> _logger;
    private readonly IExternalImageStore? _external;

    /// <param name="external">Registered only when Cloudinary upload is configured; otherwise pictures are written to the local uploads folder.</param>
    public ImageUploadService(UploadStorage storage, ILogger<ImageUploadService> logger, IExternalImageStore? external = null)
    {
        _storage = storage;
        _logger = logger;
        _external = external;
    }

    public async Task<ImageUploadResult> SaveAsync(Stream content, string originalFileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0 || length > MaxBytes)
        {
            return Reject("size", "Ảnh phải nhỏ hơn " + ImageUploadLimits.Label + ".", length);
        }

        var extension = Path.GetExtension(originalFileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension) || !ByExtension.TryGetValue(extension, out var expected))
        {
            return Reject("extension", "Chỉ chấp nhận ảnh .jpg, .jpeg, .png, .webp.", length);
        }

        // The declared MIME type must be the one that belongs to this extension (no "image/png" on a .jpg).
        var mime = (contentType ?? string.Empty).Split(';')[0].Trim();
        if (!string.Equals(mime, expected.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Reject("content-type", "Loại nội dung tệp không khớp với phần mở rộng.", length);
        }

        // Never trust the declared length: read at most MaxBytes + 1 and fail if the stream is bigger.
        using var memoryStream = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            if (memoryStream.Length + read > MaxBytes)
            {
                return Reject("size-actual", "Ảnh phải nhỏ hơn " + ImageUploadLimits.Label + ".", length);
            }

            memoryStream.Write(buffer, 0, read);
        }

        var bytes = memoryStream.ToArray();
        if (!HasSignature(bytes, expected.Format))
        {
            return Reject("signature", "Nội dung tệp không khớp với định dạng ảnh khai báo.", length);
        }

        var storedExtension = expected.Format switch
        {
            ImageFormat.Jpeg => ".jpg",
            ImageFormat.Png => ".png",
            _ => ".webp"
        };
        var safeFileName = $"{Guid.NewGuid():N}{storedExtension}";

        if (_external is not null)
        {
            try
            {
                await _external.StoreAsync(safeFileName, bytes, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Image store failed for {StoredFile}.", safeFileName);
                return new ImageUploadResult { Success = false, ErrorMessage = "Không lưu được ảnh vào kho ảnh. Vui lòng thử lại sau." };
            }
        }
        else
        {
            var uploadsRoot = _storage.ArticlesPath;
            Directory.CreateDirectory(uploadsRoot);
            await File.WriteAllBytesAsync(Path.Combine(uploadsRoot, safeFileName), bytes, cancellationToken);
        }

        _logger.LogInformation("Cover image uploaded: {StoredFile} ({Format}, {Bytes} bytes).", safeFileName, expected.Format, bytes.Length);
        return new ImageUploadResult { Success = true, Url = $"/uploads/articles/{safeFileName}" };
    }

    private ImageUploadResult Reject(string reason, string message, long declaredLength)
    {
        // Only the reason code and size are logged: the client-supplied file name is untrusted input.
        _logger.LogWarning("Image upload rejected: {Reason} (declared {DeclaredBytes} bytes).", reason, declaredLength);
        return new ImageUploadResult { Success = false, ErrorMessage = message };
    }

    private static bool HasSignature(byte[] bytes, ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ImageFormat.Png => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(PngSignature),
        ImageFormat.WebP => bytes.Length >= 12
            && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
            && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P',
        _ => false
    };
}
