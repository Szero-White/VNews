using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace VNews.Web.Helpers;

/// <summary>
/// Turns a request into an anonymous, daily-changing visitor hash for the statistics, and tells crawlers from readers.
/// Neither the address nor the user agent is stored; the hash cannot be reversed and differs every day.
/// </summary>
public static partial class VisitorFingerprint
{
    [GeneratedRegex("bot|crawl|spider|slurp|headless|preview|monitor|curl|wget|python|httpclient|lighthouse|facebookexternalhit|axios|java/", RegexOptions.IgnoreCase)]
    private static partial Regex Crawler();

    [GeneratedRegex("mobi|android|iphone|ipad", RegexOptions.IgnoreCase)]
    private static partial Regex MobileAgent();

    public static bool IsCrawler(string? userAgent) => string.IsNullOrWhiteSpace(userAgent) || Crawler().IsMatch(userAgent);

    public static bool IsMobile(string? userAgent) => !string.IsNullOrEmpty(userAgent) && MobileAgent().IsMatch(userAgent);

    public static string Hash(HttpContext context, DateTime utcNow)
    {
        var raw = $"{context.Connection.RemoteIpAddress}|{context.Request.Headers.UserAgent}|{utcNow:yyyyMMdd}|vnews-analytics";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..32];
    }
}
