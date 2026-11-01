namespace VNews.Web.Helpers;

/// <summary>Formats stored UTC timestamps for Vietnamese readers (fixed UTC+7, no daylight saving).</summary>
public static class VietnamTime
{
    private static readonly string[] Weekdays = { "Chủ nhật", "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy" };

    public static DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);

    /// <summary>e.g. "Chủ nhật, 1/11/2026, 09:30 (GMT+7)".</summary>
    public static string FormatLong(DateTime utc)
    {
        var local = ToLocal(utc);
        return $"{Weekdays[(int)local.DayOfWeek]}, {local.Day}/{local.Month}/{local.Year}, {local:HH:mm} (GMT+7)";
    }

    /// <summary>ISO-8601 value with offset for the time element's datetime attribute.</summary>
    public static string ToIso(DateTime utc) => ToLocal(utc).ToString("yyyy-MM-ddTHH:mm:ss") + "+07:00";
}
