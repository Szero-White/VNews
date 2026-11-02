using System.Net;
using System.Text.RegularExpressions;

namespace VNews.Application.Common;

/// <summary>
/// Turns what an editor typed into the HTML the page shows. Text typed or pasted as plain lines (no HTML tags) keeps its paragraphs:
/// a blank line starts a new paragraph, a single line break becomes a line break. HTML (what the visual editor produces) is left alone.
/// Not a security step: the sanitizer still runs on everything that is saved.
/// </summary>
public static class ArticleBodyFormatter
{
    private static readonly Regex BlockTag = new(@"<\s*(p|h[1-6]|ul|ol|li|blockquote|div|figure|table|img|br)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BlankLines = new(@"\n\s*\n", RegexOptions.Compiled);

    public static string ToHtml(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        if (BlockTag.IsMatch(body)) return body;

        var text = body.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var paragraphs = BlankLines.Split(text)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Select(p => "<p>" + WebUtility.HtmlEncode(p).Replace("\n", "<br>") + "</p>");
        return string.Concat(paragraphs);
    }
}
