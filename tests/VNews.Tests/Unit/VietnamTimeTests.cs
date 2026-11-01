using VNews.Web.Helpers;

namespace VNews.Tests.Unit;

public class VietnamTimeTests
{
    [Fact]
    public void FormatLong_ConvertsUtcToGmtPlus7_WithVietnameseWeekday()
    {
        // 02:30 UTC on Sunday 1 Nov 2026 is 09:30 in Vietnam, same day.
        Assert.Equal("Chủ nhật, 1/11/2026, 09:30 (GMT+7)", VietnamTime.FormatLong(new DateTime(2026, 11, 1, 2, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void FormatLong_RollsOverToNextLocalDay()
    {
        // 18:05 UTC Saturday is 01:05 Sunday in Vietnam.
        Assert.Equal("Chủ nhật, 1/11/2026, 01:05 (GMT+7)", VietnamTime.FormatLong(new DateTime(2026, 10, 31, 18, 5, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void ToIso_CarriesTheOffset() =>
        Assert.Equal("2026-11-01T09:30:00+07:00", VietnamTime.ToIso(new DateTime(2026, 11, 1, 2, 30, 0, DateTimeKind.Utc)));

    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, "Vừa xong")]
    [InlineData(30, "Vừa xong")]
    [InlineData(60, "1 phút trước")]
    [InlineData(59 * 60, "59 phút trước")]
    [InlineData(3600, "1 giờ trước")]
    [InlineData(23 * 3600 + 3599, "23 giờ trước")]
    [InlineData(24 * 3600, "1 ngày trước")]
    [InlineData(6 * 86400 + 80000, "6 ngày trước")]
    [InlineData(-120, "Vừa xong")] // clock skew: published "in the future"
    public void Ago_ChoosesTheRightUnit(int secondsAgo, string expected) =>
        Assert.Equal(expected, VietnamTime.Ago(Now.AddSeconds(-secondsAgo), Now));

    [Fact]
    public void Ago_FromAWeekOn_ShowsTheVietnamLocalDate()
    {
        // 18:00 UTC on 20 Oct is already 21 Oct 01:00 in Vietnam.
        Assert.Equal("21/10/2026", VietnamTime.Ago(new DateTime(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc), Now));
    }
}
