using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VNews.Application.Interfaces;
using VNews.Infrastructure.Persistence;

namespace VNews.Web.Services;

public sealed record UnusedUpload(string FileName, string Url, long Bytes, DateTime LastWriteUtc);

public sealed record UploadScan(int TotalFiles, int ReferencedFiles, int TooNewFiles, IReadOnlyList<UnusedUpload> Unused)
{
    public long UnusedBytes => Unused.Sum(u => u.Bytes);
}

public sealed record UploadCleanupResult(int Moved, long Bytes);

/// <summary>
/// Finds cover images in uploads/articles that no article uses any more and quarantines them.
/// Safety rules: only files this application generated (32 hex characters + .jpg/.png/.webp) are considered; a file is "in use" when
/// ANY article (every status, draft included) references it in its cover URL or inside its body; files younger than the grace period are
/// never touched (they may belong to a form that is still open); and nothing is deleted: files are MOVED to App_Data/unused-uploads
/// (outside the web root, so no longer served) where they can be put back. The check runs again at the moment of moving.
/// </summary>
public sealed class UploadCleanupService
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);
    private const string UrlPrefix = "/uploads/articles/";
    private static readonly Regex GeneratedName = new("^[0-9a-f]{32}\\.(jpg|png|webp)$", RegexOptions.Compiled);
    private static readonly Regex UrlInText = new("/uploads/articles/([0-9a-f]{32}\\.(?:jpg|png|webp))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IWebHostEnvironment _environment;
    private readonly UploadStorage _storage;
    private readonly VNewsDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly ILogger<UploadCleanupService> _logger;

    public UploadCleanupService(IWebHostEnvironment environment, UploadStorage storage, VNewsDbContext db, IAuditLogService audit, ILogger<UploadCleanupService> logger)
    {
        _environment = environment;
        _storage = storage;
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    private string UploadsRoot => _storage.ArticlesPath;
    private string QuarantineRoot => Path.Combine(_environment.ContentRootPath, "App_Data", "unused-uploads");

    public async Task<UploadScan> ScanAsync(DateTime? nowUtc = null, CancellationToken cancellationToken = default)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        if (!Directory.Exists(UploadsRoot)) return new UploadScan(0, 0, 0, Array.Empty<UnusedUpload>());

        var referenced = await ReferencedFileNamesAsync(cancellationToken);
        var files = new DirectoryInfo(UploadsRoot).EnumerateFiles().Where(f => GeneratedName.IsMatch(f.Name)).ToList();

        var unused = new List<UnusedUpload>();
        var tooNew = 0;
        var inUse = 0;
        foreach (var file in files)
        {
            if (referenced.Contains(file.Name)) { inUse++; continue; }
            if (now - file.LastWriteTimeUtc < GracePeriod) { tooNew++; continue; }
            unused.Add(new UnusedUpload(file.Name, UrlPrefix + file.Name, file.Length, file.LastWriteTimeUtc));
        }
        return new UploadScan(files.Count, inUse, tooNew, unused.OrderBy(u => u.LastWriteUtc).ToList());
    }

    /// <summary>Moves the currently unused, old-enough files to quarantine. Re-scans first, so a file used since the page was opened is skipped.</summary>
    public async Task<UploadCleanupResult> QuarantineUnusedAsync(string actorId, string actorName, DateTime? nowUtc = null, CancellationToken cancellationToken = default)
    {
        var scan = await ScanAsync(nowUtc, cancellationToken);
        if (scan.Unused.Count == 0) return new UploadCleanupResult(0, 0);

        Directory.CreateDirectory(QuarantineRoot);
        var moved = 0;
        long bytes = 0;
        foreach (var file in scan.Unused)
        {
            var source = Path.Combine(UploadsRoot, file.FileName);
            var target = Path.Combine(QuarantineRoot, file.FileName);
            try
            {
                if (!File.Exists(source) || File.Exists(target)) continue;
                File.Move(source, target);
                moved++;
                bytes += file.Bytes;
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not quarantine {File}.", file.FileName);
            }
        }

        if (moved > 0)
        {
            await _audit.LogAsync(actorId, actorName, "QuarantineUploads", "Upload", "articles", $"Chuyển {moved} ảnh không còn được bài nào dùng vào thư mục lưu tạm ({bytes / 1024} KB).", cancellationToken);
        }
        return new UploadCleanupResult(moved, bytes);
    }

    private async Task<HashSet<string>> ReferencedFileNamesAsync(CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Cover URLs of every article, any status.
        foreach (var url in await _db.Articles.AsNoTracking().Where(a => a.CoverImageUrl != null).Select(a => a.CoverImageUrl!).ToListAsync(cancellationToken))
        {
            foreach (Match match in UrlInText.Matches(url)) names.Add(match.Groups[1].Value);
        }
        // The pictures of every article's gallery (below the text), any status.
        foreach (var url in await _db.ArticleImages.AsNoTracking().Select(i => i.Url).ToListAsync(cancellationToken))
        {
            foreach (Match match in UrlInText.Matches(url)) names.Add(match.Groups[1].Value);
        }
        // Images embedded in article bodies.
        var bodies = _db.Articles.AsNoTracking().Where(a => a.Body.Contains(UrlPrefix)).Select(a => a.Body);
        await foreach (var body in bodies.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            foreach (Match match in UrlInText.Matches(body)) names.Add(match.Groups[1].Value);
        }
        return names;
    }
}
