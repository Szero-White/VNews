using System.Globalization;
using System.Text;

namespace VNews.Application.Common;

/// <summary>
/// Turns what a reader types into search tokens: lower-cased, diacritics removed ("giá vàng" and "gia vang" give the
/// same tokens), split on whitespace/punctuation. Tokens contain only letters and digits, so they hold none of the
/// LIKE wildcards (% _ [) and a reader cannot inject wildcard patterns.
/// </summary>
public static class SearchText
{
    public const int MaxQueryLength = 120;
    public const int MaxTokens = 6;

    public static IReadOnlyList<string> Tokenize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        var text = query.Length > MaxQueryLength ? query[..MaxQueryLength] : query;
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue; // combining accent/tone mark
            }

            var folded = ch switch { 'đ' or 'Đ' => 'd', _ => char.ToLowerInvariant(ch) };
            if (char.IsLetterOrDigit(folded))
            {
                current.Append(folded);
            }
            else
            {
                Flush(tokens, current);
            }
        }
        Flush(tokens, current);

        return tokens.Distinct().Take(MaxTokens).ToArray();
    }

    private static void Flush(List<string> tokens, StringBuilder current)
    {
        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
            current.Clear();
        }
    }
}
