namespace VNews.Application.DTOs.Cms;

/// <summary>Figures of one period. Visitors is the sum of each day's unique readers (a reader who comes on two days counts twice).</summary>
public sealed record PeriodTotals(int Views, int Visitors, int Comments, int Published);

public sealed record DailyPoint(DateTime Day, int Views, int Visitors, int Comments, int Published);

public sealed record TopArticleStat(int ArticleId, string Title, string Slug, string Category, int Views);

public sealed record TopCategoryStat(string Name, int Views);

/// <summary>Everything the statistics page shows for the last N days (days are Vietnam time) and the N days before, for comparison.</summary>
public sealed class AnalyticsReport
{
    public int Days { get; init; }
    public required PeriodTotals Current { get; init; }
    public required PeriodTotals Previous { get; init; }
    public IReadOnlyList<DailyPoint> Daily { get; init; } = Array.Empty<DailyPoint>();
    public IReadOnlyList<TopArticleStat> TopArticles { get; init; } = Array.Empty<TopArticleStat>();
    public IReadOnlyList<TopCategoryStat> TopCategories { get; init; } = Array.Empty<TopCategoryStat>();
    public int MobileViews { get; init; }
    public int DesktopViews { get; init; }

    /// <summary>Views per hour of the day (Vietnam time), always 24 entries.</summary>
    public IReadOnlyList<int> ViewsByHour { get; init; } = new int[24];

    /// <summary>When the first view was ever recorded; null until the site has had a reader since tracking began.</summary>
    public DateTime? TrackingSinceUtc { get; init; }
}
