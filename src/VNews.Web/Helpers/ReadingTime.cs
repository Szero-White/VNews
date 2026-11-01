using System.Text.RegularExpressions;

namespace VNews.Web.Helpers;

/// <summary>Estimated reading time of an article body (HTML), from its real word count.</summary>
public static partial class ReadingTime
{
    public const int WordsPerMinute = 220;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    public static int Minutes(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return 1;
        var words = Tags().Replace(html, " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words / (double)WordsPerMinute));
    }
}
