using Ganss.Xss;

namespace VNews.Infrastructure.Common;

/// <summary>
/// The one allow-list for article HTML. Everything an editor types is cleaned here before it is stored, so the public page can render the body
/// as HTML: only the listed tags, only href/src/alt, only http/https links; scripts, event handlers and styles never survive.
/// </summary>
public static class ArticleHtmlSanitizer
{
    public const int MinFontSize = 8;
    public const int MaxFontSize = 96;

    /// <summary>
    /// Text sizes (any whole px from MinFontSize to MaxFontSize, as fs-N) and the font families the editor offers (ff-...). The same names are
    /// styled in article-page.css and body-editor.css.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedClassNames = Enumerable.Range(MinFontSize, MaxFontSize - MinFontSize + 1).Select(n => $"fs-{n}")
        .Concat(new[] { "ff-arial", "ff-times", "ff-georgia", "ff-tahoma", "ff-verdana", "ff-courier" }).ToArray();

    private static readonly HtmlSanitizer Sanitizer = Build();

    public static string Sanitize(string html) => Sanitizer.Sanitize(html);

    private static HtmlSanitizer Build()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "h2", "h3", "h4", "blockquote", "a", "img", "span", "figure", "figcaption" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("src");
        sanitizer.AllowedAttributes.Add("alt");

        // Font size and family come from the editor as classes on <span>. Only these exact class names survive; there is no style
        // attribute, so nothing else about appearance can be injected.
        sanitizer.AllowedAttributes.Add("class");
        foreach (var name in AllowedClassNames) sanitizer.AllowedClasses.Add(name);

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");

        return sanitizer;
    }
}
