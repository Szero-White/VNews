using System.Text.RegularExpressions;

namespace VNews.Web.Services;

/// <summary>
/// Cloudinary settings. Showing pictures needs only the cloud name (public); storing new ones also needs the API key and secret, which
/// must come from the environment or a server-only file, never from the repository:
///   Cloudinary:CloudName / Cloudinary:ApiKey / Cloudinary:ApiSecret   (environment: Cloudinary__ApiSecret, ...)
/// The site reads no other variable on purpose (not even Cloudinary's own CLOUDINARY_URL), so a developer's shell that holds the
/// credentials for the migration tool can never make a test run or a local run upload to the real account.
/// With no cloud name the site behaves exactly as before: pictures are kept and served from the web server's own disk.
/// </summary>
public sealed class CloudinaryOptions
{
    public const string DefaultFolder = "vnews/articles";
    public const string DefaultTransform = "f_auto,q_auto";

    private static readonly Regex CloudNamePattern = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FolderPattern = new("^[A-Za-z0-9_-]+(/[A-Za-z0-9_-]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TransformPattern = new("^[A-Za-z0-9_,:.\\-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string CloudName { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string ApiSecret { get; init; } = "";

    /// <summary>Folder (public-id prefix) the article pictures live under in the Media Library.</summary>
    public string Folder { get; init; } = DefaultFolder;

    /// <summary>Transformation segment of every delivery URL. "f_auto,q_auto" lets Cloudinary pick the lightest format and quality.</summary>
    public string DeliveryTransform { get; init; } = DefaultTransform;

    /// <summary>Pictures can be shown from Cloudinary (needs only the public cloud name).</summary>
    public bool CanDeliver => CloudNamePattern.IsMatch(CloudName);

    /// <summary>New pictures can be stored on Cloudinary (also needs the key and the secret).</summary>
    public bool CanUpload => CanDeliver && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);

    public static CloudinaryOptions From(IConfiguration configuration)
    {
        var section = configuration.GetSection("Cloudinary");
        var folder = Clean(section["Folder"]).Trim('/');
        var transform = section["DeliveryTransform"];
        return new CloudinaryOptions
        {
            CloudName = Clean(section["CloudName"]),
            ApiKey = Clean(section["ApiKey"]),
            ApiSecret = Clean(section["ApiSecret"]),
            Folder = FolderPattern.IsMatch(folder) ? folder : DefaultFolder,
            // An explicitly empty transform means "deliver the original"; anything with unexpected characters falls back to the default.
            DeliveryTransform = transform is null ? DefaultTransform : TransformPattern.IsMatch(transform.Trim()) ? transform.Trim() : DefaultTransform,
        };
    }

    private static string Clean(string? value) => (value ?? string.Empty).Trim();

    /// <summary>Never prints the key or the secret, so an accidental log line or exception message cannot leak them.</summary>
    public override string ToString() => $"Cloudinary(cloud={(CanDeliver ? CloudName : "none")}, upload={(CanUpload ? "on" : "off")}, folder={Folder})";
}
