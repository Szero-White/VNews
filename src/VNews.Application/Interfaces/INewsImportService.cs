namespace VNews.Application.Interfaces;

/// <summary>What one import run did.</summary>
public sealed record NewsImportResult(int Added, int Skipped, IReadOnlyList<string> Problems)
{
    public bool HasProblems => Problems.Count > 0;
}

/// <summary>
/// Brings in real headlines from the publisher's public RSS feeds: title, short summary, link back and publication date, with the
/// source named on every article. Full article text is never copied. Running it again adds only what is new.
/// </summary>
public interface INewsImportService
{
    Task<NewsImportResult> ImportAsync(string actingUserId, string actingUserName, CancellationToken cancellationToken = default);
}
