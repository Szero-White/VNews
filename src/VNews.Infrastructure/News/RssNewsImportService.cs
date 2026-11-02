using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VNews.Application.Interfaces;
using VNews.Domain.Entities;
using VNews.Infrastructure.Common;
using VNews.Infrastructure.Persistence;

namespace VNews.Infrastructure.News;

public sealed class RssNewsImportService : INewsImportService
{
    public const string SourceName = "VnExpress";
    private const int ItemsPerChannel = 12;

    /// <summary>Publisher RSS channel -> VNews category slug. Channels whose category does not exist are skipped.</summary>
    private static readonly (string Channel, string CategorySlug)[] Channels =
    {
        ("thoi-su", "thoi-su"), ("kinh-doanh", "kinh-doanh"), ("the-gioi", "the-gioi"), ("khoa-hoc-cong-nghe", "cong-nghe"),
        ("the-thao", "the-thao"), ("giai-tri", "giai-tri"), ("suc-khoe", "suc-khoe"), ("giao-duc", "giao-duc"),
        ("du-lich", "du-lich"), ("phap-luat", "phap-luat"),
        // Sub-channels of "Thời sự": used only when the matching sub-category has been created in the CMS.
        ("chinh-tri", "chinh-tri"), ("dan-sinh", "dan-sinh"), ("giao-thong", "giao-thong"), ("lao-dong-viec-lam", "viec-lam"),
    };

    private readonly HttpClient _http;
    private readonly VNewsDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly ILogger<RssNewsImportService> _logger;
    private readonly bool _showSourceThumbnails;

    public RssNewsImportService(HttpClient http, VNewsDbContext db, IAuditLogService audit, IConfiguration configuration, ILogger<RssNewsImportService> logger)
    {
        _http = http;
        _db = db;
        _audit = audit;
        _logger = logger;
        // Off by default: the publisher's pictures are not ours to show unless the owner of this site decides so.
        _showSourceThumbnails = configuration.GetValue("NewsImport:ShowSourceThumbnails", false);
    }

    public async Task<NewsImportResult> ImportAsync(string actingUserId, string actingUserName, CancellationToken cancellationToken = default)
    {
        var categories = await _db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Slug, c => c.Id, cancellationToken);
        var added = 0;
        var skipped = 0;
        var problems = new List<string>();

        foreach (var (channel, categorySlug) in Channels)
        {
            if (!categories.TryGetValue(categorySlug, out var categoryId)) continue;

            try
            {
                var (a, s) = await ImportChannelAsync(channel, categoryId, cancellationToken);
                added += a;
                skipped += s;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or DbUpdateException)
            {
                // One unreachable channel or a duplicate race must not stop the others.
                _logger.LogWarning(ex, "News import: channel {Channel} failed.", channel);
                problems.Add($"Kênh “{channel}”: không nạp được.");
                _db.ChangeTracker.Clear();
            }
        }

        await _audit.LogAsync(actingUserId, actingUserName, "ImportNews", "Article", "feed", $"Nạp tin từ {SourceName}: thêm {added}, bỏ qua {skipped}", cancellationToken);
        return new NewsImportResult(added, skipped, problems);
    }

    private async Task<(int Added, int Skipped)> ImportChannelAsync(string channel, int categoryId, CancellationToken cancellationToken)
    {
        var xml = await _http.GetStringAsync($"https://vnexpress.net/rss/{channel}.rss", cancellationToken);
        var items = RssFeedParser.Parse(xml, ItemsPerChannel);
        if (items.Count == 0) return (0, 0);

        var links = items.Select(i => i.Link).ToList();
        var existing = await _db.Articles.Where(a => a.SourceUrl != null && links.Contains(a.SourceUrl)).ToListAsync(cancellationToken);
        var known = existing.Select(a => a.SourceUrl!).ToHashSet();

        // A story first imported under the parent section moves to the more specific sub-category that now carries it.
        var parentId = await _db.Categories.Where(c => c.Id == categoryId).Select(c => c.ParentId).FirstOrDefaultAsync(cancellationToken);
        var moved = parentId is null ? 0 : existing.Count(a => a.CategoryId == parentId);
        foreach (var article in existing.Where(a => parentId is not null && a.CategoryId == parentId)) article.CategoryId = categoryId;
        var slugs = (await _db.Articles.AsNoTracking().Select(a => a.Slug).ToListAsync(cancellationToken)).ToHashSet();

        var added = 0;
        foreach (var item in items.Where(i => !known.Contains(i.Link)))
        {
            var slug = BuildSlug(item, slugs);
            _db.Articles.Add(new Article
            {
                Title = item.Title,
                Slug = slug,
                Summary = item.Summary.Length > 0 ? item.Summary : item.Title,
                Body = BuildBody(item),
                CoverImageUrl = _showSourceThumbnails ? item.ImageUrl : null,
                AuthorName = SourceName,
                Status = ArticleStatus.Published,
                PublishedAt = item.PublishedUtc,
                CreatedAt = DateTime.UtcNow,
                CategoryId = categoryId,
                SourceName = SourceName,
                SourceUrl = item.Link,
            });
            slugs.Add(slug);
            added++;
        }

        if (added > 0 || moved > 0) await _db.SaveChangesAsync(cancellationToken);
        return (added, items.Count - added);
    }

    private static string BuildSlug(FeedItem item, HashSet<string> taken)
    {
        var text = SlugHelper.Slugify(item.Title);
        if (text.Length > 200) text = text[..200].TrimEnd('-');
        var id = RssFeedParser.SourceId(item.Link) ?? Guid.NewGuid().ToString("N")[..8];
        var slug = string.IsNullOrEmpty(text) ? id : $"{text}-{id}";
        for (var n = 2; taken.Contains(slug); n++) slug = $"{text}-{id}-{n}";
        return slug;
    }

    /// <summary>
    /// The page already shows the publisher's short summary above the body, so the body is only the credit and the link to the full
    /// article on the publisher's site (the full text is theirs and is never copied).
    /// </summary>
    private static string BuildBody(FeedItem item)
    {
        var link = WebUtility.HtmlEncode(item.Link);
        return ArticleHtmlSanitizer.Sanitize($"<p>Nguồn: {SourceName}. <a href=\"{link}\">Đọc bài đầy đủ tại {SourceName}</a>.</p>");
    }
}
