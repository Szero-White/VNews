using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using VNews.Application.Interfaces;

namespace VNews.Web.Services;

/// <summary>
/// Stores uploaded article pictures in Cloudinary with the official .NET SDK. The public id is "{folder}/{32 hex}" (no extension), the
/// same name the database address carries, and an existing id is never overwritten.
/// </summary>
public sealed class CloudinaryImageStore : IExternalImageStore
{
    private readonly Cloudinary _cloudinary;
    private readonly string _folder;

    public CloudinaryImageStore(CloudinaryOptions options)
    {
        if (!options.CanUpload) throw new InvalidOperationException("Cloudinary upload is not configured.");
        _folder = options.Folder;
        _cloudinary = new Cloudinary(new Account(options.CloudName, options.ApiKey, options.ApiSecret)) { Api = { Secure = true } };
    }

    public async Task StoreAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content, writable: false);
        var parameters = new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            PublicId = $"{_folder}/{Path.GetFileNameWithoutExtension(fileName)}",
            Overwrite = false,
            UniqueFilename = false,
            UseFilename = false,
        };

        var result = await _cloudinary.UploadAsync(parameters, cancellationToken);
        if (result.Error is not null || (int)result.StatusCode >= 400)
        {
            // The message comes from Cloudinary and never contains the credentials.
            throw new InvalidOperationException($"Cloudinary did not store the picture ({(int)result.StatusCode}): {result.Error?.Message}");
        }
    }
}
