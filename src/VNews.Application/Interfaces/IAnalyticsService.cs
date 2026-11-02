using VNews.Application.DTOs.Cms;

namespace VNews.Application.Interfaces;

public interface IAnalyticsService
{
    /// <summary>Records a reading. Returns false when the same visitor already read the same article in the last 30 minutes (a reload).</summary>
    Task<bool> RecordViewAsync(int articleId, string visitorHash, bool isMobile, CancellationToken cancellationToken = default);

    /// <summary>The report for the last <paramref name="days"/> days including today (Vietnam time), with the previous period for comparison.</summary>
    Task<AnalyticsReport> GetReportAsync(int days, CancellationToken cancellationToken = default);
}
