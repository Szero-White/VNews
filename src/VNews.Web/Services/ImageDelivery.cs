using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace VNews.Web.Services;

/// <summary>
/// Decides where a stored picture address is served from. The database keeps /uploads/articles/{32 hex}.{jpg|png|webp} for every
/// picture; when Cloudinary is configured this class turns that address into the Cloudinary delivery URL, otherwise it leaves it alone
/// and the web server's own /uploads folder serves it. The 32-hex name is also the Cloudinary public id, so nothing needs a lookup table.
/// </summary>
public sealed class ImageDelivery
{
    private const string LocalPrefix = "/uploads/articles/";

    private static readonly Regex Exact = new("^/uploads/articles/(?<id>[0-9a-f]{32})\\.(?:jpg|png|webp)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Inside HTML: only an address that starts an attribute value or url(...), so plain text and "https://elsewhere/uploads/articles/..." are never touched.
    private static readonly Regex InHtml = new("(?<=[\"'(=])/uploads/articles/(?<id>[0-9a-f]{32})\\.(?:jpg|png|webp)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly CloudinaryOptions _options;

    public ImageDelivery(CloudinaryOptions options) => _options = options;

    /// <summary>True when pictures are served from Cloudinary.</summary>
    public bool Enabled => _options.CanDeliver;

    /// <summary>The Cloudinary URL for a stored picture address, or the address itself when it is not one of ours or Cloudinary is off.</summary>
    [return: NotNullIfNotNull(nameof(storedUrl))]
    public string? Resolve(string? storedUrl) => TryResolve(storedUrl, out var url) ? url : storedUrl;

    public bool TryResolve(string? storedUrl, [NotNullWhen(true)] out string? deliveryUrl)
    {
        deliveryUrl = null;
        if (!Enabled || string.IsNullOrEmpty(storedUrl)) return false;
        var match = Exact.Match(storedUrl);
        if (!match.Success) return false;
        deliveryUrl = Build(match.Groups["id"].Value);
        return true;
    }

    /// <summary>Rewrites the picture addresses inside article HTML. Everything else in the text is returned unchanged.</summary>
    public string RewriteHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return html ?? string.Empty;
        if (!Enabled || !html.Contains(LocalPrefix, StringComparison.Ordinal)) return html;
        return InHtml.Replace(html, m => Build(m.Groups["id"].Value));
    }

    private string Build(string id)
    {
        var transform = _options.DeliveryTransform;
        var middle = transform.Length == 0 ? string.Empty : transform + "/";
        // No extension on purpose: with f_auto Cloudinary then answers in WebP/AVIF/JPEG as the browser accepts.
        return $"https://res.cloudinary.com/{_options.CloudName}/image/upload/{middle}{_options.Folder}/{id}";
    }
}
