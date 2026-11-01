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

    /// <summary>
    /// Relative time for listings: "Vừa xong", "5 phút trước", "3 giờ trước", "2 ngày trước"; from a week on, the
    /// Vietnam-local date (d/M/yyyy). A time in the future (clock skew) is shown as "Vừa xong".
    /// </summary>
    public static string Ago(DateTime publishedUtc, DateTime nowUtc)
    {
        var age = nowUtc - DateTime.SpecifyKind(publishedUtc, DateTimeKind.Utc);
        if (age < TimeSpan.FromMinutes(1)) return "Vừa xong";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} phút trước";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours} giờ trước";
        if (age < TimeSpan.FromDays(7)) return $"{(int)age.TotalDays} ngày trước";
        var local = ToLocal(publishedUtc);
        return $"{local.Day}/{local.Month}/{local.Year}";
    }
}
