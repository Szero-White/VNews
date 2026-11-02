using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace VNews.Infrastructure.News;

/// <summary>One entry of a news feed, reduced to what VNews stores: a headline, a short plain-text summary and a link back to the publisher.</summary>
public sealed record FeedItem(string Title, string Summary, string Link, DateTime PublishedUtc, string? ImageUrl);

/// <summary>
/// Reads a VnExpress RSS document. Pure (no network, no database) so it can be tested with a fixed XML sample. Nothing from the feed is
/// trusted: DTDs and external entities are refused, HTML is reduced to text, and only https links on the publisher's own hosts are kept.
/// </summary>
public static class RssFeedParser
{
    public const int MaxTitleLength = 300;
    public const int MaxSummaryLength = 500;

    private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public static IReadOnlyList<FeedItem> Parse(string xml, int maxItems = 12)
    {
        if (string.IsNullOrWhiteSpace(xml)) return Array.Empty<FeedItem>();

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return Array.Empty<FeedItem>();
        }

        var items = new List<FeedItem>();
        foreach (var node in document.Descendants("item"))
        {
            var item = ToItem(node);
            if (item is not null) items.Add(item);
            if (items.Count >= maxItems) break;
        }

        return items;
    }

    private static FeedItem? ToItem(XElement node)
    {
        var title = ToPlainText((string?)node.Element("title"));
        var link = ((string?)node.Element("link"))?.Trim();
        if (title.Length == 0 || !IsPublisherLink(link)) return null;

        var summary = ToPlainText((string?)node.Element("description"));
        var published = DateTimeOffset.TryParse((string?)node.Element("pubDate"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when)
            ? when.UtcDateTime
            : DateTime.UtcNow;
        var image = (string?)node.Element("enclosure")?.Attribute("url");

        return new FeedItem(
            Truncate(title, MaxTitleLength),
            Truncate(summary, MaxSummaryLength),
            link!,
            published,
            IsPublisherImage(image) ? image : null);
    }

    /// <summary>Headline and summary arrive as HTML/CDATA (a thumbnail link plus text): keep the words only.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = WebUtility.HtmlDecode(Tags.Replace(html, " "));
        return Spaces.Replace(text, " ").Trim();
    }

    /// <summary>Only https links on vnexpress.net: the one place a reader is sent to.</summary>
    public static bool IsPublisherLink(string? link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && IsHost(uri.Host, "vnexpress.net");

    public static bool IsPublisherImage(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && IsHost(uri.Host, "vnecdn.net");

    /// <summary>The numeric id VnExpress puts at the end of every article address (".../ten-bai-5139282.html"), or null.</summary>
    public static string? SourceId(string link)
    {
        var match = Regex.Match(link, @"-(\d+)\.html(?:$|[?#])");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsHost(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";
}
