using Microsoft.AspNetCore.Mvc;
using VNews.Web.Services;

namespace VNews.Web.Helpers;

public static class ArticleImages
{
    public const string FallbackPath = "~/images/demo/fallback.svg";

    /// <summary>The article's cover, or the site's neutral placeholder when it has none. The single definition used by every card, list and panel.</summary>
    public static string CoverOrFallback(this IUrlHelper url, string? coverImageUrl) =>
        string.IsNullOrWhiteSpace(coverImageUrl) ? url.Content(FallbackPath) : url.Picture(coverImageUrl);

    /// <summary>
    /// Where a stored picture address is shown from: its Cloudinary URL when Cloudinary is configured, the address itself otherwise
    /// (and for anything that is not a picture this site stored, such as an imported headline's external image).
    /// </summary>
    public static string Picture(this IUrlHelper url, string? storedUrl)
    {
        var delivery = url.ActionContext.HttpContext.RequestServices.GetService<ImageDelivery>();
        return delivery?.Resolve(storedUrl) ?? storedUrl ?? string.Empty;
    }
}
