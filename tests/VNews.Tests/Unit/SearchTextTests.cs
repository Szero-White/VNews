using System.Text;
using VNews.Application.Common;

namespace VNews.Tests.Unit;

public class SearchTextTests
{
    [Theory]
    [InlineData("giá vàng", "gia vang")]
    [InlineData("gia vang", "gia vang")]
    [InlineData("  GIÁ    VÀNG  ", "gia vang")]
    [InlineData("Giá\tvàng\n", "gia vang")]
    [InlineData("đường cao tốc", "duong cao toc")]
    [InlineData("ĐƯỜNG", "duong")]
    [InlineData("Thể thao: bóng đá, V-League!", "the thao bong da v league")]
    [InlineData("\"giá vàng\",", "gia vang")]
    public void Tokenize_FoldsCaseDiacriticsSpacesAndPunctuation(string input, string expected) =>
        Assert.Equal(expected, string.Join(' ', SearchText.Tokenize(input)));

    [Fact]
    public void Tokenize_TreatsDecomposedAndPrecomposedVietnameseTheSame()
    {
        var composed = "giá vàng ếầ".Normalize(NormalizationForm.FormC);
        var decomposed = "giá vàng ếầ".Normalize(NormalizationForm.FormD);
        Assert.Equal(SearchText.Tokenize(composed), SearchText.Tokenize(decomposed));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("!!! ??? ---")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Tokenize_WildcardsAndSymbolsOnly_GiveNoTokens_SoNothingMatchesEverything(string? input) =>
        Assert.Empty(SearchText.Tokenize(input));

    [Fact]
    public void Tokenize_TokensNeverContainLikeWildcards()
    {
        foreach (var token in SearchText.Tokenize("100% an_toan [gia] \\vang'; DROP TABLE x;--"))
        {
            Assert.All(token, ch => Assert.True(char.IsLetterOrDigit(ch), $"'{token}' has a non-alphanumeric character"));
        }
    }

    [Fact]
    public void Tokenize_CapsLengthTokenCountAndDuplicates()
    {
        var long5000 = new string('a', 5000);
        Assert.True(SearchText.Tokenize(long5000).Single().Length <= SearchText.MaxQueryLength);

        var many = string.Join(' ', Enumerable.Range(0, 40).Select(i => "t" + i));
        Assert.Equal(SearchText.MaxTokens, SearchText.Tokenize(many).Count);

        Assert.Equal(new[] { "vang" }, SearchText.Tokenize("vàng VANG vang"));
    }
}
