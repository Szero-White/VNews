using Microsoft.AspNetCore.Mvc;

namespace VNews.Web.Helpers;

public static class ArticleImages
{
    public const string FallbackPath = "~/images/demo/fallback.svg";

    /// <summary>The article's cover, or the site's neutral placeholder when it has none. The single definition used by every card, list and panel.</summary>
    public static string CoverOrFallback(this IUrlHelper url, string? coverImageUrl) =>
        string.IsNullOrWhiteSpace(coverImageUrl) ? url.Content(FallbackPath) : coverImageUrl;
}
