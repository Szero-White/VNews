using Ganss.Xss;

namespace VNews.Infrastructure.Common;

/// <summary>
/// The one allow-list for article HTML. Everything an editor types is cleaned here before it is stored, so the public page can render the body
/// as HTML: only the listed tags, only href/src/alt, only http/https links; scripts, event handlers and styles never survive.
/// </summary>
public static class ArticleHtmlSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = Build();

    public static string Sanitize(string html) => Sanitizer.Sanitize(html);

    private static HtmlSanitizer Build()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "h2", "h3", "h4", "blockquote", "a", "img" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("src");
        sanitizer.AllowedAttributes.Add("alt");

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");

        return sanitizer;
    }
}
