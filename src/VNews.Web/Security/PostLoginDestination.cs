using Microsoft.AspNetCore.Mvc;
using VNews.Domain.Identity;

namespace VNews.Web.Security;

/// <summary>
/// Where a user lands after signing in, based on the roles the server actually granted. A return URL is only
/// honoured when it is a local URL (checked by the caller) and, for the newsroom area, only for staff: a reader
/// never gets sent into the CMS.
/// </summary>
public static class PostLoginDestination
{
    public static bool IsStaff(IEnumerable<string> roles) =>
        roles.Any(r => r is Roles.Admin or Roles.Editor or Roles.Author);

    public static string For(IUrlHelper url, IEnumerable<string> roles, string? safeReturnUrl)
    {
        var granted = roles as IList<string> ?? roles.ToList();
        var staff = IsStaff(granted);

        if (!string.IsNullOrEmpty(safeReturnUrl))
        {
            var newsroom = safeReturnUrl.StartsWith("/cms", StringComparison.OrdinalIgnoreCase);
            return newsroom && !staff ? Home(url) : safeReturnUrl;
        }

        if (granted.Contains(Roles.Admin)) return url.Action("Index", "CmsUsers") ?? "/cms";
        if (granted.Contains(Roles.Editor)) return url.Action("Index", "CmsArticles", new { status = "PendingReview" }) ?? "/cms";
        if (granted.Contains(Roles.Author)) return url.Action("Index", "CmsArticles") ?? "/cms";
        return Home(url);
    }

    private static string Home(IUrlHelper url) => url.Action("Index", "Home") ?? "/";
}
