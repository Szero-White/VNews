namespace VNews.Application.DTOs.Cms;

/// <summary>Article counts by workflow status, for the whole newsroom or for one author.</summary>
public sealed class CmsStatsDto
{
    public int Draft { get; set; }
    public int PendingReview { get; set; }
    public int Rejected { get; set; }
    public int Published { get; set; }
    /// <summary>Articles in the trash; not part of <see cref="Total"/>.</summary>
    public int Trashed { get; set; }
    public int Total => Draft + PendingReview + Rejected + Published;
}
