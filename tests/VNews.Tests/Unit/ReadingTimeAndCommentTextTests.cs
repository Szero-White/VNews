using VNews.Infrastructure.Services;
using VNews.Web.Helpers;

namespace VNews.Tests.Unit;

public class ReadingTimeAndCommentTextTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("<p>ngắn</p>", 1)]
    public void ReadingTime_IsAtLeastOneMinute(string? html, int expected) => Assert.Equal(expected, ReadingTime.Minutes(html));

    [Fact]
    public void ReadingTime_CountsWordsNotMarkup()
    {
        var words = string.Join(' ', Enumerable.Repeat("từ", 441)); // 441 words at 220/min -> 3 minutes
        Assert.Equal(3, ReadingTime.Minutes($"<p class=\"x\"><strong>{words}</strong></p>"));
        Assert.Equal(1, ReadingTime.Minutes("<div class=\"rất-dài\" data-x=\"" + new string('a', 5000) + "\">hai từ</div>"));
    }

    [Theory]
    [InlineData("  xin chào  ", "xin chào")]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\n\n\n\n\nb", "a\n\nb")]
    [InlineData("an\u0000toàn\u0007", "antoàn")]
    [InlineData("<b>vẫn là chữ</b>", "<b>vẫn là chữ</b>")] // stored as text; encoded when rendered
    [InlineData(null, "")]
    public void CommentText_IsNormalisedToPlainText(string? input, string expected) => Assert.Equal(expected, CommentService.Normalize(input));
}
