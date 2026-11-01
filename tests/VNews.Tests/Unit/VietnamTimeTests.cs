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
}
