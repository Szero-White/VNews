namespace VNews.Application.Interfaces;

/// <summary>
/// An off-site home for uploaded pictures (Cloudinary). When one is registered, <see cref="IImageUploadService"/> sends each validated
/// upload here instead of writing it to the web server's disk. The address kept in the database does not change: it is still
/// /uploads/articles/{32 hex}.{jpg|png|webp}, and the web layer decides where that picture is served from.
/// </summary>
public interface IExternalImageStore
{
    /// <summary>
    /// Stores one picture under its generated file name (the 32-hex name with its extension). Must throw when the picture was not
    /// stored, so the upload is reported as failed rather than leaving an address that points at nothing.
    /// </summary>
    Task StoreAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);
}
