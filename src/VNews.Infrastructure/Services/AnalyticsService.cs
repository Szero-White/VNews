using Microsoft.EntityFrameworkCore;
using VNews.Application.DTOs.Cms;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);
    private static readonly TimeSpan ReloadWindow = TimeSpan.FromMinutes(30);
    private const int TopCount = 10;

    private readonly VNewsDbContext _context;

    public AnalyticsService(VNewsDbContext context) => _context = context;

    public async Task<bool> RecordViewAsync(int articleId, string visitorHash, bool isMobile, CancellationToken cancellationToken = default)
    {
        if (articleId <= 0 || string.IsNullOrWhiteSpace(visitorHash)) return false;
        var now = DateTime.UtcNow;
        var since = now - ReloadWindow;
        var repeated = await _context.ArticleViews.AnyAsync(
            v => v.ArticleId == articleId && v.VisitorHash == visitorHash && v.ViewedAtUtc >= since, cancellationToken);
        if (repeated) return false;

        _context.ArticleViews.Add(new ArticleView { ArticleId = articleId, VisitorHash = visitorHash, IsMobile = isMobile, ViewedAtUtc = now });
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AnalyticsReport> GetReportAsync(int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);
        var today = (DateTime.UtcNow + Vietnam).Date; // today in Vietnam
        var firstDay = today.AddDays(-(days - 1));
        var previousFirstDay = firstDay.AddDays(-days);
        var fromUtc = DateTime.SpecifyKind(firstDay - Vietnam, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(today.AddDays(1) - Vietnam, DateTimeKind.Utc);
        var previousFromUtc = DateTime.SpecifyKind(previousFirstDay - Vietnam, DateTimeKind.Utc);

        var wideViews = _context.ArticleViews.AsNoTracking().Where(v => v.ViewedAtUtc >= previousFromUtc && v.ViewedAtUtc < toUtc);
        var views = _context.ArticleViews.AsNoTracking().Where(v => v.ViewedAtUtc >= fromUtc && v.ViewedAtUtc < toUtc);

        var viewDays = await wideViews
            .GroupBy(v => v.ViewedAtUtc.AddHours(7).Date)
            .Select(g => new { Day = g.Key, Views = g.Count(), Visitors = g.Select(v => v.VisitorHash).Distinct().Count() })
            .ToListAsync(cancellationToken);

        var commentDays = await _context.Comments.AsNoTracking()
            .Where(c => c.CreatedAtUtc >= previousFromUtc && c.CreatedAtUtc < toUtc && c.Status != CommentStatus.Hidden)
            .GroupBy(c => c.CreatedAtUtc.AddHours(7).Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var publishedDays = await _context.Articles.AsNoTracking()
            .Where(a => a.Status == ArticleStatus.Published && a.PublishedAt != null && a.PublishedAt >= previousFromUtc && a.PublishedAt < toUtc)
            .GroupBy(a => a.PublishedAt!.Value.AddHours(7).Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var viewsByDay = viewDays.ToDictionary(d => d.Day);
        var commentsByDay = commentDays.ToDictionary(d => d.Day, d => d.Count);
        var publishedByDay = publishedDays.ToDictionary(d => d.Day, d => d.Count);

        DailyPoint Point(DateTime day) => new(
            day,
            viewsByDay.TryGetValue(day, out var v) ? v.Views : 0,
            viewsByDay.TryGetValue(day, out v) ? v.Visitors : 0,
            commentsByDay.GetValueOrDefault(day),
            publishedByDay.GetValueOrDefault(day));

        var current = Enumerable.Range(0, days).Select(i => Point(firstDay.AddDays(i))).ToList();
        var previous = Enumerable.Range(0, days).Select(i => Point(previousFirstDay.AddDays(i))).ToList();

        var topRaw = await views.GroupBy(v => v.ArticleId)
            .Select(g => new { ArticleId = g.Key, Views = g.Count() })
            .OrderByDescending(x => x.Views).ThenBy(x => x.ArticleId)
            .Take(TopCount).ToListAsync(cancellationToken);
        var topIds = topRaw.Select(t => t.ArticleId).ToList();
        var titles = await _context.Articles.AsNoTracking().Where(a => topIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Title, a.Slug, Category = a.Category.Name })
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        var topArticles = topRaw.Where(t => titles.ContainsKey(t.ArticleId))
            .Select(t => new TopArticleStat(t.ArticleId, titles[t.ArticleId].Title, titles[t.ArticleId].Slug, titles[t.ArticleId].Category, t.Views)).ToList();

        var topCategoryRows = await views.GroupBy(v => v.Article.Category.Name)
            .Select(g => new { Name = g.Key, Views = g.Count() })
            .OrderByDescending(c => c.Views).ThenBy(c => c.Name)
            .Take(TopCount).ToListAsync(cancellationToken);
        var topCategories = topCategoryRows.Select(c => new TopCategoryStat(c.Name, c.Views)).ToList();

        var mobile = await views.CountAsync(v => v.IsMobile, cancellationToken);
        var total = current.Sum(d => d.Views);

        var hourRows = await views.GroupBy(v => v.ViewedAtUtc.AddHours(7).Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var byHour = new int[24];
        foreach (var h in hourRows) if (h.Hour is >= 0 and < 24) byHour[h.Hour] = h.Count;

        var since = await _context.ArticleViews.AsNoTracking().MinAsync(v => (DateTime?)v.ViewedAtUtc, cancellationToken);

        return new AnalyticsReport
        {
            Days = days,
            Current = Totals(current),
            Previous = Totals(previous),
            Daily = current,
            TopArticles = topArticles,
            TopCategories = topCategories,
            MobileViews = mobile,
            DesktopViews = total - mobile,
            ViewsByHour = byHour,
            TrackingSinceUtc = since,
        };
    }

    private static PeriodTotals Totals(IReadOnlyCollection<DailyPoint> points) =>
        new(points.Sum(p => p.Views), points.Sum(p => p.Visitors), points.Sum(p => p.Comments), points.Sum(p => p.Published));
}
