using VNews.Application.Common;

namespace VNews.Tests.Unit;

/// <summary>Text typed as plain lines keeps its paragraphs on the page; HTML from the editor is left alone.</summary>
public sealed class ArticleBodyFormatterTests
{
    [Fact]
    public void PlainText_BlankLineStartsAParagraph_SingleBreakBecomesBr()
    {
        var html = ArticleBodyFormatter.ToHtml("Đoạn một.\r\nVẫn đoạn một.\r\n\r\nĐoạn hai.\n\n\n  Đoạn ba.");
        Assert.Equal("<p>Đoạn một.<br>Vẫn đoạn một.</p><p>Đoạn hai.</p><p>Đoạn ba.</p>", html);
    }

    [Fact]
    public void PlainText_IsEncoded_SoTagsInTheTextAreNotMarkup()
    {
        Assert.Equal("<p>a &amp; b &lt;i&gt;x&lt;/i&gt;</p>", ArticleBodyFormatter.ToHtml("a & b <i>x</i>"));
    }

    [Theory]
    [InlineData("<p>Đã có đoạn văn.</p>")]
    [InlineData("<h2>Tiêu đề</h2><p>Nội dung</p>")]
    [InlineData("Dòng một<br>dòng hai")]
    [InlineData("<ul><li>một</li></ul>")]
    public void Html_IsLeftAlone(string html) => Assert.Equal(html, ArticleBodyFormatter.ToHtml(html));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Empty_GivesNothing(string? value) => Assert.Equal(string.Empty, ArticleBodyFormatter.ToHtml(value));
}
