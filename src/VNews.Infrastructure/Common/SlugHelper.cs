namespace VNews.Infrastructure.Common;

internal static class SlugHelper
{
    private static readonly Dictionary<string, string> DiacriticsMap = new()
    {
        {"à","a"},{"á","a"},{"ả","a"},{"ã","a"},{"ạ","a"},
        {"ă","a"},{"ằ","a"},{"ắ","a"},{"ẳ","a"},{"ẵ","a"},{"ặ","a"},
        {"â","a"},{"ầ","a"},{"ấ","a"},{"ẩ","a"},{"ẫ","a"},{"ậ","a"},
        {"è","e"},{"é","e"},{"ẻ","e"},{"ẽ","e"},{"ẹ","e"},
        {"ê","e"},{"ề","e"},{"ế","e"},{"ể","e"},{"ễ","e"},{"ệ","e"},
        {"ì","i"},{"í","i"},{"ỉ","i"},{"ĩ","i"},{"ị","i"},
        {"ò","o"},{"ó","o"},{"ỏ","o"},{"õ","o"},{"ọ","o"},
        {"ô","o"},{"ồ","o"},{"ố","o"},{"ổ","o"},{"ỗ","o"},{"ộ","o"},
        {"ơ","o"},{"ờ","o"},{"ớ","o"},{"ở","o"},{"ỡ","o"},{"ợ","o"},
        {"ù","u"},{"ú","u"},{"ủ","u"},{"ũ","u"},{"ụ","u"},
        {"ư","u"},{"ừ","u"},{"ứ","u"},{"ử","u"},{"ữ","u"},{"ự","u"},
        {"ỳ","y"},{"ý","y"},{"ỷ","y"},{"ỹ","y"},{"ỵ","y"},
        {"đ","d"}
    };

    public static string Slugify(string input)
    {
        var normalized = input.ToLowerInvariant();

        foreach (var pair in DiacriticsMap)
        {
            normalized = normalized.Replace(pair.Key, pair.Value);
        }

        var chars = normalized.Select(c => char.IsLetterOrDigit(c) || c == ' ' ? c : ' ').ToArray();
        var cleaned = new string(chars);
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('-', parts);
    }
}
